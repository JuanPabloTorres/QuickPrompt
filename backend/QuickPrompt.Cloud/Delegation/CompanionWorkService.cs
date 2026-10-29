using System.Security.Claims;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Maps a validated delegated principal to its own queue capability. Never accepts owner/destination arguments.</summary>
public sealed class CompanionWorkService(DelegationBoundary access, DelegationOrigin origin, ExecutionQueue queue,
    AccountExecutionAccess executions, LibraryAccess libraries, ILogger<CompanionWorkService> logger)
{
    private async Task<T> Authorized<T>(ClaimsPrincipal principal,
        Func<LibraryActor, Guid, Func<CancellationToken, Task<bool>>, Task<T>> operation, CancellationToken cancellation)
    {
        var connection = await access.Active(principal, origin.Resource, cancellation) ?? throw new UnauthorizedAccessException();
        var actor = LibraryActor.FromPrincipal(principal);
        return await operation(actor, connection.Id, async token => await access.Active(principal, origin.Resource, token) is not null);
    }
    public Task<LocalWorkLease?> Claim(ClaimsPrincipal principal, CancellationToken cancellation, Action<CompanionObservation>? observed = null) =>
        Authorized(principal, (actor, id, check) => queue.ClaimLocal(actor, id, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, id, working, stop); observed?.Invoke(signal); }), cancellation);
    public Task<LeasePulse> Heartbeat(ClaimsPrincipal principal, Guid id, Guid token, CancellationToken cancellation, Action<CompanionObservation>? observed = null) =>
        Authorized(principal, (actor, connection, check) => queue.HeartbeatLocal(actor, connection, id, token, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, connection, working, stop); observed?.Invoke(signal); }), cancellation);
    public Task<bool> Finish(ClaimsPrincipal principal, Guid id, LocalWorkReceipt receipt, CancellationToken cancellation) =>
        Authorized(principal, async (actor, connection, check) =>
        {
            var accepted = await queue.FinishLocal(actor, connection, id, receipt, check, cancellation);
            if (accepted && receipt.ErrorCode is null)
            {
                // Statistics are secondary to the committed execution result. Do not
                // lose a genuine result or repeat inference because registration failed.
                try
                {
                    var use = executions.CompletedLocalUse(actor, connection, id);
                    if (use is not null) await libraries.RecordUse(actor, use.LibraryId, use.PromptId, use.Input, cancellation);
                }
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    logger.LogWarning(5310, "Completed local execution usage was not recorded: {FailureType}", failure.GetType().Name);
                }
            }
            return accepted;
        }, cancellation);
    public async Task<bool> Disconnect(ClaimsPrincipal principal, CancellationToken cancellation)
    {
        var connection = await access.CompanionActive(principal, cancellation) ?? throw new UnauthorizedAccessException();
        return await access.Revoke(LibraryActor.FromPrincipal(principal), connection.Id, cancellation);
    }
}
