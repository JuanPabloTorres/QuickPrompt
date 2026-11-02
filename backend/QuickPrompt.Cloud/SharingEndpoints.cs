using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;

namespace QuickPrompt.Cloud;

/// <summary>Cookie identity and CSRF protect every sharing mutation; actor IDs never come from request bodies.</summary>
public static class SharingEndpoints
{
    public static void MapAccountSharing(this WebApplication app)
    {
        var shares = app.MapGroup("/api/accounts/v1/sharing").RequireAuthorization();
        shares.MapGet("/collections", (HttpContext context, Guid libraryId, Guid collectionId, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.ListCollectionPublications(LibraryActor.FromPrincipal(context.User), libraryId, collectionId, cancellation));
        shares.MapPost("/collections", (HttpContext context, CollectionPublicationInput input, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.PublishCollection(LibraryActor.FromPrincipal(context.User), input, cancellation)).AddEndpointFilter<AccountCsrf>();
        shares.MapGet("/collections/{publicationId:guid}", (HttpContext context, Guid publicationId, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.ReadCollection(LibraryActor.FromPrincipal(context.User), publicationId, cancellation));
        shares.MapPost("/collections/{publicationId:guid}/import", (HttpContext context, Guid publicationId, ImportRequest input, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.ImportCollection(LibraryActor.FromPrincipal(context.User), publicationId, input.LibraryId, input.OperationId, cancellation)).AddEndpointFilter<AccountCsrf>();
        shares.MapGet("/publications", (HttpContext context, Guid libraryId, Guid promptId, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.ListPublications(LibraryActor.FromPrincipal(context.User), libraryId, promptId, cancellation));
        shares.MapPost("/publications", (HttpContext context, PublicationRequest input, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.Publish(LibraryActor.FromPrincipal(context.User), input.LibraryId, input.PromptId, input.Revision, cancellation)).AddEndpointFilter<AccountCsrf>();
        shares.MapGet("/publications/{publicationId:guid}", (HttpContext context, Guid publicationId, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.Read(LibraryActor.FromPrincipal(context.User), publicationId, cancellation));
        shares.MapPost("/publications/{publicationId:guid}/invite", async (HttpContext context, Guid publicationId, InvitationRequest input, SharingAccess sharing, CancellationToken cancellation) =>
        {
            await sharing.Invite(LibraryActor.FromPrincipal(context.User), publicationId, input.Email, cancellation);
            return Results.Accepted(value: new { message = "Si el destinatario tiene una cuenta confirmada, recibirá la invitación en QuickPrompt." });
        }).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        shares.MapPost("/publications/{publicationId:guid}/revoke", async (HttpContext context, Guid publicationId, SharingAccess sharing, CancellationToken cancellation) =>
        {
            await sharing.Revoke(LibraryActor.FromPrincipal(context.User), publicationId, cancellation);
            return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
        shares.MapPost("/publications/{publicationId:guid}/import", (HttpContext context, Guid publicationId, ImportRequest input, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.Import(LibraryActor.FromPrincipal(context.User), publicationId, input.LibraryId, input.OperationId, cancellation)).AddEndpointFilter<AccountCsrf>();
        shares.MapGet("/invitations", (HttpContext context, SharingAccess sharing, CancellationToken cancellation) =>
            sharing.ListInvitations(LibraryActor.FromPrincipal(context.User), cancellation));
        shares.MapPost("/invitations/{invitationId:guid}/decision", async (HttpContext context, Guid invitationId, InvitationDecision input, SharingAccess sharing, CancellationToken cancellation) =>
        {
            await sharing.Decide(LibraryActor.FromPrincipal(context.User), invitationId, input.State, cancellation);
            return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
    }
}

public sealed record PublicationRequest(Guid LibraryId, Guid PromptId, int Revision);
public sealed record InvitationRequest(string Email);
public sealed record ImportRequest(Guid LibraryId, Guid OperationId);
public sealed record InvitationDecision(InvitationState State);
