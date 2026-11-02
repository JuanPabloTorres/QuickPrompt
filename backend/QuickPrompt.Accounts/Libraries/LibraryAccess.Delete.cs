using Microsoft.EntityFrameworkCore;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

public sealed partial class LibraryAccess
{
    /// <summary>Erases a private definition and its version content; independent published snapshots/history remain separate.</summary>
    public Task DeletePrompt(LibraryActor actor, Guid libraryId, Guid promptId, ConfirmInput input, CancellationToken cancellation = default)
    {
        if (!input.Confirmed || input.Revision <= 0) throw new ValidationException("Confirma la eliminación de la revisión actual.");
        return InScope(actor, async () =>
        {
            await DeletePromptInTransaction(actor, libraryId, promptId, input.Revision, cancellation);
            return true;
        }, cancellation);
    }

    internal async Task DeletePromptInTransaction(LibraryActor actor, Guid libraryId, Guid promptId, int revision, CancellationToken cancellation)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        await RequireLibrary(actor, libraryId, cancellation);
        // The restricted SQL owner shares the admission/collection fences and
        // erases content atomically without granting general version UPDATE.
        var result = await database.Database.SqlQuery<int>($"SELECT qp_library.erase_private_prompt({libraryId},{promptId},{revision}) AS \"Value\"")
            .SingleAsync(cancellation);
        if (result == 0) throw new LibraryUnavailableException();
        if (result == 3) throw new LibraryRevisionException();
        if (result == 4) throw new ConflictException("Detén el trabajo activo de esta biblioteca antes de eliminar el prompt.");
        if (result is not (1 or 2)) throw new InvalidOperationException("Private erasure returned an unsupported outcome.");
    }
}
