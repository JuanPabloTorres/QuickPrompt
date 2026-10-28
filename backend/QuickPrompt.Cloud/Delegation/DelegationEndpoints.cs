using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Nova.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

public static class DelegationEndpoints
{
    private const string Server = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
    public static void MapAccountDelegation(this WebApplication app)
    {
        app.MapPost("/connect/verify", async (HttpContext context, DelegationBoundary access, DelegationOrigin origin, CancellationToken cancellation) =>
        {
            var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!cookie.Succeeded) return Results.Unauthorized();
            var pending = await context.AuthenticateAsync(Server);
            if (!pending.Succeeded || pending.Principal is null) return Reject(Errors.InvalidRequest);
            var request = context.GetOpenIddictServerRequest()!;
            var action = (string?)request.GetParameter("action");
            if (action == "review") return Results.Ok(new
            {
                label = pending.Principal.GetClaim(DelegationPolicy.LabelClaim),
                profileId = pending.Principal.GetClaim(DelegationPolicy.ProfileClaim),
                model = DelegationPolicy.Model,
                permissions = pending.Principal.GetClaim(DelegationPolicy.ProfileClaim) == LocalProposalProtocol.Profile
                    ? new[] { "Recibir contexto de prompts y datos seleccionados para NOVA únicamente cuando envíes una solicitud a este equipo", "Devolver texto o propuestas JSON con Qwen3:8b; no autoriza guardar, borrar, ejecutar herramientas ni acceder a archivos" }
                    : new[] { "Procesar únicamente las solicitudes enviadas a este equipo" },
                expiresInMinutes = 10
            });
            if (action == "deny") return Reject(Errors.AccessDenied);
            if (action != "approve") return Reject(Errors.InvalidRequest);
            var profile = pending.Principal.GetClaim(DelegationPolicy.ProfileClaim);
            if (!LocalCompanionProfiles.Known(profile)) return Reject(Errors.InvalidRequest);
            var principal = await access.Approve(LibraryActor.FromPrincipal(cookie.Principal!), pending.Principal, origin.ForProfile(profile!), cancellation);
            return Results.SignIn(principal, authenticationScheme: Server);
        }).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");

        app.MapPost("/connect/token", async (HttpContext context, DelegationBoundary access, DelegationOrigin origin, CancellationToken cancellation) =>
        {
            var request = context.GetOpenIddictServerRequest()!;
            if (!request.IsDeviceCodeGrantType() && !request.IsRefreshTokenGrantType() && !request.IsAuthorizationCodeGrantType()) return Reject(Errors.UnsupportedGrantType);
            var ticket = await context.AuthenticateAsync(Server);
            if (request.ClientId == McpDelegationPolicy.ClientId)
            {
                if (ticket.Principal is null || request.IsDeviceCodeGrantType() || !request.GetResources().SequenceEqual([origin.McpResource])
                    || await access.McpActive(ticket.Principal, cancellation) is null) return Reject(Errors.InvalidGrant);
                return Results.SignIn(ticket.Principal, authenticationScheme: Server);
            }
            var profile = ticket.Principal?.GetClaim(DelegationPolicy.ProfileClaim);
            if (ticket.Principal is null || request.ClientId != DelegationPolicy.ClientId || !LocalCompanionProfiles.Known(profile)
                || !request.GetResources().SequenceEqual([origin.ForProfile(profile!)])
                || await access.CompanionActive(ticket.Principal, cancellation) is null) return Reject(Errors.InvalidGrant);
            return Results.SignIn(ticket.Principal, authenticationScheme: Server);
        }).RequireRateLimiting("credentials");

        var account = app.MapGroup("/account/connections").RequireAuthorization();
        account.MapGet("/", (HttpContext context, DelegationBoundary access, CancellationToken cancellation) =>
            access.List(LibraryActor.FromPrincipal(context.User), cancellation));
        account.MapPost("/{id:guid}/revoke", async (HttpContext context, Guid id, DelegationBoundary access, CancellationToken cancellation) =>
        { await access.Revoke(LibraryActor.FromPrincipal(context.User), id, cancellation); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();
        app.MapGet("/api/companion/v1/profile", async (HttpContext context, DelegationBoundary access, DelegationOrigin origin, CancellationToken cancellation) =>
        {
            var connection = await access.CompanionActive(context.User, cancellation);
            return connection is null ? Results.Unauthorized() : Results.Ok(new
            {
                connectionId = connection.Id,
                connection.Label,
                connection.ProfileId,
                model = DelegationPolicy.Model,
                workProtocol = LocalCompanionProfiles.Protocol(connection.ProfileId),
                dispatchReady = true
            });
        }).RequireAuthorization(DelegationModule.AnyCompanionPolicy);
        var work = app.MapGroup("/api/companion/v1/work").RequireAuthorization(DelegationModule.Policy).RequireRateLimiting("credentials");
        app.MapPost("/api/companion/v1/disconnect", async (HttpContext context, CompanionWorkService service, CancellationToken cancellation) =>
        { await service.Disconnect(context.User, cancellation); return Results.NoContent(); }).RequireAuthorization(DelegationModule.AnyCompanionPolicy).RequireRateLimiting("credentials");
        work.MapPost("/claim", async (HttpContext context, CompanionWorkService service, CancellationToken cancellation) =>
        {
            var claimed = await service.Claim(context.User, cancellation);
            return claimed is null ? Results.NoContent() : Results.Ok(claimed);
        });
        work.MapPost("/{id:guid}/heartbeat", async (HttpContext context, Guid id, LocalLeaseToken lease, CompanionWorkService service, CancellationToken cancellation) =>
            Results.Ok(await service.Heartbeat(context.User, id, lease.Token, cancellation)));
        work.MapPost("/{id:guid}/finish", async (HttpContext context, Guid id, LocalWorkReceipt receipt, CompanionWorkService service, CancellationToken cancellation) =>
            await service.Finish(context.User, id, receipt, cancellation) ? Results.NoContent() : Results.Conflict());
        var proposals = app.MapGroup("/api/companion/v1/proposals").RequireAuthorization(DelegationModule.ProposalPolicy).RequireRateLimiting("credentials");
        proposals.MapPost("/claim", async (HttpContext context, CompanionProposalService service, CancellationToken cancellation) =>
        {
            var claimed = await service.Claim(context.User, cancellation);
            return claimed is null ? Results.NoContent() : Results.Ok(claimed);
        });
        proposals.MapPost("/{id:guid}/heartbeat", async (HttpContext context, Guid id, LocalLeaseToken lease, CompanionProposalService service, CancellationToken cancellation) =>
            Results.Ok(await service.Heartbeat(context.User, id, lease.Token, cancellation)));
        proposals.MapPost("/{id:guid}/finish", async (HttpContext context, Guid id, LocalProposalReceipt receipt, CompanionProposalService service, CancellationToken cancellation) =>
            await service.Finish(context.User, id, receipt, cancellation) ? Results.NoContent() : Results.Conflict());
        app.MapPost("/api/accounts/v1/libraries/{libraryId:guid}/prompts/{promptId:guid}/run-local",
            async (HttpContext context, Guid libraryId, Guid promptId, LocalSubmission input, AccountExecutionAccess executions, CancellationToken cancellation) =>
                Results.Accepted(value: await executions.RunLocal(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input, cancellation)))
            .RequireAuthorization().AddEndpointFilter<AccountCsrf>();
    }

    private static IResult Reject(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "La vinculación no está autorizada o ya no está disponible."
    }), [Server]);
}
