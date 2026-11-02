using Microsoft.AspNetCore.Mvc;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

/// <summary>Account identity and CSRF adapt the existing collection contracts; domain/storage owns revisions and membership.</summary>
public static class CollectionEndpoints
{
    public static void MapAccountCollections(this WebApplication app)
    {
        var library = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}").RequireAuthorization();
        library.MapGet("/groups", (HttpContext context, Guid libraryId, LibraryAccess access, CancellationToken cancellation) =>
            access.ListCollections(LibraryActor.FromPrincipal(context.User), libraryId, cancellation));
        library.MapPost("/groups", (HttpContext context, Guid libraryId, GroupInput input, LibraryAccess access, CancellationToken cancellation) =>
            access.SaveCollection(LibraryActor.FromPrincipal(context.User), libraryId, null, input, cancellation)).AddEndpointFilter<AccountCsrf>();
        library.MapPut("/groups/{id:guid}", (HttpContext context, Guid libraryId, Guid id, GroupInput input, LibraryAccess access, CancellationToken cancellation) =>
            access.SaveCollection(LibraryActor.FromPrincipal(context.User), libraryId, id, input, cancellation)).AddEndpointFilter<AccountCsrf>();
        library.MapDelete("/groups/{id:guid}", async (HttpContext context, Guid libraryId, Guid id, [FromBody] ConfirmInput input, LibraryAccess access, CancellationToken cancellation) =>
        {
            await access.DeleteCollection(LibraryActor.FromPrincipal(context.User), libraryId, id, input, cancellation); return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
        library.MapGet("/prompts/{promptId:guid}/groups", (HttpContext context, Guid libraryId, Guid promptId, LibraryAccess access, CancellationToken cancellation) =>
            access.Memberships(LibraryActor.FromPrincipal(context.User), libraryId, promptId, cancellation));
        library.MapPost("/prompts/{promptId:guid}/groups", async (HttpContext context, Guid libraryId, Guid promptId, MembershipInput input, LibraryAccess access, CancellationToken cancellation) =>
        {
            await access.AssignCollection(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input, cancellation); return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
    }
}
