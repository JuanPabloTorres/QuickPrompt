using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Active conversation store with atomic transitions, two-hour expiry and a separate owner budget.</summary>
internal sealed class AccountActiveSessions(AccountAgentMemory memory, LibraryActor actor, Guid libraryId) : IAgentSessionStore
{
    private static DateTimeOffset Clock(AccountDatabase database) => database.Database
        .SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").Single();
    private static void Prune(AccountDatabase database, LibraryActor actor, DateTimeOffset now) =>
        database.ActiveSessions.Where(x => x.OwnerId == actor.Id && x.ExpiresAt <= now).ExecuteDelete();

    public AgentSession? Get(Guid id) => memory.InScope(actor, libraryId, database =>
    {
        Prune(database, actor, Clock(database));
        var stored = database.ActiveSessions.AsNoTracking().SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id);
        return stored is null ? null : Read(stored);
    });
    public void Put(AgentSession session) => Update(session.Id, _ => session);

    public AgentSession Update(Guid id, Func<AgentSession?, AgentSession> transition)
    {
        if (id == Guid.Empty) throw new ValidationException("Sesión inválida.");
        return memory.InScope(actor, libraryId, database =>
        {
            // The owner lock also serializes trimming across libraries and host instances.
            // No lock or DbContext survives a model await. Update consumes the current row;
            // Put retains the existing replacement semantics of IAgentSessionStore.
            database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5287, hashtext({actor.Id.ToString()}))");
            var now = Clock(database); Prune(database, actor, now);
            var stored = database.ActiveSessions.SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id);
            var next = transition(stored is null ? null : Read(stored)) with {
                Approval = "NOT_GRANTED", UpdatedAt = now.UtcDateTime, StorageRevision = checked((stored?.Revision ?? 0) + 1)
            };
            var payload = AccountSessionData.Encode(database, libraryId, id, next);
            stored ??= new() { OwnerId = actor.Id, LibraryId = libraryId, SessionId = id };
            if (database.Entry(stored).State == EntityState.Detached) database.ActiveSessions.Add(stored);
            stored.Revision = next.StorageRevision; stored.Payload = payload; stored.UpdatedAt = now; stored.ExpiresAt = now.AddHours(2);
            database.SaveChanges();
            var excess = database.ActiveSessions.Where(x => x.OwnerId == actor.Id).OrderByDescending(x => x.UpdatedAt)
                .ThenBy(x => x.LibraryId).ThenBy(x => x.SessionId).Skip(100).ToArray();
            database.ActiveSessions.RemoveRange(excess); database.SaveChanges();
            return next;
        });
    }
    public void Forget(Guid id) => memory.InScope(actor, libraryId, database =>
    {
        database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5287, hashtext({actor.Id.ToString()}))");
        return database.ActiveSessions.Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.SessionId == id).ExecuteDelete();
    });
    private static AgentSession Read(ActiveAgentSession row) => AccountSessionData.Decode(row.Payload, row.SessionId) with {
        Approval = "NOT_GRANTED", StorageRevision = row.Revision
    };
}
