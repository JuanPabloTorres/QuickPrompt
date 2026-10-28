using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

public sealed record RememberedContextSummary(Guid Id, string Title, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt);

/// <summary>
/// Creates actor-bound adapters for existing agent ports. Only an explicit save/restore flow
/// selects persisted context; the local host keeps its default transient sessions.
/// </summary>
public sealed class AccountAgentMemory(NpgsqlDataSource data)
{
    public AgentSessions RememberedSessions(LibraryActor actor, Guid libraryId, int? expectedMemoryRevision = null) =>
        new(new PostgresAgentSessionStore(this, actor, libraryId, expectedMemoryRevision));
    public AgentSessions ActiveSessions(LibraryActor actor, Guid libraryId) => new(new AccountActiveSessions(this, actor, libraryId));
    public IPreparationPreferences Preferences(LibraryActor actor) => new PostgresAgentPreferences(this, actor);
    public IPromptCatalog Catalog(LibraryActor actor, Guid libraryId) => new AccountPromptCatalog(this, actor, libraryId);
    public PromptLibrary Library(LibraryActor actor, Guid libraryId)
    {
        var store = new AccountPromptStore(this, actor, libraryId);
        return new(store, store);
    }
    public ICollectionReader Collections(LibraryActor actor, Guid libraryId) => new AccountCollectionReader(this, actor, libraryId);
    public IPromptRetriever Retriever(LibraryActor actor, Guid libraryId) => new CatalogPromptRetriever(Catalog(actor, libraryId), Collections(actor, libraryId));

    /// <summary>Call within the queue fence before a user replaces active conversation context.</summary>
    public void RequireIdleConversation(LibraryActor actor, Guid libraryId) => InScope(actor, libraryId, database =>
    {
        var now = database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").Single();
        if (database.LiveRuns.Any(run => run.OwnerId == actor.Id && !run.Terminal && run.LeaseUntil > now))
            throw new ConflictException("Detén la ejecución activa antes de cambiar su contexto.");
        return true;
    });

    public RememberedContextSummary[] List(LibraryActor actor, Guid libraryId) => InScope(actor, libraryId, database =>
    {
        Prune(database, actor);
        return database.AgentContexts.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId)
            .OrderByDescending(x => x.UpdatedAt).Take(100)
            .Select(x => new RememberedContextSummary(x.SessionId, x.Title, x.UpdatedAt, x.ExpiresAt)).ToArray();
    });

    internal T InScope<T>(LibraryActor actor, Guid? libraryId, Func<AccountDatabase, T> operation)
    {
        return WithDatabase(database => AccountTransaction.Run(database, actor, () =>
        {
            if (libraryId is { } id && !database.Libraries.AsNoTracking().Any(x => x.Id == id && x.OwnerId == actor.Id))
                throw new LibraryUnavailableException();
            return operation(database);
        }));
    }

    // Only synchronous, trusted adapters call this. The owning domain service must
    // establish its actor transaction; no context survives an inference await.
    internal T WithDatabase<T>(Func<AccountDatabase, T> operation)
    {
        using var database = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(data,
            postgres => postgres.CommandTimeout(10).MigrationsHistoryTable("history", "qp_migrations")).Options);
        return operation(database);
    }

    internal static void Prune(AccountDatabase database, LibraryActor actor)
    {
        // Retention is actor-scoped. One account cannot evict another account's context.
        database.AgentContexts.Where(x => x.OwnerId == actor.Id && x.ExpiresAt <= DateTimeOffset.UtcNow).ExecuteDelete();
    }
}

internal sealed class PostgresAgentSessionStore(AccountAgentMemory memory, LibraryActor actor, Guid libraryId, int? expectedMemoryRevision) : IAgentSessionStore
{

    public AgentSession? Get(Guid id) => memory.InScope(actor, libraryId, database =>
    {
        AccountAgentMemory.Prune(database, actor);
        var stored = database.AgentContexts.AsNoTracking().SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id);
        return stored is null ? null : Deserialize(stored);
    });

    public void Put(AgentSession session) => Update(session.Id, _ => session);

    public AgentSession Update(Guid id, Func<AgentSession?, AgentSession> transition)
    {
        if (id == Guid.Empty) throw new ValidationException("Sesión inválida.");
        return memory.InScope(actor, libraryId, database =>
        {
            // Lock the owner's bounded memory set as well as serializing this session's revisions.
            // The lock ends before any model call; no process-wide actor or DbContext is retained.
            database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5281, hashtext({actor.Id.ToString()}))");
            AccountAgentMemory.Prune(database, actor);
            var stored = database.AgentContexts.SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id);
            if (expectedMemoryRevision is { } expected && expected != (stored?.Revision ?? 0))
                throw new ConflictException("El contexto o sus valores cambiaron. Recupera su revisión actual.");
            var next = transition(stored is null ? null : Deserialize(stored));
            // A retained artifact is data for review, never an approval or an instruction to replay effects.
            next = next with { Approval = "NOT_GRANTED", PendingAction = null, PendingPlan = null, PendingCommand = null, PendingRequest = null,
                StorageRevision = checked((stored?.Revision ?? 0) + 1) };
            var payload = AccountSessionData.Encode(database, libraryId, id, next);
            stored ??= new RememberedAgentContext { OwnerId = actor.Id, LibraryId = libraryId, SessionId = id };
            if (database.Entry(stored).State == EntityState.Detached) database.AgentContexts.Add(stored);
            stored.Payload = payload; stored.Title = next.Draft?.Title ?? next.Source?.Title ?? "Contexto de NOVA";
            stored.Revision = next.StorageRevision;
            stored.UpdatedAt = DateTimeOffset.UtcNow; stored.ExpiresAt = stored.UpdatedAt.AddDays(7);
            database.SaveChanges();
            var expiredByLimit = database.AgentContexts.Where(x => x.OwnerId == actor.Id)
                .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.LibraryId).ThenBy(x => x.SessionId).Skip(100).ToArray();
            database.AgentContexts.RemoveRange(expiredByLimit); database.SaveChanges();
            return next;
        });
    }

    public void Forget(Guid id) => memory.InScope(actor, libraryId, database =>
    {
        database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5281, hashtext({actor.Id.ToString()}))");
        return database.AgentContexts.Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id).ExecuteDelete();
    });

    private static AgentSession Deserialize(RememberedAgentContext context) => AccountSessionData.Decode(context.Payload, context.SessionId) with {
        Approval = "NOT_GRANTED", PendingAction = null, PendingPlan = null, PendingCommand = null, PendingRequest = null, StorageRevision = context.Revision
    };
}

internal sealed class PostgresAgentPreferences(AccountAgentMemory memory, LibraryActor actor) : IPreparationPreferences
{
    public PreparationPreferences Get() => memory.InScope(actor, null, database =>
    {
        var stored = database.AgentPreferences.AsNoTracking().SingleOrDefault(x => x.OwnerId == actor.Id);
        return stored is null ? new() : new PreparationPreferences(stored.Language, stored.Format, stored.ProviderId);
    });
    public void Save(PreparationPreferences preferences)
    {
        PreparationPreferenceRules.Validate(preferences);
        memory.InScope(actor, null, database =>
        {
            database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5282, hashtext({actor.Id.ToString()}))");
            var stored = database.AgentPreferences.SingleOrDefault(x => x.OwnerId == actor.Id) ?? new() { OwnerId = actor.Id };
            if (database.Entry(stored).State == EntityState.Detached) database.AgentPreferences.Add(stored);
            stored.Language = preferences.Language; stored.Format = preferences.Format; stored.ProviderId = preferences.ProviderId;
            return database.SaveChanges();
        });
    }
    public PreparationPreferences Confirm(PreferenceApproval input)
    {
        if (!input.Confirmed || input.Preferences is null) throw new ValidationException("Confirma las preferencias que quieres conservar.");
        Save(input.Preferences); return Get();
    }
    public void Forget() => memory.InScope(actor, null, database =>
    {
        database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5282, hashtext({actor.Id.ToString()}))");
        return database.AgentPreferences.Where(x => x.OwnerId == actor.Id).ExecuteDelete();
    });
}
