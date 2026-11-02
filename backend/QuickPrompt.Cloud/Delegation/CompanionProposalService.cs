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
        Func<LibraryActor, Guid, Func<CancellationToken, Task<bool>>, Task<T>> operation, CancellationToken cancellation, bool? reasoningAssertion = null)
    {
        async Task<bool> Check(CancellationToken token)
        {
            var active = await access.ProposalActive(principal, token);
            return active is not null && (reasoningAssertion is null || (active.ProfileId == PairedReasoningProtocol.GrantProfile) == reasoningAssertion);
        }
        var connection = await access.ProposalActive(principal, cancellation) ?? throw new UnauthorizedAccessException();
        if (reasoningAssertion is { } asserted && (connection.ProfileId == PairedReasoningProtocol.GrantProfile) != asserted)
            throw new QuickPrompt.Local.ValidationException("El perfil vinculado y el permiso local de razonamiento no coinciden.");
        return await operation(LibraryActor.FromPrincipal(principal), connection.Id, Check);
    }
    public Task<LocalProposalLease?> Claim(ClaimsPrincipal principal, CancellationToken cancellation, Action<CompanionObservation>? observed = null, bool reasoningAssertion = false) =>
        Authorized(principal, (actor, connection, check) => queue.ClaimProposal(actor, connection, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, connection, working, stop); observed?.Invoke(signal); }, acceptsThinking: reasoningAssertion), cancellation, reasoningAssertion);
    public Task<LeasePulse> Heartbeat(ClaimsPrincipal principal, Guid id, Guid token, CancellationToken cancellation, Action<CompanionObservation>? observed = null, bool reasoningAssertion = false) =>
        Authorized(principal, (actor, connection, check) => queue.HeartbeatProposal(actor, connection, id, token, check, cancellation,
            async (working, stop) => { var signal = await access.ObserveChecked(actor, connection, working, stop); observed?.Invoke(signal); }), cancellation, reasoningAssertion);
    public Task<bool> Finish(ClaimsPrincipal principal, Guid id, LocalProposalReceipt receipt, CancellationToken cancellation) =>
        Authorized(principal, (actor, connection, check) => queue.FinishProposal(actor, connection, id, receipt, check, cancellation), cancellation);
}
