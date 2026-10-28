using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Actor/library-bound adapters for PromptLibrary. LibraryAccess remains the version writer.</summary>
internal sealed class AccountPromptStore(AccountAgentMemory memory, LibraryActor actor, Guid libraryId, AccountDatabase? transactionDatabase = null) : IPromptReader, IPromptWriter
{
    private T InScope<T>(Func<AccountDatabase, T> operation)
    {
        if (transactionDatabase is null) return memory.InScope(actor, libraryId, operation);
        if (transactionDatabase.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        return operation(transactionDatabase);
    }

    public Template? Find(Guid id) => InScope(database =>
        database.Prompts.AsNoTracking().SingleOrDefault(x => x.LibraryId == libraryId && x.Id == id) is { } row ? PromptData.Map(row) : null);

    public CatalogPage<Template> Search(CatalogQuery query) => InScope(database =>
    {
        if (query.Page is < 0 or > 10000 || query.Size is < 1 or > 100 || query.Search is null || query.Search.Length > 1000
            || query.Sort is not ("title" or "updated" or "recent")) throw new ValidationException("Consulta inválida.");
        var found = CollectionModel.Filter(database, libraryId, query.GroupId, query.Ungrouped, includeArchived: true).Where(x => x.IsArchived != query.Active);
        if (query.Category.Length > 0)
        {
            if (!int.TryParse(query.Category, out var category) || !Enum.IsDefined((PromptCategory)category)) throw new ValidationException("Categoría inválida.");
            found = found.Where(x => x.Category == category);
        }
        if (query.Favorites) found = found.Where(x => x.IsFavorite);
        if (query.Recent) found = found.Where(x => x.LastUsedAt != null);
        if (query.Search.Length > 0)
        {
            // Apply Latin accent/case folding on both sides before bounded recall.
            // Wildcards remain literal and every join remains library-scoped.
            var pattern = "%" + PromptSearch.Normalize(query.Search).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            found = found.Where(x => EF.Functions.Like(AccountSearchText.Latin(x.Title), pattern, "\\") || EF.Functions.Like(AccountSearchText.Latin(x.Template), pattern, "\\")
                || EF.Functions.Like(AccountSearchText.Latin(x.Description), pattern, "\\") || database.CollectionMembers.Any(member => member.LibraryId == libraryId && member.PromptId == x.Id
                    && database.Collections.Any(group => group.LibraryId == libraryId && group.Id == member.CollectionId && EF.Functions.Like(AccountSearchText.Latin(group.Name), pattern, "\\"))));
        }
        var count = found.Count();
        var sorted = query.Sort == "title" ? found.OrderBy(x => x.Title).ThenBy(x => x.Id)
            : query.Sort == "recent" ? found.OrderByDescending(x => x.LastUsedAt).ThenBy(x => x.Id)
            : found.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id);
        return new CatalogPage<Template>(sorted.Skip(query.Page * query.Size).Take(query.Size).AsEnumerable().Select(PromptData.Map).ToList(), count);
    });

    public TemplateVersion[] Versions(Guid id) => InScope(database =>
    {
        if (!database.Prompts.Any(x => x.LibraryId == libraryId && x.Id == id)) throw new MissingPromptException();
        return database.Versions.AsNoTracking().Where(x => x.LibraryId == libraryId && x.PromptId == id).OrderByDescending(x => x.Revision).Take(200)
            .AsEnumerable().Select(row => new TemplateVersion(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{libraryId:N}:{id:N}:{row.Revision}")).AsSpan(0, 16)),
                id, row.Revision, PromptData.Map(row), "Versión privada conservada", row.CreatedAt.UtcDateTime)).ToArray();
    });

    public Template Create(Template prompt) => InScope(database =>
        new LibraryAccess(database).SaveTemplateInTransaction(actor, libraryId, null, 0, prompt, CancellationToken.None).GetAwaiter().GetResult());

    public Template Change(Guid id, int revision, Action<Template> change, string reason) => InScope(database =>
    {
        var row = database.Prompts.AsNoTracking().SingleOrDefault(x => x.LibraryId == libraryId && x.Id == id) ?? throw new MissingPromptException();
        if (row.Revision != revision) throw new LibraryRevisionException();
        var edited = PromptData.Map(row); change(edited);
        if (edited.Id != id || edited.Revision != revision || edited.Uses != row.Uses || edited.LastUsed != row.LastUsedAt?.UtcDateTime)
            throw new ValidationException("La edición no puede sustituir identidad, revisión o recibos de uso.");
        return new LibraryAccess(database).SaveTemplateInTransaction(actor, libraryId, id, revision, edited, CancellationToken.None).GetAwaiter().GetResult();
    });

    public CompiledPrompt Preview(Guid id, int revision, Dictionary<string, string> values)
    {
        var source = Find(id) ?? throw new MissingPromptException();
        if (!source.IsActive || source.Revision != revision) throw new ConflictException("El prompt cambió o está archivado.");
        return new(Guid.NewGuid(), id, revision, source.Title, PromptData.Render(source, values), new Dictionary<string, string>(), DateTime.UtcNow);
    }
    public void RecordUse(Guid id, UseInput input) => memory.WithDatabase(database =>
    { new LibraryAccess(database).RecordUse(actor, libraryId, id, input).GetAwaiter().GetResult(); return true; });

    public void Delete(Guid id, int revision) => InScope(database =>
    {
        new LibraryAccess(database).DeletePromptInTransaction(actor, libraryId, id, revision, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    });
}
