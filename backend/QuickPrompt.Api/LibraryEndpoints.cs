using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Nova.Api;

// HTTP adaptation for library capabilities. Host/security configuration stays in Program.
public static class LibraryEndpoints
{
    public static void MapLibrary(this RouteGroupBuilder api)
    {
        api.MapGet("/providers", () => ProviderRegistry.Providers);
        api.MapGet("/categories", () => PromptCategories.All);
        api.MapGet("/history", (int? page, IResultHistory history) => history.ResultHistory(page ?? 0));
        api.MapPost("/history/clear", (HistoryClearInput input, IResultHistory history) => { history.ClearResultHistory(input); return Results.NoContent(); });
        api.MapPost("/transfers/prepare", (TransferInput input, IPromptTransfer transfer) => transfer.Prepare(input));
        api.MapGet("/prompts", (HttpRequest request, IPromptCatalog library) =>
        {
            var q = request.Query;
            bool Flag(string key) => q[key] == "true";
            int Number(string key, int fallback) => q[key].Count == 0 ? fallback : int.TryParse(q[key], out var n) ? n : throw new ValidationException("Paginación inválida.");
            Guid? group = q["groupId"].Count == 0 ? null : Guid.TryParse(q["groupId"], out var id) ? id : throw new ValidationException("Grupo inválido.");
            return library.Search(new(q["search"].FirstOrDefault() ?? "", Favorites: Flag("favorites"),
                Active: !Flag("archived"), GroupId: group, Sort: q["sort"].FirstOrDefault() ?? "updated",
                Page: Number("page", 0), Size: Number("size", 24), Recent: Flag("recent")));
        });
        api.MapGet("/prompts/{id:guid}", (Guid id, IPromptCatalog library) => library.Get(id));
        api.MapPost("/prompts", (PromptInput input, PromptLibrary library) =>
        {
            var prompt = library.Create(input); return Results.Created($"/api/v1/prompts/{prompt.Id}", prompt);
        });
        api.MapPut("/prompts/{id:guid}", (Guid id, PromptInput input, PromptLibrary library) => library.Update(id, input));
        api.MapPost("/prompts/{id:guid}/favorite", (Guid id, RevisionInput input, PromptLibrary library) => library.Favorite(id, input.Revision));
        api.MapPost("/prompts/{id:guid}/archive", (Guid id, RevisionInput input, PromptLibrary library) => library.Archive(id, input.Revision, true));
        api.MapPost("/prompts/{id:guid}/restore", (Guid id, RevisionInput input, PromptLibrary library) => library.Archive(id, input.Revision, false));
        api.MapPost("/prompts/{id:guid}/duplicate", (Guid id, PromptLibrary library) => library.Duplicate(id));
        api.MapDelete("/prompts/{id:guid}", (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ConfirmInput input, PromptLibrary library) => { library.Delete(id, input); return Results.NoContent(); });
        api.MapPost("/prompts/{id:guid}/compile", (Guid id, CompileInput input, PromptLibrary library) => library.Compile(id, input));
        api.MapPost("/prompts/{id:guid}/usage", (Guid id, UseInput input, PromptLibrary library) => { library.RecordUse(id, input); return Results.NoContent(); });
        api.MapPost("/templates/inspect", (InspectInput input) => new { variableNames = TemplateCompiler.Names(input.TemplateText) });
        api.MapPost("/templates/toggle-variable", (ToggleVariableInput input) => EditorTools.Toggle(input));
        api.MapPost("/templates/rename-variable", (RenameVariableInput input) => EditorTools.Rename(input));
        api.MapGet("/groups", (ICollectionReader collections) => collections.Groups());
        api.MapPost("/groups", (GroupInput input, ICollectionWriter collections) => collections.Create(input));
        api.MapPut("/groups/{id:guid}", (Guid id, GroupInput input, ICollectionWriter collections) => collections.Rename(id, input));
        api.MapDelete("/groups/{id:guid}", (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ConfirmInput input, ICollectionWriter collections) => { collections.Delete(id, input); return Results.NoContent(); });
        api.MapGet("/prompts/{id:guid}/groups", (Guid id, ICollectionReader collections) => collections.Memberships(id));
        api.MapPost("/prompts/{id:guid}/groups", (Guid id, MembershipInput input, ICollectionWriter collections) => { collections.Assign(id, input); return Results.NoContent(); });
        api.MapGet("/prompts/{id:guid}/versions", (Guid id, IPromptCatalog library) => library.Versions(id));
        api.MapGet("/prompts/{id:guid}/share", (Guid id, int? revision, IPromptSharing sharing) => Results.Text(sharing.Export(id, revision), "application/json"));
        api.MapPost("/prompts/{id:guid}/versions/{versionRevision:int}/restore", (Guid id, int versionRevision, ConfirmInput input, PromptLibrary library) => library.RestoreVersion(id, versionRevision, input));
        api.MapGet("/library/export", (ILibraryArchiveReader archives) => Results.Text(archives.Export(), "application/json"));
        api.MapPost("/library/import/preview", (JsonElement input, ILibraryArchiveReader archives) => archives.Inspect(input));
        api.MapPost("/library/import", (LibraryImportInput input, ILibraryArchiveWriter archives) => new { imported = archives.Import(input) });
        api.MapPost("/library/restore/preview", (JsonElement input, ILibraryArchiveReader archives) => archives.InspectRestore(input));
        api.MapPost("/library/restore", (LibraryImportInput input, ILibraryArchiveWriter archives) => new { imported = archives.Restore(input) });
        api.MapPost("/library/backup", (ILibraryArchiveWriter archives) => archives.Backup());
        api.MapPost("/library/legacy/convert", async (HttpRequest request, ILegacyArchiveConverter converter, CancellationToken ct) =>
        {
            if (request.ContentLength > converter.MaximumBytes) throw new ValidationException("El archivo SQLite supera 4 MB.");
            return Results.Text(await converter.Convert(request.Body, ct), "application/json");
        });
    }
}
