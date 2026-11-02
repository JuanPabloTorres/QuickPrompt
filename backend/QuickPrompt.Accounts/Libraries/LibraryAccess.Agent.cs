using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

public sealed partial class LibraryAccess
{
    /// <summary>Existing prompt/version writer with complete private definition data for application ports.</summary>
    internal async Task<Template> SaveTemplateInTransaction(LibraryActor actor, Guid libraryId, Guid? promptId, int revision, Template edited, CancellationToken cancellation)
    {
        var saved = await SavePromptInTransaction(actor, libraryId, promptId, revision, edited.Title, edited.Template, cancellation, edited);
        return PromptData.Map(await database.Prompts.AsNoTracking().SingleAsync(x => x.LibraryId == libraryId && x.Id == saved.Id, cancellation));
    }

    /// <summary>Records exactly one explicit use of a reviewed rendering; retries do not increase usage.</summary>
    public Task RecordUse(LibraryActor actor, Guid libraryId, Guid promptId, UseInput input, CancellationToken cancellation = default)
    {
        if (input.OperationId == Guid.Empty || input.Values is null || input.Values.Count > 60
            || input.Values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000) || input.ExpectedText is null || input.ExpectedText.Length > 64000)
            throw new ValidationException("Uso inválido.");
        var json = JsonSerializer.Serialize(new { libraryId, promptId, input.Revision, input.ExpectedText, values = input.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray() });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            var key = actor.Id.ToString("N") + input.OperationId.ToString("N");
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5291, hashtext({key}))", cancellation);
            if (!await database.Prompts.AnyAsync(x => x.LibraryId == libraryId && x.Id == promptId, cancellation))
                throw new LibraryUnavailableException();
            var receipt = await database.PromptUses.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == actor.Id && x.OperationId == input.OperationId, cancellation);
            if (receipt is not null)
            {
                if (receipt.LibraryId != libraryId || receipt.PromptId != promptId || receipt.RequestHash != hash) throw new ConflictException("Esta operación de uso corresponde a otro contenido o destino.");
                return true;
            }
            var source = await database.Prompts.SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == promptId && !x.IsArchived, cancellation)
                ?? throw new LibraryUnavailableException();
            if (source.Revision != input.Revision) throw new LibraryRevisionException();
            var compiled = PromptData.Render(PromptData.Map(source), input.Values);
            if (compiled != input.ExpectedText) throw new ConflictException("La representación revisada cambió.");
            // Lock the current revision through this write; a concurrent definition edit
            // cannot record a use against a different version or lose the counter increment.
            var now = DateTimeOffset.UtcNow;
            var changed = await database.Prompts.Where(x => x.LibraryId == libraryId && x.Id == promptId && x.Revision == input.Revision && !x.IsArchived)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Uses, x => x.Uses + 1).SetProperty(x => x.LastUsedAt, now), cancellation);
            if (changed != 1) throw new LibraryRevisionException();
            database.PromptUses.Add(new() { OwnerId = actor.Id, OperationId = input.OperationId, LibraryId = libraryId,
                PromptId = promptId, Revision = input.Revision, RequestHash = hash, CreatedAt = now });
            try { await database.SaveChangesAsync(cancellation); }
            catch (DbUpdateConcurrencyException) { throw new LibraryRevisionException(); }
            return true;
        }, cancellation);
    }
}
