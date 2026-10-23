using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// JSON documents retain the existing Archive v1/v2 and portable-package codecs.
// Readers have no import/restore methods; these ports are not model tools.
public interface ILibraryArchiveReader
{
    int MaximumBytes { get; }
    string Export();
    ImportPreview Inspect(JsonElement data);
    LibraryRestorePreview InspectRestore(JsonElement data);
}

public interface ILibraryArchiveWriter
{
    int Import(LibraryImportInput input);
    int Restore(LibraryImportInput input);
    LibraryBackup Backup();
}

public interface IPromptSharing
{
    string Export(Guid promptId, int? expectedRevision);
}

public interface ILegacyArchiveConverter
{
    int MaximumBytes { get; }
    Task<string> Convert(Stream source, CancellationToken ct);
}

public sealed record LibraryRestorePreview(ImportPreview Preview, string ExpectedSnapshot);
public sealed record LibraryBackup(string FileName)
{
    public string Integrity => "ok";
}
