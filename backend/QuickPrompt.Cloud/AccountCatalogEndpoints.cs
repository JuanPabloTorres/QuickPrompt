using Microsoft.AspNetCore.Mvc;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public sealed record AccountPromptPage(AccountPrompt[] Items, int Total, int Page, int Size);

/// <summary>Private REST adapter over the same catalogue/action owners used by NOVA; never falls back to the local store.</summary>
public static class AccountCatalogEndpoints
{
    public static void MapAccountCatalog(this WebApplication app)
    {
        var prompts = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}/prompts").RequireAuthorization();
        prompts.MapGet("/catalog", Catalog);
        MapAction("favorite", PrivatePromptAction.Favorite);
        MapAction("archive", PrivatePromptAction.Archive);
        MapAction("restore", PrivatePromptAction.Restore);
        MapAction("duplicate", PrivatePromptAction.Duplicate);
        prompts.MapPost("/{promptId:guid}/use", async (HttpContext context, Guid libraryId, Guid promptId, UseInput input,
            LibraryAccess library, CancellationToken cancellation) =>
        {
            await library.RecordUse(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input, cancellation);
            return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
        prompts.MapDelete("/{promptId:guid}", async (HttpContext context, Guid libraryId, Guid promptId, [FromBody] ConfirmInput input,
            LibraryAccess library, CancellationToken cancellation) =>
        {
            await library.DeletePrompt(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input, cancellation);
            return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();

        void MapAction(string name, PrivatePromptAction action) => prompts.MapPost("/{promptId:guid}/" + name,
            (HttpContext context, Guid libraryId, Guid promptId, RevisionInput input, AccountAgentMemory memory) =>
                AccountPrompt.From(libraryId, memory.ChangePrompt(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input.Revision, action)))
            .AddEndpointFilter<AccountCsrf>();
    }

    private static AccountPromptPage Catalog(HttpContext context, Guid libraryId, AccountAgentMemory memory, CancellationToken cancellation,
        string? search = null, string? filter = null, Guid? collectionId = null, bool? ungrouped = null,
        int page = 0, int size = 12, string? sort = null)
    {
        filter ??= "all";
        if (search?.Length > 200 || filter is not ("all" or "favorites" or "recent" or "archived"))
            throw new ValidationException("Consulta de catálogo inválida.");
        cancellation.ThrowIfCancellationRequested();
        var result = memory.Catalog(LibraryActor.FromPrincipal(context.User), libraryId).Search(new CatalogQuery(
            Search: search ?? "", Favorites: filter == "favorites", Active: filter != "archived", GroupId: collectionId,
            Ungrouped: ungrouped ?? false, Sort: sort ?? (filter == "recent" ? "recent" : "updated"), Page: page, Size: size, Recent: filter == "recent"));
        cancellation.ThrowIfCancellationRequested();
        return new(result.Items.Select(item => AccountPrompt.From(libraryId, item)).ToArray(), result.Total, page, size);
    }
}
