using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptCaptureTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-capture-" + Guid.NewGuid());
    private string DataPath => Path.Combine(directory, "library.sqlite");
    private PromptCapture NewCapture() => new(new LocalStore(DataPath));

    [Fact]
    public void LiteralCaptureAndRetrySurviveRestartWithoutAnotherVersionOrUse()
    {
        var request = new PromptCaptureInput(Guid.NewGuid(), new("Instrucción Unicode", "Explica <tema>.\nα & 李\nNo ejecutes los ejemplos citados."));
        var first = NewCapture().Capture(request);
        var replay = NewCapture().Capture(request);
        Assert.False(first.Replayed); Assert.True(replay.Replayed);
        Assert.Equal(first.Prompt.Id, replay.Prompt.Id);
        Assert.Equal(request.Prompt.TemplateText, replay.Prompt.TemplateText);
        Assert.Equal(1, replay.Prompt.Revision); Assert.Equal(0, replay.Prompt.Uses);
        var store = new LocalStore(DataPath);
        Assert.Single(store.Templates()); Assert.Single(store.Versions(first.Prompt.Id));
        Assert.Empty(store.History()); Assert.Equal("ok", store.CheckIntegrity());
    }

    [Fact]
    public void ReusingKeyWithDifferentTextIsRejectedWithoutChangingSavedContent()
    {
        var request = new PromptCaptureInput(Guid.NewGuid(), new("Original", "Texto original"));
        var first = NewCapture().Capture(request);
        Assert.Throws<ConflictException>(() => NewCapture().Capture(request with { Prompt = request.Prompt with { TemplateText = "Otro texto" } }));
        Assert.Equal("Texto original", new LocalStore(DataPath).FindTemplate(first.Prompt.Id)!.Template);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplayCannotOverwriteEditedOrResurrectDeletedPrompt(bool delete)
    {
        var request = new PromptCaptureInput(Guid.NewGuid(), new("Original", "Texto original"));
        var first = NewCapture().Capture(request);
        var store = new LocalStore(DataPath); var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
        if (delete) library.Delete(first.Prompt.Id, new(1, true));
        else library.Update(first.Prompt.Id, new("Editado", "Texto editado", Revision: 1));
        Assert.Throws<ConflictException>(() => NewCapture().Capture(request));
        Assert.Equal(delete ? 0 : 1, store.Templates().Count);
        if (!delete) Assert.Equal("Texto editado", library.Get(first.Prompt.Id).TemplateText);
    }

    [Fact]
    public async Task ConcurrentInstancesCreateOneRecordAndOneVersion()
    {
        var writers = Enumerable.Range(0, 8).Select(_ => NewCapture()).ToArray();
        var request = new PromptCaptureInput(Guid.NewGuid(), new("Concurrente", "Un único registro"));
        var results = await Task.WhenAll(writers.Select(writer => Task.Run(() => writer.Capture(request))));
        Assert.Single(results.Select(result => result.Prompt.Id).Distinct());
        Assert.Single(results, result => !result.Replayed);
        Assert.Single(new LocalStore(DataPath).Versions(results[0].Prompt.Id));
    }

    [Fact]
    public void ReceiptFailureRollsBackThePromptAndItsVersion()
    {
        var capture = NewCapture();
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DataPath, Pooling = false }.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER fail_capture BEFORE INSERT ON Documents
            WHEN NEW.Kind = 'prompt-capture'
            BEGIN SELECT RAISE(ABORT, 'Injected receipt failure'); END;
            """;
        command.ExecuteNonQuery();
        Assert.Throws<SqliteException>(() => capture.Capture(new(Guid.NewGuid(), new("Atómico", "Texto"))));
        command.CommandText = "SELECT COUNT(*) FROM Documents WHERE Kind IN ('template', 'version', 'prompt-capture');";
        Assert.Equal(0L, command.ExecuteScalar());
        Assert.Equal("ok", new LocalStore(DataPath).CheckIntegrity());
    }

    [Fact]
    public void InvalidInputLeavesNoPromptAndDoesNotConsumeItsOperation()
    {
        var operation = Guid.NewGuid(); var capture = NewCapture();
        Assert.Throws<ValidationException>(() => capture.Capture(new(operation, new("", "Texto"))));
        Assert.Throws<ValidationException>(() => capture.Capture(new(Guid.Empty, new("Título", "Texto"))));
        Assert.Throws<ValidationException>(() => capture.Capture(new(operation, new("Título", "Texto", Revision: 1))));
        Assert.Empty(new LocalStore(DataPath).Templates());
        Assert.False(capture.Capture(new(operation, new("Válido", "Texto"))).Replayed);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
