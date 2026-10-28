using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;

namespace QuickPrompt.Accounts.Executions;

/// <summary>
/// Owns admissions, short-lived leases and atomic results. All instances coordinate in PostgreSQL;
/// the lock is released before processing. A lost lease is interrupted, never automatically retried.
/// </summary>
public sealed partial class ExecutionQueue(ExecutionDataSource source, ExecutionLimits limits)
{
    public ExecutionLimits Limits { get; } = Validated(limits);
    /// <summary>Order consent/stamp changes with claims and result commits. Call before opening an account transaction.</summary>
    public Task<T> Fence<T>(Func<Task<T>> action, CancellationToken cancellation = default) => Change((_, _) => action(), cancellation);
    private static ExecutionLimits Validated(ExecutionLimits value) { value.Validate(); return value; }
    private AccountDatabase Database() => new(new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(source.Data,
        postgres => postgres.CommandTimeout(10).MigrationsHistoryTable("history", "qp_migrations")).Options);
    private static Task<DateTimeOffset> Clock(AccountDatabase database, CancellationToken cancellation) =>
        database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellation);

    private async Task<T> Change<T>(Func<AccountDatabase, DateTimeOffset, Task<T>> action, CancellationToken cancellation)
    {
        await using var database = Database();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellation);
        // Admission counts and lease allocation must agree across processes, not just within one host.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(5283, 0)", cancellation);
        var now = await Clock(database, cancellation);
        var result = await action(database, now);
        await database.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return result;
    }

    public Task<ExecutionView> Admit(LibraryActor actor, Guid libraryId, Guid operationId, string requestHash, string payload,
        CancellationToken cancellation = default, Guid? connectionId = null, Func<CancellationToken, Task<bool>>? authorize = null) =>
        AdmitInside(actor, libraryId, operationId, requestHash, payload, connectionId is null ? "CompilePrompt" : "RunLocalPrompt", connectionId, authorize, cancellation);

    internal Task<ExecutionView> AdmitProposal(LibraryActor actor, Guid libraryId, Guid operationId, string requestHash, string payload,
        Guid connection, Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation) =>
        AdmitInside(actor, libraryId, operationId, requestHash, payload, "ModelProposal", connection, authorize, cancellation);

    private Task<ExecutionView> AdmitInside(LibraryActor actor, Guid libraryId, Guid operationId, string requestHash, string payload,
        string kind, Guid? connectionId, Func<CancellationToken, Task<bool>>? authorize, CancellationToken cancellation) => Change(async (database, now) =>
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('quickprompt.actor_id', {actor.Id.ToString()}, true)", cancellation);
        // A replay is still bound to a live grant. Revocation must not be bypassed by an old operation ID.
        if (connectionId is not null && (authorize is null || !await authorize(cancellation))) throw new LibraryUnavailableException();
        var previous = await database.Executions.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == actor.Id && x.OperationId == operationId, cancellation);
        if (previous is not null)
        {
            if (previous.LibraryId != libraryId || previous.RequestHash != requestHash || previous.Kind != kind || previous.ConnectionId != connectionId) throw new ConflictException("La operación ya pertenece a otra solicitud.");
            return View(previous);
        }
        if (connectionId is not null) now = await Clock(database, cancellation);
        var pending = database.Executions.Where(x => x.Status == ExecutionStatus.Queued);
        if (await pending.CountAsync(x => x.OwnerId == actor.Id, cancellation) >= Limits.OwnerQueued)
            throw new ExecutionQuotaException("OWNER_QUEUE_FULL");
        if (await pending.CountAsync(cancellation) >= Limits.GlobalQueued) throw new ExecutionQuotaException("QUEUE_FULL");
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (await database.Executions.CountAsync(x => x.OwnerId == actor.Id && x.CreatedAt >= day, cancellation) >= Limits.DailyAdmissions)
            throw new ExecutionQuotaException("DAILY_LIMIT");
        var job = new AccountExecution
        {
            Id = Guid.NewGuid(),
            OwnerId = actor.Id,
            LibraryId = libraryId,
            OperationId = operationId,
            Payload = payload,
            RequestHash = requestHash,
            CreatedAt = now,
            Status = ExecutionStatus.Queued,
            ConnectionId = connectionId,
            Kind = kind
        };
        database.Executions.Add(job);
        return View(job);
    }, cancellation);

    /// <summary>Claims one eligible job. Opaque lease capability stays inside the trusted dispatcher.</summary>
    public Task<ExecutionLease?> Claim(CancellationToken cancellation = default) =>
        Change((database, now) => ClaimInside(database, now, null, null, "CompilePrompt", cancellation), cancellation);

    private async Task<ExecutionLease?> ClaimInside(AccountDatabase database, DateTimeOffset now, Guid? connectionId, Guid? ownerId, string kind, CancellationToken cancellation)
    {
        // Fixed server policy: at most 100 terminal payloads per dispatch pass, never active work.
        // The database capability erases content without granting the worker general UPDATE/DELETE.
        await database.Database.ExecuteSqlRawAsync("SELECT qp_operations.expire_execution_content()", cancellation);
        await database.Executions.Where(x => x.Status == ExecutionStatus.Running && x.LeaseUntil <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, x => x.CancelRequested ? ExecutionStatus.Cancelled : ExecutionStatus.Interrupted)
                .SetProperty(x => x.FinishedAt, now).SetProperty(x => x.ErrorCode, x => x.CancelRequested ? null : "LEASE_EXPIRED")
                .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), cancellation);
        await database.Executions.Where(x => x.Status == ExecutionStatus.Queued && x.CancelRequested)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, ExecutionStatus.Cancelled).SetProperty(x => x.FinishedAt, now), cancellation);
        var oldestAllowed = now.AddSeconds(-Limits.QueueLifetimeSeconds);
        await database.Executions.Where(x => x.Status == ExecutionStatus.Queued && x.CreatedAt <= oldestAllowed)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, ExecutionStatus.Interrupted).SetProperty(x => x.FinishedAt, now)
                .SetProperty(x => x.ErrorCode, "QUEUE_EXPIRED"), cancellation);
        if (await database.Executions.CountAsync(x => x.Status == ExecutionStatus.Running, cancellation) >= Limits.GlobalRunning) return null;
        var eligible = database.Executions.Where(x => x.Status == ExecutionStatus.Queued && !x.CancelRequested
            && x.ConnectionId == connectionId && x.Kind == kind && (ownerId == null || x.OwnerId == ownerId)
            && database.Executions.Count(r => r.OwnerId == x.OwnerId && r.Status == ExecutionStatus.Running) < Limits.OwnerRunning);
        // Prefer an owner who has not recently held a turn, then FIFO within that owner's queue.
        var job = await eligible.OrderBy(x => database.Executions.Where(r => r.OwnerId == x.OwnerId).Max(r => r.StartedAt) ?? DateTimeOffset.UnixEpoch)
            .ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellation);
        if (job is null) return null;
        job.Status = ExecutionStatus.Running; job.StartedAt = now; job.Attempts = 1;
        job.LeaseToken = Guid.NewGuid(); job.LeaseUntil = now.AddSeconds(Limits.LeaseSeconds);
        return new(job.Id, job.OwnerId, job.LibraryId, job.LeaseToken.Value, job.Kind, job.Payload, job.ConnectionId);
    }

    public Task<LeasePulse> Heartbeat(ExecutionLease lease, CancellationToken cancellation = default) => Change(async (database, _) =>
    {
        if (lease.ConnectionId is not null) throw new ValidationException("El turno local requiere su conexión autorizada.");
        var (job, checkedAt) = await Leased(database, lease, cancellation);
        if (job is null) return new LeasePulse(false, false);
        job.LeaseUntil = checkedAt.AddSeconds(Limits.LeaseSeconds);
        return new LeasePulse(true, job.CancelRequested);
    }, cancellation);

    /// <summary>Cancellation wins if committed before this row lock. Output and terminal state commit together.</summary>
    public Task<bool> Finish(ExecutionLease lease, ExecutionOutput? output, string? errorCode = null, bool interrupted = false,
        CancellationToken cancellation = default) => Change(async (database, _) =>
    {
        if (lease.ConnectionId is not null) throw new ValidationException("El resultado local requiere su conexión autorizada.");
        var (job, completedAt) = await Leased(database, lease, cancellation);
        if (job is null) return false;
        return await Complete(database, job, lease, completedAt, output, errorCode, interrupted, cancellation);
    }, cancellation);

    private static async Task<bool> Complete(AccountDatabase database, AccountExecution job, ExecutionLease lease, DateTimeOffset completedAt,
        ExecutionOutput? output, string? errorCode, bool interrupted, CancellationToken cancellation)
    {
        if (job.CancelRequested) job.Status = ExecutionStatus.Cancelled;
        else if (interrupted) { job.Status = ExecutionStatus.Interrupted; job.ErrorCode = "HOST_STOPPED"; }
        else if (errorCode is not null) { job.Status = ExecutionStatus.Failed; job.ErrorCode = errorCode; }
        else
        {
            if (output is null || output.Text.Length > 64000) throw new ValidationException("Falta un resultado válido.");
            // No model or extension may replace the compiler's literal result for this capability.
            var expected = await new CompileExecutionHandler().Execute(new(job.Id, job.OwnerId, job.LibraryId, lease.Token, "CompilePrompt", job.Payload), cancellation);
            if (expected != output with { Response = null, Model = null }
                || (job.ConnectionId is null && (output.Response is not null || output.Model is not null))
                || (job.ConnectionId is not null && (string.IsNullOrWhiteSpace(output.Response) || output.Response.Length > 64000
                    || output.Model != QuickPrompt.Accounts.Delegation.DelegationPolicy.Model))) throw new ValidationException("El resultado no corresponde a la solicitud.");
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('quickprompt.actor_id', {job.OwnerId.ToString()}, true)", cancellation);
            database.ExecutionResults.Add(new()
            {
                ExecutionId = job.Id,
                OwnerId = job.OwnerId,
                LibraryId = job.LibraryId,
                PromptId = output.PromptId,
                PromptRevision = output.Revision,
                Title = output.Title,
                Text = output.Text,
                CreatedAt = completedAt,
                Response = output.Response,
                Model = output.Model
            });
            job.Status = ExecutionStatus.Completed;
        }
        job.FinishedAt = completedAt; job.LeaseToken = null; job.LeaseUntil = null;
        return true;
    }

    private static async Task<(AccountExecution? Job, DateTimeOffset CheckedAt)> Leased(AccountDatabase database, ExecutionLease lease, CancellationToken cancellation)
    {
        // Lock the row as well as the dispatcher: cancellation uses the restricted account connection.
        var job = await database.Executions.FromSqlInterpolated($"SELECT * FROM qp_operations.executions WHERE \"Id\"={lease.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellation);
        // The row lock can wait behind an account cancellation/transaction. A clock captured
        // before that wait (including transaction_timestamp/now()) cannot authorize this commit.
        var checkedAt = await Clock(database, cancellation);
        var owned = job is { Status: ExecutionStatus.Running } && job.OwnerId == lease.OwnerId && job.LibraryId == lease.LibraryId
            && job.ConnectionId == lease.ConnectionId
            && job.LeaseToken == lease.Token && job.LeaseUntil > checkedAt;
        return (owned ? job : null, checkedAt);
    }

    internal static ExecutionView View(AccountExecution job) => new(job.Id, job.LibraryId, job.Kind, job.Status, job.CancelRequested,
        job.CreatedAt, job.StartedAt, job.FinishedAt, job.ErrorCode, job.ForgottenAt);
}
