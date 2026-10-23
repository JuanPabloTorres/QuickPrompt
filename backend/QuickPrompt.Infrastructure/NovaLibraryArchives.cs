using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Adapters to the existing archive/package owner, not another codec or transaction.
public sealed class NovaLibraryArchives(LocalStore store) : ILibraryArchiveReader, ILibraryArchiveWriter
{
    public int MaximumBytes => LocalStore.MaxArchiveBytes;
    public string Export() => store.Export();
    public ImportPreview Inspect(JsonElement data) => store.PreviewImport(data.GetRawText());
    public LibraryRestorePreview InspectRestore(JsonElement data) => store.NovaRestorePreview(data);
    public int Import(LibraryImportInput input)
    {
        if (!input.Confirmed) throw new ValidationException("Revisa y confirma la importación.");
        return store.Import(input.Data.GetRawText());
    }
    public int Restore(LibraryImportInput input) => store.NovaRestore(input);
    public LibraryBackup Backup() => new(store.NovaBackup());
}

public sealed class NovaPromptSharing(LocalStore store, IPromptCatalog catalog) : IPromptSharing
{
    public string Export(Guid promptId, int? expectedRevision)
    {
        catalog.Get(promptId);
        return store.ExportTemplate(promptId, expectedRevision);
    }
}

public sealed class LegacyArchiveConverter : ILegacyArchiveConverter
{
    public int MaximumBytes => LegacyImporter.MaximumBytes;
    public Task<string> Convert(Stream source, CancellationToken ct) => LegacyImporter.Convert(source, ct);
}
