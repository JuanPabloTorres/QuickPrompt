using System.Security.Claims;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Nova.Application;
using QuickPrompt.Accounts.Delegation;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Only the proposal profile can access this queue lane. The principal supplies owner and connection.</summary>
public sealed class CompanionProposalService(DelegationBoundary access, ExecutionQueue queue)
{
    private async Task<T> Authorized<T>(ClaimsPrincipal principal,
        Func<LibraryActor, Guid, Func<CancellationToken, Task<bool>>, Task<T>> operation, CancellationToken cancellation)
    {
        async Task<bool> Check(CancellationToken token) => await access.ProposalActive(principal, token) is not null;
        var connection = await access.ProposalActive(principal, cancellation) ?? throw new UnauthorizedAccessException();
        return await operation(LibraryActor.FromPrincipal(principal), connection.Id, Check);
    }
    public Task<LocalProposalLease?> Claim(ClaimsPrincipal principal, CancellationToken cancellation, Action<CompanionObservation>? observed = null) =>
        Authorized(principal, (actor, connection, check) => queue.ClaimProposal(actor, connection, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, connection, working, stop); observed?.Invoke(signal); }), cancellation);
    public Task<LeasePulse> Heartbeat(ClaimsPrincipal principal, Guid id, Guid token, CancellationToken cancellation, Action<CompanionObservation>? observed = null) =>
        Authorized(principal, (actor, connection, check) => queue.HeartbeatProposal(actor, connection, id, token, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, connection, working, stop); observed?.Invoke(signal); }), cancellation);
    public Task<bool> Finish(ClaimsPrincipal principal, Guid id, LocalProposalReceipt receipt, CancellationToken cancellation) =>
        Authorized(principal, (actor, connection, check) => queue.FinishProposal(actor, connection, id, receipt, check, cancellation), cancellation);
}
