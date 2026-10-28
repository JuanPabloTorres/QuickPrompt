using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Abstractions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Accounts.Delegation;

public static class DelegationPolicy
{
    public const string ClientId = LocalWorkProtocol.ClientId;
    public const string Scope = LocalWorkProtocol.Scope;
    public const string Profile = LocalWorkProtocol.Profile;
    public const string Model = LocalWorkProtocol.Model;
    public const string ConnectionClaim = "qp:connection";
    public const string ProfileClaim = "qp:profile";
    public const string LabelClaim = "qp:device-label";
}

/// <summary>Domain consent and revocation. The protocol supplies a validated pending principal, never owner arguments.</summary>
public sealed partial class ConnectionAccess(AccountDatabase database, UserManager<AccountUser> users,
    IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations, ILogger<ConnectionAccess> logger)
{
    public Task<ConnectionView[]> List(LibraryActor actor, CancellationToken cancellation = default) =>
        AccountTransaction.Run(database, actor, async () =>
        {
            var owner = await users.FindByIdAsync(actor.Id.ToString());
            var stamp = owner is { EmailConfirmed: true, SecurityStamp: not null } ? Stamp(owner.SecurityStamp) : null;
            var rows = await database.Connections.AsNoTracking().Where(x => x.OwnerId == actor.Id)
                .OrderByDescending(x => x.CreatedAt).Take(100).ToArrayAsync(cancellation);
            var ids = rows.Select(x => x.AuthorizationId).ToArray();
            var valid = await database.Set<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().AsNoTracking()
                .Where(x => ids.Contains(x.Id) && x.Status == Statuses.Valid && x.Subject == actor.Id.ToString())
                .Select(x => x.Id).ToArrayAsync(cancellation);
            var now = DateTimeOffset.UtcNow;
            return rows.Select(x => new ConnectionView(x.Id, x.Label, x.ProfileId, x.CreatedAt, x.ExpiresAt,
                x.RevokedAt != null, x.RevokedAt is null && x.ExpiresAt > now && x.StampHash == stamp && valid.Contains(x.AuthorizationId), x.LibraryId, McpDelegationPolicy.ScopesFor(x.McpCapabilities))).ToArray();
        }, cancellation);

    public async Task<ClaimsPrincipal> Approve(LibraryActor actor, ClaimsPrincipal pending, string resource, CancellationToken cancellation)
    {
        var label = pending.GetClaim(DelegationPolicy.LabelClaim);
        var profile = pending.GetClaim(DelegationPolicy.ProfileClaim);
        if (!Guid.TryParse(pending.GetTokenId(), out var codeId) || codeId == Guid.Empty
            || !Guid.TryParse(pending.GetAuthorizationId(), out var authorizationId) || authorizationId == Guid.Empty
            || label is not { Length: > 0 and <= 80 } || !LocalCompanionProfiles.Known(profile)
            || !pending.HasScope(LocalCompanionProfiles.Scope(profile!))
            || pending.GetScopes().Any(x => x != LocalCompanionProfiles.Scope(profile!) && x != Scopes.OfflineAccess)
            || !pending.GetResources().SequenceEqual([resource]))
            throw new ValidationException("La vinculación no corresponde al perfil permitido.");
        var user = await users.FindByIdAsync(actor.Id.ToString());
        if (user is not { EmailConfirmed: true, SecurityStamp: not null }) throw new UnauthorizedAccessException();
        return await AccountTransaction.Run(database, actor, async () =>
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5285, hashtext({actor.Id.ToString()}))", cancellation);
            if (await database.Connections.AnyAsync(x => x.UserCodeId == codeId, cancellation)) throw new ConflictException("Este código ya se revisó.");
            if (await database.Connections.CountAsync(x => x.OwnerId == actor.Id && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellation) >= 5)
                throw new ValidationException("Revoca una conexión antes de vincular otro equipo.");
            var id = Guid.NewGuid();
            var identity = new ClaimsIdentity("QuickPrompt.Delegation");
            identity.SetClaim(Claims.Subject, actor.Id.ToString());
            identity.SetClaim(ClaimTypes.NameIdentifier, actor.Id.ToString());
            identity.SetClaim(DelegationPolicy.ConnectionClaim, id.ToString());
            identity.SetClaim(DelegationPolicy.ProfileClaim, profile);
            identity.SetScopes(pending.GetScopes()); identity.SetResources(resource);
            foreach (var claim in identity.Claims) claim.SetDestinations(Destinations.AccessToken);
            var client = await applications.FindByClientIdAsync(DelegationPolicy.ClientId, cancellation) ?? throw new InvalidOperationException("Companion client missing.");
            // The device/user code pair already belongs to one protocol authorization.
            // Bind that same grant to the consenting person; replacing its ID would leave
            // the existing device-code database entry attached to the anonymous grant.
            var authorization = await authorizations.FindByIdAsync(authorizationId.ToString(), cancellation)
                ?? throw new ValidationException("La vinculación ya no está disponible.");
            if (await authorizations.GetStatusAsync(authorization, cancellation) != Statuses.Valid
                || await authorizations.GetSubjectAsync(authorization, cancellation) is not null
                || await authorizations.GetTypeAsync(authorization, cancellation) != AuthorizationTypes.AdHoc
                || await authorizations.GetApplicationIdAsync(authorization, cancellation) != await applications.GetIdAsync(client, cancellation))
                throw new ValidationException("La vinculación ya no está disponible.");
            identity.SetAuthorizationId(authorizationId.ToString());
            var descriptor = new OpenIddictAuthorizationDescriptor();
            await authorizations.PopulateAsync(descriptor, authorization, cancellation);
            descriptor.Subject = actor.Id.ToString(); descriptor.Type = AuthorizationTypes.Permanent;
            descriptor.Principal = new ClaimsPrincipal(identity);
            await authorizations.UpdateAsync(authorization, descriptor, cancellation);
            var now = DateTimeOffset.UtcNow;
            database.Connections.Add(new()
            {
                Id = id,
                OwnerId = actor.Id,
                AuthorizationId = authorizationId,
                UserCodeId = codeId,
                Label = label,
                ProfileId = profile!,
                Resource = resource,
                StampHash = Stamp(user.SecurityStamp),
                CreatedAt = now,
                ExpiresAt = now.AddDays(30)
            });
            try { await database.SaveChangesAsync(cancellation); }
            catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            { throw new ConflictException("Este código ya se revisó."); }
            return new ClaimsPrincipal(identity);
        }, cancellation);
    }

    /// <summary>Fresh database check supplements protocol validation and catches revocation across host instances.</summary>
    public async Task<DelegatedConnection?> Active(ClaimsPrincipal principal, string resource, CancellationToken cancellation, string scope = DelegationPolicy.Scope)
    {
        if (!principal.HasScope(scope) || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var owner)
            || !Guid.TryParse(principal.GetClaim(DelegationPolicy.ConnectionClaim), out var id)
            || principal.FindFirstValue(ClaimTypes.NameIdentifier) != owner.ToString()) return Unavailable("PRINCIPAL");
        var actor = LibraryActor.FromPrincipal(principal);
        var connection = await Owned(actor, id, resource, cancellation);
        if (connection is null) return null;
        if (principal.GetAuthorizationId() != connection.AuthorizationId.ToString()) return Unavailable("AUTHORIZATION");
        if (principal.GetClaim(DelegationPolicy.ProfileClaim) != connection.ProfileId) return Unavailable("PROFILE");
        if (scope is DelegationPolicy.Scope or LocalProposalProtocol.Scope
            && (!LocalCompanionProfiles.Known(connection.ProfileId)
                || LocalCompanionProfiles.Scope(connection.ProfileId) != scope
                || connection.LibraryId is not null || connection.McpCapabilities != McpCapabilities.None
                || principal.GetScopes().Any(x => x != scope && x != Scopes.OfflineAccess))) return Unavailable("GRANT_SCOPE");
        if (scope == McpDelegationPolicy.ReadScope && (connection.ProfileId is not (McpDelegationPolicy.Profile or McpDelegationPolicy.ImportProfile or McpDelegationPolicy.LibraryProfile)
            || connection.LibraryId is null || principal.GetClaim(McpDelegationPolicy.LibraryClaim) != connection.LibraryId.ToString())) return Unavailable("LIBRARY");
        if (scope == McpDelegationPolicy.ReadScope && (!connection.McpCapabilities.HasFlag(McpCapabilities.Read)
            || principal.GetScopes().Where(x => x != Scopes.OfflineAccess).Any(x => !McpDelegationPolicy.ScopesFor(connection.McpCapabilities).Contains(x)))) return Unavailable("GRANT_SCOPE");
        if (!Guid.TryParse(principal.GetTokenId(), out var tokenId) || !await database.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().AsNoTracking().AnyAsync(x =>
            x.Id == tokenId && x.Status == Statuses.Valid && x.Subject == actor.Id.ToString() && x.Authorization!.Id == connection.AuthorizationId, cancellation))
            return Unavailable("TOKEN");
        return connection;
    }

    public async Task<DelegatedConnection?> Owned(LibraryActor actor, Guid id, string resource, CancellationToken cancellation)
    {
        var connection = await AccountTransaction.Run(database, actor, () => database.Connections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor.Id && x.Resource == resource && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellation), cancellation);
        if (connection is null) return Unavailable("CONNECTION");
        // Bypass the protocol manager's per-host cache for domain authority. A grant
        // revoked by another issuer instance or replay protection must take effect here.
        if (!await database.Set<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().AsNoTracking().AnyAsync(x =>
            x.Id == connection.AuthorizationId && x.Status == Statuses.Valid && x.Subject == actor.Id.ToString(), cancellation)) return Unavailable("GRANT");
        var user = await users.FindByIdAsync(actor.Id.ToString());
        return user is { EmailConfirmed: true, SecurityStamp: not null } && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(connection.StampHash), Convert.FromHexString(Stamp(user.SecurityStamp))) ? connection : null;
    }

    public Task<bool> Revoke(LibraryActor actor, Guid id, CancellationToken cancellation = default) => AccountTransaction.Run(database, actor, async () =>
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5285, hashtext({id.ToString()}))", cancellation);
        var connection = await database.Connections.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor.Id, cancellation)
            ?? throw new LibraryUnavailableException();
        if (connection.RevokedAt is not null) return false;
        connection.RevokedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellation);
        await database.Executions.Where(x => x.OwnerId == actor.Id && x.ConnectionId == id
            && (x.Status == QuickPrompt.Accounts.Executions.ExecutionStatus.Queued || x.Status == QuickPrompt.Accounts.Executions.ExecutionStatus.Running))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.CancelRequested, true), cancellation);
        var authorization = await authorizations.FindByIdAsync(connection.AuthorizationId.ToString(), cancellation);
        if (authorization is not null && !await authorizations.TryRevokeAsync(authorization, cancellation))
            throw new ConflictException("No se pudo confirmar la revocación. Reintenta.");
        return true;
    }, cancellation);

    private static string Stamp(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private DelegatedConnection? Unavailable(string reason)
    {
        logger.LogWarning(5285, "Delegated connection rejected: {Reason}", reason);
        return null;
    }
}
