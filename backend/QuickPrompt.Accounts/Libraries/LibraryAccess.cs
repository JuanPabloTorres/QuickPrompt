using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

/// <summary>Only the authenticated transport may construct the actor; never deserialize it from a tool payload.</summary>
public sealed record LibraryActor
{
    public Guid Id { get; }
    private LibraryActor(Guid id) => Id = id;
    public static LibraryActor FromPrincipal(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Se requiere una cuenta autenticada.");
        return new(id);
    }
}

public sealed class LibraryUnavailableException() : Exception("La biblioteca o el prompt no está disponible.");
public sealed class LibraryRevisionException() : Exception("El prompt cambió; recupera su revisión actual.");
public sealed record AccountPrompt(Guid Id, Guid LibraryId, string Title, string Template, int Revision,
    string Description = "", int CategoryId = 0, bool IsFavorite = false, bool IsArchived = false,
    int Uses = 0, DateTimeOffset? LastUsedAt = null, DateTimeOffset? CreatedAt = null, DateTimeOffset? UpdatedAt = null)
{
    public string[] VariableNames => TemplateCompiler.Names(Template);
    internal static AccountPrompt From(LibraryPrompt row) => From(row.LibraryId, PromptLibrary.Document(PromptData.Map(row)));
    public static AccountPrompt From(Guid library, PromptDocument value) => new(value.Id, library, value.Title, value.TemplateText, value.Revision,
        value.Description, value.CategoryId, value.IsFavorite, value.IsArchived, value.Uses, value.LastUsedAt, value.CreatedAt, value.UpdatedAt);
    // Creation receipts return their initial version, not later metadata edits.
    internal static AccountPrompt Initial(LibraryPrompt source, PromptVersion version) => new(source.Id, source.LibraryId,
        version.Title, version.Template, version.Revision, version.Description, version.Category, CreatedAt: source.CreatedAt, UpdatedAt: version.CreatedAt);
}
public sealed record AccountPromptSummary(Guid Id, string Title, int Revision);

/// <summary>Owner of private library operations. Authorization and RLS context share a bounded transaction.</summary>
public sealed partial class LibraryAccess(AccountDatabase database)
{
    /// <summary>Bounded literal search within one authorized library; the model cannot widen its scope.</summary>
    public Task<AccountPromptSummary[]> SearchPrompts(LibraryActor actor, Guid libraryId, string query, int page, int limit, CancellationToken cancellation = default)
        => SearchPrompts(actor, libraryId, query, page, limit, null, cancellation);

    /// <summary>Filters exact authorized collection membership before paging. Missing/foreign collections never fall back to global search.</summary>
    public Task<AccountPromptSummary[]> SearchPrompts(LibraryActor actor, Guid libraryId, string query, int page, int limit, Guid? collectionId, CancellationToken cancellation = default)
    {
        if (query is null || query.Length > 200 || page is < 0 or > 10000 || limit is < 1 or > 20 || collectionId == Guid.Empty) throw new ArgumentException("Búsqueda inválida.");
        return InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            return await CollectionModel.Filter(database, libraryId, collectionId, false)
                .Where(x => query == "" || x.Title.Contains(query) || x.Template.Contains(query))
                .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(page * limit).Take(limit)
                .Select(x => new AccountPromptSummary(x.Id, x.Title, x.Revision)).ToArrayAsync(cancellation);
        }, cancellation);
    }
    public Task<OwnedLibrary> CreateLibrary(LibraryActor actor, string name, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) throw new ArgumentException("Nombre de biblioteca inválido.");
        return InScope(actor, async () =>
        {
            var library = new OwnedLibrary { Id = Guid.NewGuid(), OwnerId = actor.Id, Name = name.Trim(), CreatedAt = DateTimeOffset.UtcNow };
            database.Libraries.Add(library);
            await database.SaveChangesAsync(cancellation);
            return library;
        }, cancellation);
    }

    public Task<OwnedLibrary[]> ListLibraries(LibraryActor actor, CancellationToken cancellation = default) =>
        InScope(actor, () => database.Libraries.AsNoTracking().Where(x => x.OwnerId == actor.Id)
            .OrderBy(x => x.Name).Take(200).ToArrayAsync(cancellation), cancellation);

    public Task<AccountPrompt[]> ListPrompts(LibraryActor actor, Guid libraryId, CancellationToken cancellation = default) =>
        ListPrompts(actor, libraryId, null, false, cancellation);

    public Task<AccountPrompt[]> ListPrompts(LibraryActor actor, Guid libraryId, Guid? collectionId, bool ungrouped, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            var rows = await CollectionModel.Filter(database, libraryId, collectionId, ungrouped)
                .OrderByDescending(x => x.UpdatedAt).Take(200).AsNoTracking().ToArrayAsync(cancellation);
            return rows.Select(AccountPrompt.From).ToArray();
        }, cancellation);

    public Task<AccountPrompt> GetPrompt(LibraryActor actor, Guid libraryId, Guid promptId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            var row = await database.Prompts.AsNoTracking().SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == promptId, cancellation)
                ?? throw new LibraryUnavailableException();
            return AccountPrompt.From(row);
        }, cancellation);

    public Task<AccountPrompt> SavePrompt(LibraryActor actor, Guid libraryId, Guid? promptId,
        int revision, string title, string template, CancellationToken cancellation = default)
        => InScope(actor, () => SavePromptInTransaction(actor, libraryId, promptId, revision, title, template, cancellation), cancellation);

    // Sharing imports reuse the same validation and version writer inside their atomic receipt transaction.
    internal async Task<AccountPrompt> SavePromptInTransaction(LibraryActor actor, Guid libraryId, Guid? promptId,
        int revision, string title, string template, CancellationToken cancellation, Template? edited = null)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        // Reuse the product's compiler/constraints; a new storage adapter does not redefine placeholders.
        new Template { Title = title, Template = template }.Validate();
        await RequireLibrary(actor, libraryId, cancellation);
        LibraryPrompt prompt;
        if (promptId is { } id)
        {
            prompt = await database.Prompts.SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == id, cancellation)
                ?? throw new LibraryUnavailableException();
            if (prompt.Revision != revision) throw new LibraryRevisionException();
        }
        else
        {
            if (revision != 0) throw new LibraryRevisionException();
            prompt = new() { Id = Guid.NewGuid(), LibraryId = libraryId, CreatedAt = DateTimeOffset.UtcNow };
            database.Prompts.Add(prompt);
        }
        prompt.Title = title.Trim();
        prompt.Template = template;
        if (edited is not null)
        {
            edited.Validate();
            prompt.Description = edited.Description; prompt.Category = (int)edited.Category;
            prompt.IsFavorite = edited.IsFavorite; prompt.IsArchived = !edited.IsActive;
            prompt.Defaults = PromptData.EncodeDefaults(edited.Variables);
        }
        else
        {
            // The title/body API preserves compatible private metadata; removing a
            // field from the definition removes only its now-inapplicable default.
            var names = TemplateCompiler.Names(template);
            prompt.Defaults = PromptData.EncodeDefaults(PromptData.Defaults(prompt.Defaults).Where(x => names.Contains(x.Key)).ToDictionary());
        }
        prompt.Revision++;
        prompt.UpdatedAt = DateTimeOffset.UtcNow;
        database.Versions.Add(new PromptVersion
        {
            LibraryId = libraryId,
            PromptId = prompt.Id,
            Revision = prompt.Revision,
            Title = prompt.Title,
            Template = prompt.Template,
            Description = prompt.Description, Category = prompt.Category, Defaults = prompt.Defaults,
            CreatedAt = prompt.UpdatedAt
        });
        try { await database.SaveChangesAsync(cancellation); }
        catch (DbUpdateConcurrencyException) { throw new LibraryRevisionException(); }
        // The additive date metadata must match PostgreSQL's stored precision on reads/replays.
        await database.Entry(prompt).ReloadAsync(cancellation);
        return AccountPrompt.From(prompt);
    }

    /// <summary>Portable definition only, through the shared domain allowlist. Never exports defaults, identity, memory or results.</summary>
    public Task<TemplatePackage> ExportPrompt(LibraryActor actor, Guid libraryId, Guid promptId, int revision, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            var prompt = await database.Prompts.AsNoTracking().SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == promptId, cancellation)
                ?? throw new LibraryUnavailableException();
            if (prompt.Revision != revision) throw new LibraryRevisionException();
            return TemplatePackage.From(PromptData.Map(prompt));
        }, cancellation);

    public Task<PromptVersion[]> ListVersions(LibraryActor actor, Guid libraryId, Guid promptId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            if (!await database.Prompts.AsNoTracking().AnyAsync(x => x.LibraryId == libraryId && x.Id == promptId, cancellation))
                throw new LibraryUnavailableException();
            return await database.Versions.AsNoTracking().Where(x => x.LibraryId == libraryId && x.PromptId == promptId)
                .OrderByDescending(x => x.Revision).Take(200).ToArrayAsync(cancellation);
        }, cancellation);

    private async Task RequireLibrary(LibraryActor actor, Guid libraryId, CancellationToken cancellation)
    {
        if (!await database.Libraries.AsNoTracking().AnyAsync(x => x.Id == libraryId && x.OwnerId == actor.Id, cancellation))
            throw new LibraryUnavailableException();
    }

    private Task<T> InScope<T>(LibraryActor actor, Func<Task<T>> operation, CancellationToken cancellation) =>
        AccountTransaction.Run(database, actor, operation, cancellation);
}
