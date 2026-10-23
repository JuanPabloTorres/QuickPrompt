using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ProviderPortTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-provider-ports-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact]
    public void TransfersAndExecutionReviewPreserveApprovalRevisionExactTextAndReadOnlyEffects()
    {
        var module = new LocalPromptLibrary(Path.Combine(directory, "synthetic.sqlite"));
        var prompt = module.Prompts.Create(new("Sintético", "Explica <tema>.")); var values = new Dictionary<string, string> { ["tema"] = "α" };
        var before = module.Archives.Export(); IPromptTransfer transfer = module.Transfers;
        Assert.Throws<ValidationException>(() => transfer.Prepare(new(prompt.Id, 1, values, "chatgpt", false)));
        Assert.Throws<ValidationException>(() => transfer.Prepare(new(prompt.Id, 1, values, "ollama", true)));
        Assert.Throws<ValidationException>(() => transfer.Prepare(new(prompt.Id, 1, values, "https://untrusted.invalid", true)));
        Assert.Throws<ConflictException>(() => transfer.Prepare(new(prompt.Id, 2, values, "chatgpt", true)));
        var prepared = transfer.Prepare(new(prompt.Id, 1, values, "chatgpt", true));
        Assert.Equal("Explica α.", prepared.Text); Assert.Equal("https://chatgpt.com/", prepared.DestinationUrl); Assert.Equal(1, prepared.TemplateRevision);
        Assert.Equal("COPY_AND_OPEN", prepared.Mode);
        ExecuteInput input = new(prompt.Id, 1, values, "qwen3:8b", true, prepared.Text);
        Assert.Throws<ValidationException>(() => module.Prompts.ReviewExecution(input with { Confirmed = false }));
        Assert.Throws<ConflictException>(() => module.Prompts.ReviewExecution(input with { Revision = 2 }));
        Assert.Throws<ConflictException>(() => module.Prompts.ReviewExecution(input with { ExpectedText = "Otra cosa" }));
        Assert.Throws<ValidationException>(() => module.Prompts.ReviewExecution(input with { Values = new() { ["unknown"] = "no" } }));
        var compiled = module.Prompts.ReviewExecution(input); Assert.Equal(prepared.Text, compiled.Text); Assert.Equal(1, compiled.Revision);
        Assert.Equal(before, module.Archives.Export());
    }

    [Fact]
    public void PreferenceOwnerRejectsUnapprovedAndInvalidChoicesAndPersistsOnlyConfirmedValues()
    {
        var path = Path.Combine(directory, "synthetic.sqlite"); var store = new LocalStore(path);
        IPreparationPreferences preferences = new PreparationPreferenceStore(store); var before = store.NovaSnapshot();
        Assert.Throws<ValidationException>(() => preferences.Confirm(new(new("en", "JSON", "chatgpt"), false)));
        Assert.Throws<ValidationException>(() => preferences.Confirm(new(null!, true)));
        Assert.Throws<ValidationException>(() => preferences.Confirm(new(new("fr", "SQL", "unknown"), true)));
        Assert.Equal(before, store.NovaSnapshot());
        var approved = new PreparationPreferences("en", "JSON", "chatgpt"); Assert.Equal(approved, preferences.Confirm(new(approved, true)));
        IPreparationPreferences restarted = new PreparationPreferenceStore(new LocalStore(path)); Assert.Equal(approved, restarted.Get());
        restarted.Forget(); Assert.Equal(new PreparationPreferences(), preferences.Get());
    }
}
