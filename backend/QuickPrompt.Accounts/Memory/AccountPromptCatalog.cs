using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Read-only projection over the same actor/library-bound reader used by PromptLibrary.</summary>
internal sealed class AccountPromptCatalog(AccountAgentMemory memory, LibraryActor actor, Guid libraryId) : IPromptCatalog
{
    private readonly IPromptReader reader = new AccountPromptStore(memory, actor, libraryId);
    public PromptDocument Get(Guid id) => PromptLibrary.Document(reader.Find(id) ?? throw new MissingPromptException());
    public CatalogPage<PromptDocument> Search(CatalogQuery query)
    {
        var page = reader.Search(query);
        return new(page.Items.Select(PromptLibrary.Document).ToList(), page.Total);
    }
    public TemplateVersion[] Versions(Guid id) => reader.Versions(id);
}
