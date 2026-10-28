using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Libraries;

public sealed record AccountCaptureInput(Guid OperationId, string Title, string TemplateText);
public sealed record AccountCaptureReceipt(AccountPrompt Prompt, bool Replayed);
public sealed class CaptureConflictException() : Exception("Esta operación de creación corresponde a otro contenido o destino.");

/// <summary>Private immutable receipt referencing the initial version; it contains no second copy of the prompt body.</summary>
public sealed class PromptCaptureReceipt
{
    public Guid OwnerId { get; set; }
    public Guid OperationId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int Revision { get; set; } = 1;
    public string ContentHash { get; set; } = "";
}

public sealed partial class LibraryAccess
{
    /// <summary>Creates a prompt, initial version and receipt atomically. Creation IDs are namespaced separately from sharing imports.</summary>
    public Task<AccountCaptureReceipt> Capture(LibraryActor actor, Guid libraryId, AccountCaptureInput input, CancellationToken cancellation = default)
    {
        if (input.OperationId == Guid.Empty) throw new ArgumentException("Se requiere identidad de operación.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { libraryId.ToString("N"), input.Title, input.TemplateText }))));
        return InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            var key = actor.Id.ToString("N") + input.OperationId.ToString("N");
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5279, hashtext({key}))", cancellation);
            var previous = await database.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == actor.Id && x.OperationId == input.OperationId, cancellation);
            if (previous is not null)
            {
                if (previous.LibraryId != libraryId || previous.ContentHash != hash) throw new CaptureConflictException();
                var source = await database.Prompts.AsNoTracking().SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == previous.PromptId, cancellation)
                    ?? throw new LibraryUnavailableException();
                var initial = await database.Versions.AsNoTracking().SingleAsync(x => x.LibraryId == libraryId && x.PromptId == previous.PromptId && x.Revision == 1, cancellation);
                return new AccountCaptureReceipt(AccountPrompt.Initial(source, initial), true);
            }
            var prompt = await SavePromptInTransaction(actor, libraryId, null, 0, input.Title, input.TemplateText, cancellation);
            database.Captures.Add(new() { OwnerId = actor.Id, OperationId = input.OperationId, LibraryId = libraryId, PromptId = prompt.Id, ContentHash = hash });
            await database.SaveChangesAsync(cancellation);
            return new AccountCaptureReceipt(prompt, false);
        }, cancellation);
    }
}
