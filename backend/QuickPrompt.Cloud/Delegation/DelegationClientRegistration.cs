using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Nova.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>One known public device client; no arbitrary client registration or redirect URLs.</summary>
public sealed class DelegationClientRegistration(IServiceScopeFactory scopes, DelegationOrigin origin, McpAccountSettings mcp,
    Microsoft.Extensions.Options.IOptionsMonitor<OpenIddict.Server.OpenIddictServerOptions> serverOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellation)
    {
        _ = serverOptions.CurrentValue; // Validate enabled credentials before accepting traffic.
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountDatabase>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellation);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(5285, 1)", cancellation);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = DelegationPolicy.ClientId,
            ClientType = ClientTypes.Public,
            DisplayName = "QuickPrompt · Componente local",
            ConsentType = ConsentTypes.Explicit,
            Permissions = { Permissions.Endpoints.DeviceAuthorization,
                Permissions.Endpoints.Token, Permissions.Endpoints.Revocation, Permissions.GrantTypes.DeviceCode,
                Permissions.GrantTypes.RefreshToken, Permissions.Prefixes.Scope + DelegationPolicy.Scope,
                Permissions.Prefixes.Scope + LocalProposalProtocol.Scope }
        };
        var existing = await manager.FindByClientIdAsync(descriptor.ClientId, cancellation);
        if (existing is null) await manager.CreateAsync(descriptor, cancellation);
        else
        {
            var permissions = (await manager.GetPermissionsAsync(existing, cancellation)).ToHashSet();
            var fixedPolicy = permissions.Where(x => !x.StartsWith(Permissions.Prefixes.Resource, StringComparison.Ordinal)).ToHashSet();
            var previousPolicy = descriptor.Permissions.Where(x => x != Permissions.Prefixes.Scope + LocalProposalProtocol.Scope).ToHashSet();
            if (await manager.GetClientTypeAsync(existing, cancellation) != ClientTypes.Public
                || await manager.GetConsentTypeAsync(existing, cancellation) != ConsentTypes.Explicit
                || (!fixedPolicy.SetEquals(descriptor.Permissions) && !fixedPolicy.SetEquals(previousPolicy)))
                throw new InvalidOperationException("The registered companion client differs from the fixed delegation policy.");
            // One public client can serve explicitly configured issuers sharing a database.
            // Each server still registers and accepts only its own exact resource.
            var changed = permissions.Add(Permissions.Prefixes.Resource + origin.Resource);
            changed |= permissions.Add(Permissions.Prefixes.Resource + origin.ProposalResource);
            changed |= permissions.Add(Permissions.Prefixes.Scope + LocalProposalProtocol.Scope);
            // Client registration can offer the capability; old grants/tokens are never upgraded.
            if (changed)
            {
                await manager.PopulateAsync(descriptor, existing, cancellation);
                descriptor.Permissions.UnionWith(permissions);
                await manager.UpdateAsync(existing, descriptor, cancellation);
            }
        }
        if (existing is null)
        {
            var created = (await manager.FindByClientIdAsync(descriptor.ClientId, cancellation))!;
            descriptor.Permissions.Add(Permissions.Prefixes.Resource + origin.Resource);
            descriptor.Permissions.Add(Permissions.Prefixes.Resource + origin.ProposalResource);
            await manager.UpdateAsync(created, descriptor, cancellation);
        }
        if (mcp.Enabled)
        {
            var mcpDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = McpDelegationPolicy.ClientId,
                ClientType = ClientTypes.Public,
                DisplayName = "QuickPrompt · MCP",
                ConsentType = ConsentTypes.Explicit,
                RedirectUris = { mcp.RedirectUri! },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
                Permissions = { Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code,
                    Permissions.Prefixes.Scope + McpDelegationPolicy.ReadScope, Permissions.Prefixes.Scope + McpDelegationPolicy.ImportScope,
                    Permissions.Prefixes.Scope + McpDelegationPolicy.CreateScope,
                    Permissions.Prefixes.Scope + McpDelegationPolicy.CollectionsScope,
                    Permissions.Prefixes.Resource + origin.McpResource }
            };
            var client = await manager.FindByClientIdAsync(mcpDescriptor.ClientId, cancellation);
            if (client is null) await manager.CreateAsync(mcpDescriptor, cancellation);
            else
            {
                var stored = (await manager.GetPermissionsAsync(client, cancellation)).ToHashSet();
                var expectedPolicy = mcpDescriptor.Permissions.Where(x => !x.StartsWith(Permissions.Prefixes.Resource, StringComparison.Ordinal)).ToHashSet();
                var storedPolicy = stored.Where(x => !x.StartsWith(Permissions.Prefixes.Resource, StringComparison.Ordinal)).ToHashSet();
                var previousPolicy = expectedPolicy.Where(x => x != Permissions.Prefixes.Scope + McpDelegationPolicy.CollectionsScope).ToHashSet();
                var importPolicy = previousPolicy.Where(x => x != Permissions.Prefixes.Scope + McpDelegationPolicy.CreateScope).ToHashSet();
                var originalPolicy = importPolicy.Where(x => x != Permissions.Prefixes.Scope + McpDelegationPolicy.ImportScope).ToHashSet();
                if (await manager.GetClientTypeAsync(client, cancellation) != ClientTypes.Public
                    || await manager.GetConsentTypeAsync(client, cancellation) != ConsentTypes.Explicit
                    || !(await manager.GetRedirectUrisAsync(client, cancellation)).ToHashSet().SetEquals([mcp.RedirectUri!.AbsoluteUri])
                    || !(await manager.GetRequirementsAsync(client, cancellation)).ToHashSet().SetEquals(mcpDescriptor.Requirements)
                    || (!storedPolicy.SetEquals(expectedPolicy) && !storedPolicy.SetEquals(previousPolicy) && !storedPolicy.SetEquals(importPolicy) && !storedPolicy.SetEquals(originalPolicy)))
                    throw new InvalidOperationException("Registered MCP client differs from the configured callback or fixed policy.");
                // Upgrade only known client policies. Existing grants keep their
                // original scopes; every optional capability requires fresh consent.
                var changed = stored.Add(Permissions.Prefixes.Resource + origin.McpResource);
                changed |= stored.Add(Permissions.Prefixes.Scope + McpDelegationPolicy.ImportScope);
                changed |= stored.Add(Permissions.Prefixes.Scope + McpDelegationPolicy.CreateScope);
                changed |= stored.Add(Permissions.Prefixes.Scope + McpDelegationPolicy.CollectionsScope);
                if (changed)
                {
                    await manager.PopulateAsync(mcpDescriptor, client, cancellation);
                    mcpDescriptor.Permissions.UnionWith(stored);
                    await manager.UpdateAsync(client, mcpDescriptor, cancellation);
                }
            }
        }
        await transaction.CommitAsync(cancellation);
    }
    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}
