using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class RuntimePortTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "runtime-ports-" + Guid.NewGuid());
    private LocalPromptLibrary Module() => new(Path.Combine(directory, "synthetic.sqlite"));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact]
    public void ContractsBelongToApplicationAndOldAssemblyNamesResolveThroughForwarders()
    {
        foreach (var type in new[] { typeof(ILocalLlmClient), typeof(TemplateEditInterpretation), typeof(LlmUnavailableException), typeof(IWebPromptTransfer) }) Assert.Same(typeof(PromptLibrary).Assembly, type.Assembly);
        Assert.Same(typeof(ILocalLlmClient), Type.GetType("QuickPrompt.Web.Infrastructure.ILocalLlmClient, QuickPrompt.Infrastructure", true));
        Assert.Same(typeof(TemplateEditInterpretation), Type.GetType("QuickPrompt.Web.Infrastructure.TemplateEditInterpretation, QuickPrompt.Infrastructure", true));
        Assert.Same(typeof(LlmUnavailableException), Type.GetType("QuickPrompt.Web.Infrastructure.LlmUnavailableException, QuickPrompt.Infrastructure", true));
        Assert.Same(typeof(IWebPromptTransfer), Type.GetType("QuickPrompt.Web.Infrastructure.IWebPromptTransfer, QuickPrompt.Web", true));
    }

    [Fact]
    public void PanelCanUseSubstitutedPortsWithoutPersistenceAndItsProjectionDoesNotExportDefaults()
    {
        var source = new PanelPorts(); var panel = new PromptPanel(source, source);
        var page = panel.Library("Fuente", false, 0); var json = JsonSerializer.Serialize(page);
        Assert.Single(page.Items); Assert.Equal(["nombre"], page.Items[0].Variables); Assert.DoesNotContain("PRIVATE_DEFAULT", json); Assert.Equal(0, source.Compiles);
        Assert.Throws<ValidationException>(() => panel.Compile(source.Source.Id, source.Source.Revision, new() { ["unknown"] = "x" })); Assert.Equal(0, source.Compiles);
        Assert.Throws<ConflictException>(() => panel.Compile(source.Source.Id, 1, new() { ["nombre"] = "Ana" })); Assert.Equal(0, source.Compiles);
        var result = panel.Compile(source.Source.Id, 2, new() { ["nombre"] = "Ana" }); Assert.Equal("Hola Ana.", result.Text); Assert.Equal(1, source.Compiles); Assert.Equal(2, source.ExpectedRevision);
    }

    [Fact]
    public void WorkflowPortDoesNotWriteAtPreparationAndRejectsStalePlansBeforeAnyEffect()
    {
        var module = Module(); Assert.Same(module.Store, module.Assist);
        var source = module.Authoring.Save(new() { Title = "Fuente", Template = "Texto." });
        var beforePrepare = module.Archives.Export();
        var plan = module.Assist.PrepareAssist(new([new(AssistAction.ProposeGroup, "Grupo preparado"), new(AssistAction.Favorite)], "ollama"), source.Id);
        Assert.Equal(beforePrepare, module.Archives.Export());
        var updated = module.Templates.Find(source.Id)!; updated.Title = "Actualizada"; module.Authoring.Save(updated); var beforeApply = module.Archives.Export();
        Assert.Throws<ConflictException>(() => module.Assist.ApplyAssist(plan)); Assert.Equal(beforeApply, module.Archives.Export()); Assert.Empty(module.CollectionReader.Groups()); Assert.False(module.Templates.Find(source.Id)!.IsFavorite);
        var fresh = module.Assist.PrepareAssist(plan.Plan, source.Id); module.Assist.ApplyAssist(fresh); Assert.Single(module.CollectionReader.Groups()); Assert.True(module.Templates.Find(source.Id)!.IsFavorite);
    }

    [Fact]
    public void TransferJournalIsMetadataOnlyIdempotentAndOwnedByTheSameStore()
    {
        var module = Module(); Assert.Same(module.Store, module.TransferAudit); Assert.Same(module.Store, module.TransferHistory);
        var source = module.Authoring.Save(new() { Title = "Fuente", Template = "PRIVATE_PROMPT_BODY" });
        var activity = new WebTransferActivity(Guid.NewGuid(), source.Id, source.Revision, ProviderId.ChatGptWeb, TransferAction.PrepareOnly, TransferState.NeedsManualPaste, TransferPhase.COPIED_OPENED, "copied-manual-paste", 1, "1.0.0", DateTime.UtcNow);
        module.TransferAudit.RecordTransfer(activity); module.TransferAudit.RecordTransfer(activity);
        var stored = Assert.Single(module.TransferHistory.Transfers()); Assert.Equal(activity, stored); Assert.DoesNotContain("PRIVATE_PROMPT_BODY", JsonSerializer.Serialize(stored));
        var before = module.Archives.Export(); Assert.Throws<ValidationException>(() => module.TransferAudit.RecordTransfer(activity with { Id = Guid.NewGuid(), DurationMs = -1 })); Assert.Equal(before, module.Archives.Export());
        Assert.Equal("ok", module.Diagnostics.CheckIntegrity()); Assert.Equal(LocalStore.MaxArchiveBytes, module.Archives.MaximumBytes);
    }

    [Fact]
    public void EditorCopiesPreserveFieldsAndDoNotShareMutableDefaultsOrModels()
    {
        var source = new Template { Title = "Fuente", Template = "Hola <nombre>.", Variables = new() { ["nombre"] = "Ana" }, Revision = 7, IsFavorite = true };
        var copy = WorkspaceSnapshots.Copy(source); Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(copy)); copy.Variables["nombre"] = "Bea"; copy.Title = "Otra"; Assert.Equal("Ana", source.Variables["nombre"]); Assert.Equal("Fuente", source.Title);
        var group = new PromptGroup { Name = "Grupo", Description = "Conservar", IsPinned = true }; var groupCopy = WorkspaceSnapshots.Copy(group); Assert.Equal(JsonSerializer.Serialize(group), JsonSerializer.Serialize(groupCopy)); groupCopy.Name = "Otro"; Assert.Equal("Grupo", group.Name);
        var profile = new InstructionProfile { Name = "Perfil", Text = "Breve.", Revision = 3 }; var profileCopy = WorkspaceSnapshots.Copy(profile); Assert.Equal(JsonSerializer.Serialize(profile), JsonSerializer.Serialize(profileCopy)); profileCopy.Text = "Otro."; Assert.Equal("Breve.", profile.Text);
    }

    [Fact]
    public void HomeAndInteropAdapterNoLongerReceiveTheConcreteStore()
    {
        Assert.DoesNotContain(typeof(QuickPrompt.Web.Components.Pages.Home).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), property => property.GetCustomAttribute<InjectAttribute>() is not null && property.PropertyType == typeof(LocalStore));
        Assert.DoesNotContain(typeof(PanelSession).GetConstructors().SelectMany(constructor => constructor.GetParameters()), parameter => parameter.ParameterType == typeof(LocalStore));
    }

    private sealed class PanelPorts : IPromptReader, ICompilationWriter
    {
        public Template Source = new() { Title = "Fuente", Template = "Hola <nombre>.", Revision = 2, Variables = new() { ["nombre"] = "PRIVATE_DEFAULT" } };
        public int Compiles; public int? ExpectedRevision;
        public Template? Find(Guid id) => id == Source.Id ? Source : null;
        public CatalogPage<Template> Search(CatalogQuery query) => new([Source], 1);
        public CompiledPrompt Preview(Guid id, int revision, Dictionary<string, string> values) => throw new InvalidOperationException("Unexpected reader preview.");
        public CompiledPrompt Compile(Guid id, Dictionary<string, string> values, Guid? instructionId = null, string adjustments = "", int? expectedRevision = null)
        { Compiles++; ExpectedRevision = expectedRevision; return new(Guid.NewGuid(), id, Source.Revision, Source.Title, TemplateCompiler.Render(Source.Template, values), new Dictionary<string, string>(), DateTime.UtcNow); }
        public void FavoriteFinal(CompiledPrompt prompt) => throw new InvalidOperationException();
        public void SaveExecution(LlmExecution execution) => throw new InvalidOperationException();
        public void ClearHistory() => throw new InvalidOperationException();
    }
}
