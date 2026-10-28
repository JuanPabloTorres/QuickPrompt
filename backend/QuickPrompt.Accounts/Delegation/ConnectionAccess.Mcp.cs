using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Accounts.Delegation;

[Flags]
public enum McpCapabilities { None = 0, Read = 1, Import = 2, Create = 4, Collections = 8 }

public static class McpDelegationPolicy
{
    public const string ClientId = "quickprompt-mcp";
    public const string ReadScope = "prompts:read";
    public const string ImportScope = "shares:import";
    public const string CreateScope = "prompts:create";
    public const string CollectionsScope = "collections:import";
    public const string Profile = "mcp-library-read";
    public const string ImportProfile = "mcp-library-import";
    public const string LibraryProfile = "mcp-library";
    public const string LibraryClaim = "qp:library";

    public static string[] ScopesFor(McpCapabilities capabilities) => new[] {
        (McpCapabilities.Read, ReadScope), (McpCapabilities.Import, ImportScope), (McpCapabilities.Create, CreateScope), (McpCapabilities.Collections, CollectionsScope)
    }.Where(pair => capabilities.HasFlag(pair.Item1)).Select(pair => pair.Item2).ToArray();
}

public sealed partial class ConnectionAccess
{
    /// <summary>Creates one explicitly approved, library-bound browser grant. Protocol validates redirect and PKCE first.</summary>
    public Task<ClaimsPrincipal> ApproveMcp(LibraryActor actor, Guid libraryId, string resource, bool offline, CancellationToken cancellation, bool allowImport = false, bool allowCreate = false, bool allowCollections = false) =>
        AccountTransaction.Run(database, actor, async () =>
        {
            var user = await users.FindByIdAsync(actor.Id.ToString());
            if (user is not { EmailConfirmed: true, SecurityStamp: not null }) throw new UnauthorizedAccessException();
            if (!await database.Libraries.AnyAsync(x => x.Id == libraryId && x.OwnerId == actor.Id, cancellation)) throw new LibraryUnavailableException();
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5285, hashtext({actor.Id.ToString()}))", cancellation);
            if (await database.Connections.CountAsync(x => x.OwnerId == actor.Id && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellation) >= 5)
                throw new ValidationException("Revoca una conexión antes de autorizar otra.");
            var id = Guid.NewGuid();
            var identity = new ClaimsIdentity("QuickPrompt.Delegation");
            identity.SetClaim(Claims.Subject, actor.Id.ToString());
            identity.SetClaim(ClaimTypes.NameIdentifier, actor.Id.ToString());
            identity.SetClaim(DelegationPolicy.ConnectionClaim, id.ToString());
            identity.SetClaim(DelegationPolicy.ProfileClaim, McpDelegationPolicy.LibraryProfile);
            identity.SetClaim(McpDelegationPolicy.LibraryClaim, libraryId.ToString());
            var capabilities = McpCapabilities.Read | (allowImport ? McpCapabilities.Import : McpCapabilities.None) | (allowCreate ? McpCapabilities.Create : McpCapabilities.None)
                | (allowCollections ? McpCapabilities.Collections : McpCapabilities.None);
            var grantedScopes = McpDelegationPolicy.ScopesFor(capabilities).ToList();
            if (offline) grantedScopes.Add(Scopes.OfflineAccess);
            identity.SetScopes(grantedScopes);
            identity.SetResources(resource);
            foreach (var claim in identity.Claims) claim.SetDestinations(Destinations.AccessToken);
            var client = await applications.FindByClientIdAsync(McpDelegationPolicy.ClientId, cancellation)
                ?? throw new InvalidOperationException("MCP client missing.");
            var authorization = await authorizations.CreateAsync(new ClaimsPrincipal(identity), actor.Id.ToString(),
                (await applications.GetIdAsync(client, cancellation))!, AuthorizationTypes.Permanent, identity.GetScopes(), cancellation);
            var authorizationId = Guid.Parse((await authorizations.GetIdAsync(authorization, cancellation))!);
            identity.SetAuthorizationId(authorizationId.ToString());
            var now = DateTimeOffset.UtcNow;
            database.Connections.Add(new()
            {
                Id = id,
                OwnerId = actor.Id,
                AuthorizationId = authorizationId,
                // Legacy column: device grants use the user-code ID; browser grants use a unique server-generated consent correlation.
                UserCodeId = Guid.NewGuid(),
                LibraryId = libraryId,
                Label = "MCP · Biblioteca autorizada",
                ProfileId = McpDelegationPolicy.LibraryProfile,
                McpCapabilities = capabilities,
                Resource = resource,
                StampHash = Stamp(user.SecurityStamp),
                CreatedAt = now,
                ExpiresAt = now.AddDays(30)
            });
            await database.SaveChangesAsync(cancellation);
            return new ClaimsPrincipal(identity);
        }, cancellation);
}
