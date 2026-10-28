using System.Security.Claims;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Nova.Application;
using OpenIddict.Abstractions;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>
/// Keeps actor transactions (which clear their change tracker) independent from
/// the protocol's request unit of work. Never clears OpenIddict's pending token entries.
/// Only validated principals and curated domain results cross this boundary.
/// </summary>
public sealed class DelegationBoundary(IServiceScopeFactory scopes, ExecutionQueue queue, DelegationOrigin origin) : IExecutionConnectionAuthority, IProposalConnectionAuthority
{
    private async Task<T> Run<T>(Func<ConnectionAccess, Task<T>> operation)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ConnectionAccess>());
    }
    public Task<ConnectionView[]> List(LibraryActor actor, CancellationToken cancellation) =>
        Run(access => access.List(actor, cancellation));
    public Task<ClaimsPrincipal> Approve(LibraryActor actor, ClaimsPrincipal pending, string resource, CancellationToken cancellation) =>
        Run(access => access.Approve(actor, pending, resource, cancellation));
    public Task<DelegatedConnection?> Active(ClaimsPrincipal principal, string resource, CancellationToken cancellation) =>
        Run(access => access.Active(principal, resource, cancellation));
    public Task<DelegatedConnection?> McpActive(ClaimsPrincipal principal, CancellationToken cancellation) =>
        Run(access => access.Active(principal, origin.McpResource, cancellation, McpDelegationPolicy.ReadScope));
    public Task<DelegatedConnection?> ProposalActive(ClaimsPrincipal principal, CancellationToken cancellation) =>
        Run(access => access.Active(principal, origin.ProposalResource, cancellation, LocalProposalProtocol.Scope));
    public Task<DelegatedConnection?> CompanionActive(ClaimsPrincipal principal, CancellationToken cancellation)
    {
        var profile = principal.GetClaim(DelegationPolicy.ProfileClaim);
        return LocalCompanionProfiles.Known(profile)
            ? Run(access => access.Active(principal, origin.ForProfile(profile!), cancellation, LocalCompanionProfiles.Scope(profile!)))
            : Task.FromResult<DelegatedConnection?>(null);
    }
    public Task<ClaimsPrincipal> ApproveMcp(LibraryActor actor, Guid libraryId, bool offline, CancellationToken cancellation, bool allowImport = false, bool allowCreate = false, bool allowCollections = false) =>
        Run(access => access.ApproveMcp(actor, libraryId, origin.McpResource, offline, cancellation, allowImport, allowCreate, allowCollections));
    public Task<bool> Revoke(LibraryActor actor, Guid id, CancellationToken cancellation) =>
        queue.RevokeConnection(actor, id, () => Run(access => access.Revoke(actor, id, cancellation)), cancellation);
    public async Task<bool> Owned(LibraryActor actor, Guid connectionId, CancellationToken cancellation) =>
        await Run(access => access.Owned(actor, connectionId, origin.Resource, cancellation)) is not null;
    public async Task<bool> ProposalOwned(LibraryActor actor, Guid connectionId, CancellationToken cancellation) =>
        await Run(access => access.Owned(actor, connectionId, origin.ProposalResource, cancellation)) is { ProfileId: LocalProposalProtocol.Profile };
}
