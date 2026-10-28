using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using System.Text.Json;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptVersionTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "nova-versions-" + Guid.NewGuid(), "synthetic.sqlite");
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    public PromptVersionTests() { store = new(path); var adapter = new NovaPromptStore(store); library = new(adapter, adapter); }

    [Fact]
    public void RestoreUsesTheServerSnapshotAndAppendsHistoryWhileKeepingCurrentOperationalState()
    {
        var original = library.Create(new("Órbita 🛰️", "Explica <tema>.\nNo inventes datos.", "Primera descripción", 3,
            Defaults: new() { ["tema"] = "ciencia α\nβ" }));
        var edited = library.Update(original.Id, new("Actual", "Revisa <material>.", "Otra descripción", 0,
            original.Revision, new() { ["material"] = "código" }));
        var favorite = library.Favorite(edited.Id, edited.Revision);
        var compiled = library.Compile(favorite.Id, new(favorite.Revision, []));
        library.RecordUse(favorite.Id, new(favorite.Revision, [], Guid.NewGuid(), compiled.Text));
        var archived = library.Archive(favorite.Id, favorite.Revision, true);
        var before = store.Versions(original.Id);
        var restored = library.RestoreVersion(original.Id, original.Revision, new(archived.Revision, true));
        Assert.Equal(original.Id, restored.Id); Assert.Equal(archived.Revision + 1, restored.Revision);
        Assert.Equal(original.Title, restored.Title); Assert.Equal(original.TemplateText, restored.TemplateText);
        Assert.Equal(original.Description, restored.Description); Assert.Equal(original.CategoryId, restored.CategoryId);
        Assert.Equal(original.Defaults, restored.Defaults);
        Assert.True(restored.IsFavorite); Assert.True(restored.IsArchived);
        Assert.Equal(1, restored.Uses); Assert.Equal(archived.CreatedAt, restored.CreatedAt);
        Assert.Equal(before.Count + 1, store.Versions(original.Id).Count);
        Assert.Equal(before.Select(version => version.Id), store.Versions(original.Id).Skip(1).Select(version => version.Id));
        var restarted = new NovaPromptStore(new LocalStore(path));
        Assert.Equal(JsonSerializer.Serialize(restored), JsonSerializer.Serialize(new PromptLibrary(restarted, restarted).Get(restored.Id)));
        Assert.Equal("ok", store.CheckIntegrity());
    }

    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("current")]
    [InlineData("missing")]
    [InlineData("stale")]
    public void RejectedRestoreNeverMutatesDataOrAddsHistory(string failure)
    {
        var original = library.Create(new("Anterior", "Texto anterior."));
        var current = library.Update(original.Id, new("Actual", "Texto actual.", Revision: original.Revision));
        var expectedBeforeConcurrentChange = current.Revision;
        if (failure == "missing") current = library.Update(current.Id, new(current.Title, current.TemplateText, Revision: current.Revision));
        if (failure == "stale") current = library.Favorite(current.Id, current.Revision);
        var before = store.Export();
        var targetRevision = failure == "current" ? current.Revision : failure == "missing" ? 999 : original.Revision;
        var expectedRevision = failure == "stale" ? expectedBeforeConcurrentChange : failure == "missing" ? 1000 : current.Revision;
        var confirmed = failure != "unconfirmed";
        void Restore() => library.RestoreVersion(current.Id, targetRevision, new(expectedRevision, confirmed));
        if (failure == "stale") Assert.Throws<ConflictException>(Restore);
        else if (failure == "missing") Assert.Throws<MissingPromptException>(Restore);
        else Assert.Throws<ValidationException>(Restore);
        Assert.Equal(before, store.Export());
    }

    public void Dispose() {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var directory = Path.GetFullPath(Path.GetDirectoryName(path)!);
        if (!directory.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(directory).StartsWith("nova-versions-", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid fixture cleanup target.");
        Directory.Delete(directory, true);
    }
}
