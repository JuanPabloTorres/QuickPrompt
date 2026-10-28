using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Existing agent/editor read port, bound to an authenticated account and one library.</summary>
internal sealed class AccountCollectionReader(AccountAgentMemory memory, LibraryActor actor, Guid libraryId) : ICollectionReader
{
    public GroupSummary[] Groups() => memory.InScope(actor, libraryId, database => CollectionModel.Project(CollectionModel.Rows(database, libraryId).ToArray()));

    public Guid[] Memberships(Guid promptId) => memory.InScope(actor, libraryId, database =>
    {
        if (!database.Prompts.Any(x => x.Id == promptId && x.LibraryId == libraryId)) throw new MissingPromptException();
        return database.CollectionMembers.AsNoTracking().Where(x => x.LibraryId == libraryId && x.PromptId == promptId)
            .OrderBy(x => x.CollectionId).Select(x => x.CollectionId).ToArray();
    });
}
