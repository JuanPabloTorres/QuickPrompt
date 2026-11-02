using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptRenderingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-render-" + Guid.NewGuid());
    [Fact]
    public void ExternalRenderingUsesOnlyExplicitValuesAndLeavesPrivateSettingsDefaultsAndHistoryUntouched()
    {
        var store = new LocalStore(Path.Combine(directory, "library.sqlite"));
        var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
        var prompt = library.Create(new("Tutor", "Explica <tema> para <nivel>.", Defaults: new()
        { ["tema"] = "PRIVATE_DEFAULT", ["nivel"] = "PRIVATE_LEVEL" }));
        var preferences = store.Settings() with { GeneralInstructions = "PRIVATE_GENERAL_INSTRUCTIONS" };
        store.SaveSettings(preferences);
        var missing = library.Render(prompt.Id, new(1, []));
        Assert.Null(missing.Text); Assert.Equal(new[] { "tema", "nivel" }, missing.MissingVariables);
        var rendered = library.Render(prompt.Id, new(1, new() { ["tema"] = "MCP", ["nivel"] = "principiante" }));
        Assert.Equal("Explica MCP para principiante.", rendered.Text);
        Assert.Empty(rendered.MissingVariables);
        var saved = library.Get(prompt.Id);
        Assert.Equal(prompt.TemplateText, saved.TemplateText); Assert.Equal(0, saved.Uses); Assert.Equal(1, saved.Revision);
        Assert.Equal("PRIVATE_DEFAULT", saved.Defaults["tema"]);
        Assert.Equal("PRIVATE_GENERAL_INSTRUCTIONS", store.Settings().GeneralInstructions);
        Assert.Single(store.Versions(prompt.Id)); Assert.Empty(store.History()); Assert.Empty(store.Executions());
        library.Archive(prompt.Id, 1, true);
        Assert.Throws<MissingPromptException>(() => library.Render(prompt.Id, new(2, new())));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
