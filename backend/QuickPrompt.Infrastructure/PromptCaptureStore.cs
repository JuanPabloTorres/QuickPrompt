using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore : IPromptCaptureWriter
{
    /// <summary>Stores the prompt, version and hash-only receipt atomically. Retries never resurrect an edited or deleted record.</summary>
    public PromptCaptureWriteResult Capture(Guid operationId, Template prompt)
    {
        if (operationId == Guid.Empty) throw new ValidationException("Identificador de operación requerido.");
        prompt.Validate();
        var hash = CaptureHash(prompt);
        return Write((db, tx) =>
        {
            var receipt = GetOne<PromptCaptureReceipt>(db, "prompt-capture", operationId, tx);
            if (receipt is not null)
            {
                if (receipt.Hash != hash) throw new ConflictException("La operación de captura ya se utilizó con otro contenido.");
                var saved = GetOne<Template>(db, "template", receipt.PromptId, tx);
                if (saved is null || saved.Revision != receipt.Revision || CaptureHash(saved) != hash)
                    throw new ConflictException("La captura ya se completó, pero el prompt cambió o fue eliminado. No se repetirá.");
                return new PromptCaptureWriteResult(saved, true);
            }
            var created = SaveInside(db, tx, prompt, "external capture");
            // No duplicate body/defaults are retained by the receipt. Full SQLite backup preserves these keys;
            // portable content export/import does not transfer execution receipts to another library.
            Put(db, tx, "prompt-capture", operationId.ToString(),
                new PromptCaptureReceipt(hash, created.Id, created.Revision, DateTime.UtcNow));
            return new PromptCaptureWriteResult(created, false);
        });
    }

    private static string CaptureHash(Template prompt) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            prompt.Title, prompt.Template, prompt.Description, prompt.Category,
            Defaults = prompt.Variables.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
        }, Json))));

    private sealed record PromptCaptureReceipt(string Hash, Guid PromptId, int Revision, DateTime CreatedAt);
}
