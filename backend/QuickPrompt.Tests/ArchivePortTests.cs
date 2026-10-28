using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ArchivePortTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-archive-ports-" + Guid.NewGuid());
    private LocalPromptLibrary Module(string name = "source") => new(Path.Combine(directory, name + ".sqlite"));
    private static JsonElement Document(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact]
    public void SharingUsesCurrentRevisionAndTheExistingPrivateDataAllowlist()
    {
        var module = Module();
        var source = module.Store.Save(new() { Title = "Portátil", Template = "Explica <tema>.", Variables = new() { ["tema"] = "PRIVATE_DEFAULT" } });
        module.Store.Compile(source.Id, new() { ["tema"] = "PRIVATE_HISTORY" });
        var before = module.Archives.Export();
        IPromptSharing sharing = module.Sharing;
        var packet = sharing.Export(source.Id, source.Revision);
        Assert.Equal(module.Store.ExportTemplate(source.Id, source.Revision), packet);
        foreach (var privateValue in new[] { "PRIVATE_DEFAULT", "PRIVATE_HISTORY", source.Id.ToString(), "variables", "history" }) Assert.DoesNotContain(privateValue, packet);
        Assert.Contains("PRIVATE_HISTORY", before);
        Assert.Equal(before, module.Archives.Export());
        source.Template = "Compara <tema>."; module.Store.Save(source);
        Assert.Throws<ConflictException>(() => sharing.Export(source.Id, source.Revision - 1));
        Assert.Throws<MissingPromptException>(() => sharing.Export(Guid.NewGuid(), 1));
    }

    [Fact]
    public void ImportAndRestorePortsPreserveConfirmationSnapshotsAndVerifiedBackup()
    {
        var module = Module(); var target = Module("target");
        var original = module.Prompts.Create(new("Original", "Explica <tema>."));
        var packet = Document(module.Sharing.Export(original.Id, original.Revision));
        ILibraryArchiveReader read = target.Archives; ILibraryArchiveWriter write = target.Archives;
        Assert.Equal(1, read.Inspect(packet).NewTemplates);
        Assert.Empty(target.Store.Templates());
        Assert.Throws<ValidationException>(() => write.Import(new(packet, false)));
        Assert.Equal(1, write.Import(new(packet, true)));
        Assert.Equal(0, write.Import(new(packet, true)));
        var archive = Document(module.Archives.Export());
        var preview = read.InspectRestore(archive);
        var later = target.Prompts.Create(new("Posterior", "No se debe perder sin revisión."));
        var before = read.Export();
        Assert.Throws<ValidationException>(() => write.Restore(new(archive, false, preview.ExpectedSnapshot)));
        Assert.Throws<ConflictException>(() => write.Restore(new(archive, true, preview.ExpectedSnapshot)));
        Assert.Equal(before, read.Export());
        Assert.False(Directory.Exists(Path.Combine(directory, "backups")));
        var fresh = read.InspectRestore(archive);
        Assert.Equal(1, write.Restore(new(archive, true, fresh.ExpectedSnapshot)));
        Assert.Equal(original.Id, Assert.Single(target.Store.Templates()).Id);
        var path = Assert.Single(Directory.GetFiles(Path.Combine(directory, "backups"), "*.sqlite"));
        var backup = new LocalStore(path);
        Assert.NotNull(backup.FindTemplate(later.Id)); Assert.Equal("ok", backup.CheckIntegrity());
        Assert.Equal("ok", write.Backup().Integrity);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "backups"), "*.sqlite").Length);
    }

    [Fact]
    public void RestoreThroughPortStillRejectsActiveRunsAndPreservesReplayReceipts()
    {
        var module = Module(); var records = new LiveRunStore(module.Store); var live = new LiveRuns(records, module.Prompts);
        var archive = Document(module.Archives.Export());
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Petición sintética"); live.Create(input);
        var snapshot = module.Archives.InspectRestore(archive).ExpectedSnapshot;
        Assert.Throws<ConflictException>(() => module.Archives.Restore(new(archive, true, snapshot)));
        live.Cancel(input.RunId);
        snapshot = module.Archives.InspectRestore(archive).ExpectedSnapshot;
        module.Archives.Restore(new(archive, true, snapshot));
        Assert.False(records.TryCreate(new(input.RunId, input.SessionId, "different", "BROWSER", LiveStatus.CREATED, 0, DateTime.UtcNow)));
    }

    [Fact]
    public async Task LegacyConversionIsBoundedForUnknownLengthStreamsAndPropagatesCancellation()
    {
        var temporaryBefore = Directory.GetFiles(Path.GetTempPath(), "quickprompt-import-*.sqlite").ToHashSet();
        ILegacyArchiveConverter converter = new LegacyArchiveConverter();
        using var oversized = new GeneratedStream();
        await Assert.ThrowsAsync<ValidationException>(() => converter.Convert(oversized, default));
        Assert.Equal(converter.MaximumBytes + 1, oversized.ReadBytes);
        Assert.False(oversized.Disposed);
        using var cancellation = new CancellationTokenSource(); using var interrupted = new GeneratedStream(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.Convert(interrupted, cancellation.Token));
        Assert.InRange(interrupted.ReadBytes, 1, 65536); Assert.False(interrupted.Disposed);
        using var preCancelled = new CancellationTokenSource(); preCancelled.Cancel(); using var untouched = new GeneratedStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.Convert(untouched, preCancelled.Token));
        Assert.Equal(0, untouched.ReadBytes);
        Assert.DoesNotContain(Directory.GetFiles(Path.GetTempPath(), "quickprompt-import-*.sqlite"), path => !temporaryBefore.Contains(path));
    }

    [Fact]
    public async Task LegacyPortConvertsRealSqliteWithoutMutatingSourceAndKeepsAdditiveImport()
    {
        var module = Module(); var path = Path.Combine(directory, "legacy.sqlite");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False")) {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "CREATE TABLE PromptTemplate(Id TEXT,Title TEXT,Template TEXT,Description TEXT,Category INTEGER,VariablesJson TEXT,IsFavorite INTEGER); INSERT INTO PromptTemplate VALUES($id,'Legado','Hola <nombre>','Prueba',0,'{}',1)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(path);
        await using var source = File.OpenRead(path);
        var archive = Document(await module.LegacyConverter.Convert(source, default));
        Assert.True(source.CanRead); Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(1, module.Archives.Inspect(archive).NewTemplates);
        Assert.Equal(1, module.Archives.Import(new(archive, true)));
        Assert.Equal(0, module.Archives.Import(new(archive, true)));
        Assert.True(Assert.Single(module.Store.Templates()).IsFavorite);
    }

    private sealed class GeneratedStream(CancellationTokenSource? cancellation = null) : Stream
    {
        public int ReadBytes { get; private set; }
        public bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); buffer.Span.Clear(); ReadBytes += buffer.Length;
            cancellation?.Cancel(); return ValueTask.FromResult(buffer.Length);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
