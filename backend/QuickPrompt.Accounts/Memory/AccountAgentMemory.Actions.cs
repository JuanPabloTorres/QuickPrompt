using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

public enum PrivatePromptAction { Favorite, Archive, Restore, Duplicate }

public sealed partial class AccountAgentMemory
{
    /// <summary>Reuses the existing prompt owner within one revision-bound transaction; no arbitrary metadata patch.</summary>
    public PromptDocument ChangePrompt(LibraryActor actor, Guid libraryId, Guid promptId, int revision, PrivatePromptAction action)
    {
        if (revision <= 0 || !Enum.IsDefined(action)) throw new ValidationException("Acción o revisión inválida.");
        return InScope(actor, libraryId, database =>
        {
            database.Database.ExecuteSqlInterpolated($"SELECT 1 FROM qp_library.prompts WHERE \"LibraryId\"={libraryId} AND \"Id\"={promptId} AND \"DeletedAt\" IS NULL FOR UPDATE");
            var store = new AccountPromptStore(this, actor, libraryId, database);
            var library = new PromptLibrary(store, store);
            if (library.Get(promptId).Revision != revision) throw new LibraryRevisionException();
            return action switch
            {
                PrivatePromptAction.Favorite => library.Favorite(promptId, revision),
                PrivatePromptAction.Archive => library.Archive(promptId, revision, true),
                PrivatePromptAction.Restore => library.Archive(promptId, revision, false),
                PrivatePromptAction.Duplicate => library.Duplicate(promptId),
                _ => throw new ValidationException("Acción no disponible.")
            };
        });
    }
}
