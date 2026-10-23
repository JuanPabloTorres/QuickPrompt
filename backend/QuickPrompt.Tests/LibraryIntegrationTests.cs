using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Nova.Tests;

public sealed class LibraryIntegrationTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "nova-tests-" + Guid.NewGuid(), "library.sqlite");
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    public LibraryIntegrationTests()
    {
        store = new(path); var adapter = new NovaPromptStore(store); library = new(adapter, adapter);
    }
    [Fact] public void CategoryCatalogueUsesPersistedDomainIdsAndSurvivesEditAndArchiveRoundTrip()
    {
        var categories = PromptCategories.All;
        Assert.Equal(Enum.GetValues<PromptCategory>().Select(value => (int)value), categories.Select(value => value.Id));
        Assert.All(categories, category => Assert.False(string.IsNullOrWhiteSpace(category.Name)));
        foreach (var category in categories)
        {
            var prompt = library.Create(new("Categoría " + category.Name, "Explica <tema>.", CategoryId: category.Id));
            Assert.Equal(category.Id, prompt.CategoryId);
        }
        var original = library.Search(new(Size: 100)).Items.First();
        var changed = library.Update(original.Id, new(original.Title, original.TemplateText, CategoryId: (int)PromptCategory.Education, Revision: original.Revision));
        Assert.Equal((int)PromptCategory.Education, changed.CategoryId);
        Assert.Throws<ValidationException>(() => library.Update(changed.Id, new(changed.Title, changed.TemplateText, CategoryId: 99999, Revision: changed.Revision)));
        Assert.Equal(changed.Revision, library.Get(changed.Id).Revision);
        var targetStore = new LocalStore(Path.Combine(Path.GetDirectoryName(path)!, "categories-restored.sqlite"));
        targetStore.Import(store.Export(), true);
        var target = new NovaPromptStore(targetStore); var restored = new PromptLibrary(target, target);
        foreach (var prompt in library.Search(new(Size: 100)).Items)
            Assert.Equal(prompt.CategoryId, restored.Get(prompt.Id).CategoryId);
        Assert.Equal("ok", targetStore.CheckIntegrity());
    }
    [Fact] public void PreviewIsReadOnlyAndCopyUsageSurvivesRestartExactlyOnce()
    {
        var prompt = library.Create(new("Unicode", "Hola <niño>\n<niño> · \\<literal\\>"));
        var values = new Dictionary<string, string> { ["niño"] = "α\nβ <niño>" };
        var before = store.Export();
        var compiled = library.Compile(prompt.Id, new(prompt.Revision, values));
        Assert.Equal("Hola α\nβ <niño>\nα\nβ <niño> · <literal>", compiled.Text);
        Assert.Equal(before, store.Export());
        var use = new UseInput(prompt.Revision, values, Guid.NewGuid(), compiled.Text);
        library.RecordUse(prompt.Id, use);
        var restarted = new LocalStore(path); var adapter = new NovaPromptStore(restarted); var reopened = new PromptLibrary(adapter, adapter);
        reopened.RecordUse(prompt.Id, use);
        Assert.Equal(1, reopened.Get(prompt.Id).Uses);
        Assert.Single(restarted.History());
        Assert.Empty(restarted.History()[0].Values);
        Assert.Equal(prompt.TemplateText, reopened.Get(prompt.Id).TemplateText);
    }
    [Fact] public void ConcurrentEditCannotOverwriteFavoriteOrArchive()
    {
        var prompt = library.Create(new("Original", "Texto"));
        var favorite = library.Favorite(prompt.Id, prompt.Revision);
        Assert.Throws<ConflictException>(() => library.Update(prompt.Id, new("Stale", "Changed", Revision: prompt.Revision)));
        var archived = library.Archive(prompt.Id, favorite.Revision, true);
        Assert.Empty(library.Search(new()).Items);
        Assert.Throws<ValidationException>(() => library.Compile(prompt.Id, new(archived.Revision, new())));
        Assert.True(library.Archive(prompt.Id, archived.Revision, false).IsFavorite);
    }
    [Fact] public void DeleteRequiresBothConfirmationAndCurrentRevision()
    {
        var prompt = library.Create(new("Keep", "Text"));
        Assert.Throws<ValidationException>(() => library.Delete(prompt.Id, new(prompt.Revision, false)));
        Assert.Throws<ConflictException>(() => library.Delete(prompt.Id, new(999, true)));
        Assert.Equal(prompt, library.Get(prompt.Id) with { Defaults = prompt.Defaults, VariableNames = prompt.VariableNames });
        library.Delete(prompt.Id, new(prompt.Revision, true));
        Assert.Throws<MissingPromptException>(() => library.Get(prompt.Id));
    }
    [Fact] public void DefaultValuesAndCategoriesSurviveReactEditsAndDuplication()
    {
        var prompt = library.Create(new("Title", "Explain <topic>", CategoryId: 3, Defaults: new() { ["topic"] = "Unicode" }));
        var edited = library.Update(prompt.Id, new("New title", "Explain <topic> carefully", CategoryId: 3, Revision: prompt.Revision));
        Assert.Equal("Explain Unicode carefully", library.Compile(prompt.Id, new(edited.Revision, new())).Text);
        var duplicate = library.Duplicate(prompt.Id);
        Assert.NotEqual(prompt.Id, duplicate.Id); Assert.Equal(3, duplicate.CategoryId); Assert.Equal("Unicode", duplicate.Defaults["topic"]);
        Assert.Equal(2, store.Versions(prompt.Id).Count);
    }
    [Fact] public void UseReceiptCannotBeReplayedWithDifferentContent()
    {
        var prompt = library.Create(new("Safe", "<x>"));
        var use = new UseInput(prompt.Revision, new() { ["x"] = "one" }, Guid.NewGuid(), "one");
        library.RecordUse(prompt.Id, use);
        Assert.Throws<ConflictException>(() => library.RecordUse(prompt.Id, use with { Values = new() { ["x"] = "two" } }));
        Assert.Equal(1, library.Get(prompt.Id).Uses);
    }
    [Fact] public void LegacyBackupRoundTripPreservesGroupsProfilesAndRevisions()
    {
        var prompt = library.Create(new("Saved", "<x>"));
        var group = store.SaveGroup(new() { Name = "Trabajo" }); store.AssignGroup(group.Id, "template", [prompt.Id]);
        store.SaveInstruction(new() { Name = "Directo", Text = "Responde brevemente" });
        var backup = store.Export();
        var target = new LocalStore(Path.Combine(Path.GetDirectoryName(path)!, "restored.sqlite"));
        target.Import(backup, true);
        Assert.Equal(backup, target.Export()); Assert.Equal("ok", target.CheckIntegrity());
        Assert.Contains(group.Id, target.GroupsFor("template", prompt.Id));
    }
    [Fact] public void ChangedGlobalInstructionsCannotRecordADifferentTextThanTheClipboard()
    {
        var prompt = library.Create(new("Snapshot", "Original"));
        var text = library.Compile(prompt.Id, new(prompt.Revision, new())).Text;
        store.SaveSettings(store.Settings() with { GeneralInstructions = "Nueva instrucción" });
        Assert.Throws<ConflictException>(() => library.RecordUse(prompt.Id, new(prompt.Revision, new(), Guid.NewGuid(), text)));
        Assert.Empty(store.History()); Assert.Equal(0, library.Get(prompt.Id).Uses);
    }
    [Fact] public void RestoreRequiresFreshFingerprintAndMakesVerifiedBackupBeforeReplacing()
    {
        library.Create(new("First", "One"));
        var archive = store.Export(); var snapshot = store.NovaSnapshot();
        var second = library.Create(new("Second", "Two"));
        var data = System.Text.Json.JsonSerializer.SerializeToElement(System.Text.Json.JsonSerializer.Deserialize<object>(archive));
        Assert.Throws<ConflictException>(() => store.NovaRestore(new(data, true, snapshot)));
        Assert.Equal("Second", library.Get(second.Id).Title);
        store.NovaRestore(new(data, true, store.NovaSnapshot()));
        Assert.Equal(archive, store.Export());
        var backup = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "backups"), "*.sqlite").Single();
        var backed = new LocalStore(backup);
        Assert.Equal("ok", backed.CheckIntegrity()); Assert.Equal(2, backed.Templates().Count);
    }
    [Fact] public void DeletingCollectionPreservesPromptAndRequiresCurrentRevision()
    {
        var prompt = library.Create(new("Prompt", "Text"));
        var group = store.SaveGroup(new() { Name = "One" });
        store.NovaAssign(prompt.Id, new(group.Id, prompt.Revision, true));
        var renamed = store.NovaRenameGroup(group.Id, new("Two", group.Revision));
        Assert.Throws<ConflictException>(() => store.NovaDeleteGroup(group.Id, new(group.Revision, true)));
        store.NovaDeleteGroup(group.Id, new(renamed.Revision, true));
        Assert.Empty(store.GroupsFor("template", prompt.Id)); Assert.NotNull(library.Get(prompt.Id));
    }
    [Fact] public void EditCannotInferAMissingRevision()
    {
        var prompt = library.Create(new("Keep", "Original"));
        Assert.Throws<ValidationException>(() => library.Update(prompt.Id, new("Overwrite", "Changed")));
        Assert.Equal("Original", library.Get(prompt.Id).TemplateText);
    }
    [Fact] public void DirectLegacyDatabasePathIsRejectedWithoutChangingItsBytes()
    {
        var legacy = Path.Combine(Path.GetDirectoryName(path)!, "legacy.sqlite");
        using (var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={legacy};Pooling=False"))
        {
            db.Open(); using var command = db.CreateCommand(); command.CommandText = "CREATE TABLE PromptTemplate(Id TEXT,Title TEXT); INSERT INTO PromptTemplate VALUES('synthetic','Synthetic legacy');"; command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(legacy);
        Assert.Throws<ValidationException>(() => NovaDatabaseGuard.Validate(legacy));
        Assert.Equal(before, File.ReadAllBytes(legacy)); NovaDatabaseGuard.Validate(path);
    }
    [Fact] public async Task NovaRecommendsOnlyRetrievedIdsAndCannotChangeTheLibrary()
    {
        var prompt = library.Create(new("Arquitectura", "Revisar dependencias"));
        var adapter = new NovaPromptStore(store); var inference = new FakeInference(new(NovaIntent.SEARCH, "arquitectura", 1));
        var assistant = new NovaAssistant(adapter, inference); var before = store.Export();
        var action = await assistant.Interpret(new("Find a prompt"), "correlation", default);
        Assert.Equal([prompt.Id], action.SourcePromptIds); Assert.Equal(prompt.Id, action.Candidates.Single().Id);
        Assert.Equal(before, store.Export());
        var empty = new NovaAssistant(adapter, new FakeInference(new(NovaIntent.SEARCH, "missing", 1)));
        Assert.Empty((await empty.Interpret(new("Find another"), "correlation", default)).SourcePromptIds);
    }
    [Fact] public async Task NovaRejectsUnknownIntentsAndOversizedPlans()
    {
        var adapter = new NovaPromptStore(store);
        foreach (var plan in new[] { new NovaPlan((NovaIntent)999, "", 1), new NovaPlan(NovaIntent.SEARCH, "word", 4) })
        {
            var assistant = new NovaAssistant(adapter, new FakeInference(plan));
            await Assert.ThrowsAsync<ValidationException>(() => assistant.Interpret(new("Untrusted request"), "id", default));
        }
        Assert.Empty(store.Templates());
    }
    private sealed class FakeInference(NovaPlan plan) : INovaInference
    {
        public Task<NovaPlan> Interpret(string message, string model, CancellationToken ct) => Task.FromResult(plan);
        public Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> Suggest(Template source, string context, string model, CancellationToken ct) => throw new NotSupportedException();
    }
    public void Dispose() => Directory.Delete(Path.GetDirectoryName(path)!, true);
}
