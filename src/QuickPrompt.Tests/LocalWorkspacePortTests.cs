using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class LocalWorkspacePortTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "local-workspace-ports-" + Guid.NewGuid());
    private LocalPromptLibrary Module() => new(Path.Combine(directory, "synthetic.sqlite"));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    private static Template Copy(Template value) => JsonSerializer.Deserialize<Template>(JsonSerializer.Serialize(value))!;

    [Fact]
    public void FactoryAliasesShareOneOwnerAndAuthoringRetainsOptimisticConcurrency()
    {
        var module = Module(); ITemplateAuthoring authoring = module.Authoring; IPromptReader read = module.Templates;
        Assert.Same(module.Store, authoring); Assert.Same(module.Store, module.Compiled); Assert.Same(module.Store, module.Compilation);
        Assert.Same(module.Store, module.Settings); Assert.Same(module.Store, module.Proposals);
        var source = authoring.Save(new() { Title = "Original", Template = "Hola <nombre>" });
        Assert.Equal(1, read.ActiveCount);
        var stale = read.Find(source.Id)!; source.Title = "Revisada"; var updated = authoring.Save(source);
        Assert.Equal(2, updated.Revision); Assert.Equal(2, read.Versions(source.Id).Length);
        Assert.Throws<ConflictException>(() => authoring.Save(stale));
        Assert.Throws<ConflictException>(() => module.TemplateWriter.Delete(source.Id, stale.Revision));
        Assert.Equal("Revisada", read.Find(source.Id)!.Title);
        module.TemplateWriter.Delete(source.Id, updated.Revision); Assert.Null(read.Find(source.Id)); Assert.Empty(read.Versions(source.Id));
    }

    [Fact]
    public void PreviewRemainsReadOnlyAndCompilePreservesCompositionPrivacyAndHistory()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Composición", Template = "Hola <nombre>" });
        module.Settings.SaveSettings(new(GeneralInstructions: "No inventes datos."));
        var profile = module.Store.SaveInstruction(new() { Name = "Perfil", Text = "Usa frases breves." });
        var before = module.Archives.Export(); var values = new Dictionary<string, string> { ["nombre"] = "PRIVATE_VALUE" };
        var preview = module.Compiled.Preview(source.Id, values, profile.Id, "Termina con una pregunta.");
        Assert.Equal(before, module.Archives.Export()); Assert.Empty(module.Compiled.History());
        var compiled = module.Compilation.Compile(source.Id, values, profile.Id, "Termina con una pregunta.", source.Revision);
        Assert.Equal(preview.Text, compiled.Text); Assert.Equal(profile.Id, compiled.Composition!.InstructionId);
        Assert.Empty(compiled.Values); Assert.Single(module.Compiled.History()); Assert.Equal(1, module.Templates.Find(source.Id)!.Uses);
        module.Settings.SaveSettings(new(RememberValues: true));
        var remembered = module.Compilation.Compile(source.Id, values, expectedRevision: source.Revision); Assert.Equal(values, remembered.Values);
        var latest = module.Templates.Find(source.Id)!; latest.Template = "Adiós <nombre>"; module.Authoring.Save(latest);
        var afterChange = module.Archives.Export();
        Assert.Throws<ConflictException>(() => module.Compilation.Compile(source.Id, values, expectedRevision: source.Revision));
        Assert.Equal(afterChange, module.Archives.Export());
    }

    [Fact]
    public void ProposalLifecycleAndAuthoringKeepApprovalAtomicAndStagingSeparateFromSaving()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Propuesta", Template = "Hola <nombre>" });
        var proposal = new AiProposal(Guid.NewGuid(), source.Id, source.Revision, "Saludos <nombre>", [], [], [], ["nombre"], "qwen3:8b");
        module.Proposals.SaveProposal(proposal);
        var staged = module.Proposals.StageDraftImprovement(proposal.Id, Copy(source));
        Assert.Equal(proposal.Text, staged.Template); Assert.Equal(source.Template, module.Templates.Find(source.Id)!.Template); Assert.Single(module.Templates.Versions(source.Id));
        var pending = proposal with { Id = Guid.NewGuid() }; module.Proposals.SaveProposal(pending);
        var accepted = Copy(source); accepted.Template = pending.Text; module.Authoring.Save(accepted, "AI accepted", pending.Id);
        Assert.Equal(pending.Text, module.Templates.Find(source.Id)!.Template); Assert.Equal(2, module.Templates.Versions(source.Id).Length);
        var beforeReplay = module.Archives.Export(); Assert.Throws<ConflictException>(() => module.Authoring.Save(accepted, "AI accepted", pending.Id)); Assert.Equal(beforeReplay, module.Archives.Export());
    }

    [Fact]
    public void HistoryCommandsPreserveTemplatesAndInvalidSettingsCannotChangeStoredPreferences()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Historial", Template = "Texto sin variables." });
        var compiled = module.Compilation.Compile(source.Id, [], expectedRevision: source.Revision);
        module.Compilation.SaveExecution(new(Guid.NewGuid(), compiled.Id, "qwen3:8b", "Respuesta sintética", 1, DateTime.UtcNow));
        Assert.Single(module.Compiled.Executions()); module.Compilation.FavoriteFinal(compiled); Assert.True(Assert.Single(module.Compiled.History()).Favorite);
        var before = module.Settings.Settings(); Assert.Throws<ValidationException>(() => module.Settings.SaveSettings(new(Language: "invalid"))); Assert.Equal(before, module.Settings.Settings());
        module.Compilation.ClearHistory(); Assert.Empty(module.Compiled.History()); Assert.Empty(module.Compiled.Executions()); Assert.NotNull(module.Templates.Find(source.Id));
    }
}
