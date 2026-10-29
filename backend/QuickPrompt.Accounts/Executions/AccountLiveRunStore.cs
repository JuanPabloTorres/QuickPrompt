using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

/// <summary>
/// Durable visible-run metadata, separate from child inference slots. A lost host is interrupted;
/// approvals are not restored or replayed. All mutable operations serialize with revocation.
/// </summary>
public sealed partial class AccountLiveRunStore(
    AccountAgentMemory memory, ExecutionQueue queue, IProposalConnectionAuthority connections,
    LibraryActor actor, Guid libraryId, Guid connectionId, Guid hostToken) : ILiveRunCommitStore
{
    private const int LeaseSeconds = 30;
    private const int LifetimeMinutes = 15;
    private const int DailyAdmissions = 100;
    private const int MaximumEvents = 1000;
    public Guid ConnectionId => connectionId;

    /// <summary>Reopen stored identity for metadata/recovery; a new host token cannot resume old controls.</summary>
    public static AccountLiveRunStore Reopen(AccountAgentMemory memory, ExecutionQueue queue, IProposalConnectionAuthority connections,
        LibraryActor actor, Guid libraryId, Guid runId, Guid hostToken)
    {
        var connection = memory.InScope(actor, libraryId, database => database.LiveRuns.AsNoTracking()
            .Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.RunId == runId).Select(x => (Guid?)x.ConnectionId).SingleOrDefault())
            ?? throw new MissingPromptException();
        return new(memory, queue, connections, actor, libraryId, connection, hostToken);
    }

    private T Read<T>(Func<AccountDatabase, DateTimeOffset, T> operation) => memory.InScope(actor, libraryId, database =>
    {
        database.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(5292, hashtext({actor.Id.ToString()}))");
        database.Database.ExecuteSqlRaw("SELECT qp_operations.expire_agent_event_content()");
        var now = database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").Single();
        // Recovery is explicit metadata, never reconstruction of a browser or a pending effect.
        foreach (var expired in database.LiveRuns.Where(x => x.OwnerId == actor.Id && !x.Terminal && x.LeaseUntil <= now).ToArray())
            Interrupt(database, expired, now, "HOST_LEASE_EXPIRED");
        database.SaveChanges();
        return operation(database, now);
    });

    private T Change<T>(Func<AccountDatabase, DateTimeOffset, T> operation, bool requireConnection = true) =>
        queue.Fence(async () =>
        {
            if (libraryId == Guid.Empty || connectionId == Guid.Empty || hostToken == Guid.Empty)
                throw new ValidationException("Falta el contexto autorizado de NOVA.");
            // Validate before borrowing the actor's one-connection memory pool.
            if (requireConnection && !await connections.ProposalOwned(actor, connectionId, CancellationToken.None))
                throw new LibraryUnavailableException();
            return Read(operation);
        }).GetAwaiter().GetResult();

    public bool TryCreate(LiveRecord record) => Change((database, now) =>
    {
        if (record.RunId == Guid.Empty || record.SessionId == Guid.Empty || record.Sequence != 0 || record.Status != LiveStatus.CREATED
            || record.Approval != "NONE" || record.EffectCommitted || record.Fingerprint.Length != 64 || record.Mode != "SAME_TAB")
            throw new ValidationException("Inicio visible inválido.");
        if (database.LiveRuns.Any(x => x.OwnerId == actor.Id && x.RunId == record.RunId)) return false;
        if (database.LiveRuns.Any(x => x.OwnerId == actor.Id && !x.Terminal)) throw new ConflictException("Esta cuenta ya tiene una ejecución activa.");
        CompanionAvailability.Require(database, actor.Id, connectionId, now);
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (database.LiveRuns.Count(x => x.OwnerId == actor.Id && x.CreatedAt >= day) >= DailyAdmissions)
            throw new ExecutionQuotaException("DAILY_AGENT_LIMIT");
        database.LiveRuns.Add(new()
        {
            OwnerId = actor.Id,
            LibraryId = libraryId,
            ConnectionId = connectionId,
            RunId = record.RunId,
            HostToken = hostToken,
            Record = JsonSerializer.Serialize(record),
            CreatedAt = now,
            LeaseUntil = now.AddSeconds(LeaseSeconds)
        });
        database.SaveChanges();
        return true;
    });

    public LiveRecord? Get(Guid id) => Read((database, _) =>
        database.LiveRuns.AsNoTracking().SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.RunId == id) is { } row ? Decode(row) : null);

    public LiveEvent[] Events(Guid id, long after) => Read((database, _) =>
    {
        if (after < 0) throw new ValidationException("Secuencia inválida.");
        if (!database.LiveRuns.Any(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.RunId == id)) throw new MissingPromptException();
        return database.LiveEvents.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.RunId == id && x.Sequence > after)
            .OrderBy(x => x.Sequence).Take(MaximumEvents).AsEnumerable().Select(x => JsonSerializer.Deserialize<LiveEvent>(x.Payload)!).ToArray();
    });

    public LiveEvent Append(LiveRecord record, LiveEvent item) => Append(record, item, null);

    public LiveEvent Append(LiveRecord record, LiveEvent item, LiveApproval? proposal) => Change((database, now) =>
    {
        var row = Owned(database, record.RunId, now);
        var prior = Decode(row);
        var terminalReceipt = row.Terminal && record.Status == prior.Status && item.Stage == "UiReceipt" && item.Status == LiveStatus.UI_UPDATED;
        var terminalClose = row.Terminal && item.Stage == "Browser" && !record.BrowserOpen
            && (record with { BrowserOpen = prior.BrowserOpen, UpdatedAt = prior.UpdatedAt }) == prior;
        if (row.Terminal && !terminalReceipt && !terminalClose || row.Sequence != record.Sequence || item.RunId != row.RunId
            || record.SessionId != prior.SessionId || record.Fingerprint != prior.Fingerprint || record.Mode != prior.Mode || record.Model != prior.Model
            || item.SafeDisplayMessage.Length > 2000 || row.Sequence >= MaximumEvents - 1)
            throw new ConflictException("El evento no corresponde al turno vigente.");
        if (record.Approval == "PENDING")
        {
            if (proposal is null || proposal.Hash.Length != 64) throw new ValidationException("Falta la propuesta exacta.");
            row.ApprovalId = proposal.Id; row.ApprovalHash = proposal.Hash; row.DraftHash = LiveRuns.Hash(proposal.Draft);
        }
        else if (record.Approval == "APPROVED" && (proposal is null || row.ApprovalId != proposal.Id
            || row.ApprovalHash != proposal.Hash || row.DraftHash != LiveRuns.Hash(proposal.Draft)))
            throw new ConflictException("La aprobación no corresponde al contenido propuesto.");
        if (record.EffectCommitted != prior.EffectCommitted || record.Approval == "CONSUMED" && row.CommittedPromptId is null)
            throw new ConflictException("Los efectos requieren el guardado atómico.");
        if (row.CommittedPromptId is not null && record.Approval != "CONSUMED") throw new ConflictException("La aprobación ya fue consumida.");
        return AppendInside(database, row, record, item, now);
    }, requireConnection: !LiveRuns.Terminal(record.Status));

    /// <summary>Host-only pulse. Does not allocate an inference slot or extend the total run budget.</summary>
    public bool Heartbeat(Guid id) => Change((database, now) =>
    {
        var row = Owned(database, id, now);
        if (row.Terminal) return false;
        row.LeaseUntil = new[] { now.AddSeconds(LeaseSeconds), row.CreatedAt.AddMinutes(LifetimeMinutes) }.Min();
        database.SaveChanges(); return true;
    });

    /// <summary>Owner cancellation works across hosts and after connection revocation.</summary>
    public void Cancel(Guid id) => Change((database, now) =>
    {
        var row = database.LiveRuns.SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.RunId == id) ?? throw new MissingPromptException();
        if (!row.Terminal)
        {
            var prior = Decode(row);
            AppendInside(database, row, prior with { Status = LiveStatus.CANCELLED, PauseRequested = false },
                new(row.RunId, Guid.NewGuid(), 0, now.UtcDateTime, "Execution", LiveActor.USER_SELECTED, null, null,
                    LiveStatus.CANCELLED, null, "USER_CANCELLED", "Detenido por el propietario; no se repetirán acciones."), now);
        }
        return true;
    }, requireConnection: false);

    private AccountLiveRun Owned(AccountDatabase database, Guid id, DateTimeOffset now)
    {
        var row = database.LiveRuns.SingleOrDefault(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.RunId == id) ?? throw new MissingPromptException();
        if (row.HostToken != hostToken || row.ConnectionId != connectionId || row.LeaseUntil <= now && !row.Terminal)
            throw new ConflictException("El turno del agente ya no pertenece a este proceso.");
        return row;
    }

    private static LiveRecord Decode(AccountLiveRun row) => JsonSerializer.Deserialize<LiveRecord>(row.Record)!;

    private static LiveEvent AppendInside(AccountDatabase database, AccountLiveRun row, LiveRecord record, LiveEvent item, DateTimeOffset now)
    {
        var next = checked(row.Sequence + 1);
        var entry = item with { Sequence = next, Timestamp = now.UtcDateTime };
        row.Record = JsonSerializer.Serialize(record with { Sequence = next, UpdatedAt = now.UtcDateTime });
        row.Sequence = next; row.Terminal = LiveRuns.Terminal(record.Status);
        database.LiveEvents.Add(new() { OwnerId = row.OwnerId, RunId = row.RunId, Sequence = next, Payload = JsonSerializer.Serialize(entry) });
        database.SaveChanges(); return entry;
    }

    private static void Interrupt(AccountDatabase database, AccountLiveRun row, DateTimeOffset now, string reason)
    {
        var record = Decode(row) with { Status = LiveStatus.FAILED, ErrorCode = reason, PauseRequested = false };
        AppendInside(database, row, record, new(row.RunId, Guid.NewGuid(), 0, now.UtcDateTime, "Recovery", LiveActor.SYSTEM_VALIDATION,
            null, null, LiveStatus.FAILED, null, reason, "El turno del proceso expiró. Se conserva el estado y no se repetirán efectos."), now);
    }
}
