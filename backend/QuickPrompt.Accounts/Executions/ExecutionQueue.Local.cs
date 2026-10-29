using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

public sealed partial class ExecutionQueue
{
    public Task<bool> RevokeConnection(LibraryActor actor, Guid connection, Func<Task<bool>> revoke, CancellationToken cancellation) =>
        Change(async (database, now) =>
        {
            var changed = await revoke();
            await CancelLocal(database.Executions.Where(x => x.OwnerId == actor.Id && x.ConnectionId == connection), now, "CONNECTION_REVOKED", cancellation);
            return changed;
        }, cancellation);

    public Task<bool> InvalidateLocalSession(Guid owner, Func<Task<bool>> invalidate, CancellationToken cancellation = default) =>
        Change(async (database, now) =>
        {
            var changed = await invalidate();
            if (changed) await CancelLocal(database.Executions.Where(x => x.OwnerId == owner && x.ConnectionId != null), now, "SESSION_REVOKED", cancellation);
            return changed;
        }, cancellation);

    private static Task<int> CancelLocal(IQueryable<AccountExecution> jobs, DateTimeOffset now, string code, CancellationToken cancellation) =>
        jobs.Where(x => x.Status == ExecutionStatus.Queued || x.Status == ExecutionStatus.Running)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, ExecutionStatus.Cancelled).SetProperty(x => x.FinishedAt, now)
                .SetProperty(x => x.ErrorCode, code).SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), cancellation);
    /// <summary>All authorization callbacks run inside the queue fence, outside any model call.</summary>
    public Task<LocalWorkLease?> ClaimLocal(LibraryActor actor, Guid connection, Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation,
        Func<bool, CancellationToken, Task>? observe = null) =>
        Change<LocalWorkLease?>(async (database, now) =>
        {
            if (!await authorize(cancellation)) throw new UnauthorizedAccessException();
            now = await Clock(database, cancellation);
            var lease = await ClaimInside(database, now, connection, actor.Id, "RunLocalPrompt", cancellation);
            if (observe is not null) await observe(lease is not null, cancellation);
            if (lease is null) return null;
            var compiled = await new CompileExecutionHandler().Execute(lease with { Kind = "CompilePrompt" }, cancellation);
            return new(lease.Id, lease.Token, compiled.Text, DelegationPolicy.Model, Limits.LeaseSeconds, Limits.MaximumSeconds);
        }, cancellation);

    public Task<LeasePulse> HeartbeatLocal(LibraryActor actor, Guid connection, Guid id, Guid token,
        Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation, Func<bool, CancellationToken, Task>? observe = null) =>
        PulseLocal(actor, connection, id, token, "RunLocalPrompt", authorize, cancellation, observe);

    private Task<LeasePulse> PulseLocal(LibraryActor actor, Guid connection, Guid id, Guid token, string kind,
        Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation, Func<bool, CancellationToken, Task>? observe = null) => Change(async (database, _) =>
    {
        var lease = await LocalLease(database, actor, connection, id, token, cancellation, kind);
        var (job, checkedAt) = await Leased(database, lease, cancellation);
        if (!await authorize(cancellation)) throw new UnauthorizedAccessException();
        if (job is null) return new LeasePulse(false, false);
        checkedAt = await Clock(database, cancellation);
        if (job.LeaseUntil <= checkedAt) return new LeasePulse(false, true);
        if (checkedAt - job.StartedAt >= TimeSpan.FromSeconds(Limits.MaximumSeconds)) return new LeasePulse(false, true);
        if (observe is not null) await observe(true, cancellation);
        job.LeaseUntil = checkedAt.AddSeconds(Limits.LeaseSeconds);
        return new LeasePulse(true, job.CancelRequested);
    }, cancellation);

    public Task<bool> FinishLocal(LibraryActor actor, Guid connection, Guid id, LocalWorkReceipt receipt,
        Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation) => Change(async (database, _) =>
    {
        if (receipt.Response is { Length: > 64000 } || (receipt.ErrorCode is not null && (receipt.Response is not null
            || receipt.ErrorCode is not ("MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_FAILED" or "LOCAL_STOPPED"))))
            throw new ValidationException("Resultado local inválido.");
        var lease = await LocalLease(database, actor, connection, id, receipt.Token, cancellation);
        var (job, completedAt) = await Leased(database, lease, cancellation);
        if (!await authorize(cancellation)) throw new UnauthorizedAccessException();
        completedAt = await Clock(database, cancellation);
        if (job is not null && job.LeaseUntil <= completedAt) return false;
        if (job is null || completedAt - job.StartedAt >= TimeSpan.FromSeconds(Limits.MaximumSeconds)) return false;
        var compiled = await new CompileExecutionHandler().Execute(lease with { Kind = "CompilePrompt" }, cancellation);
        var output = compiled with { Response = receipt.Response, Model = DelegationPolicy.Model };
        return await Complete(database, job, lease, completedAt, output, receipt.ErrorCode, receipt.ErrorCode == "LOCAL_STOPPED", cancellation);
    }, cancellation);

    private static async Task<ExecutionLease> LocalLease(AccountDatabase database, LibraryActor actor, Guid connection, Guid id, Guid token, CancellationToken cancellation, string kind = "RunLocalPrompt")
    {
        var job = await database.Executions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor.Id
            && x.ConnectionId == connection && x.Kind == kind, cancellation) ?? throw new LibraryUnavailableException();
        return new(job.Id, job.OwnerId, job.LibraryId, token, job.Kind, job.Payload, connection);
    }
}
