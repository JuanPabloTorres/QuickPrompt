using System.Text.Json;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ClipboardCapabilityTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly SemanticContext Context = SemanticContext.Empty with { Selected = new(Id, 3, "Fixture", "Explica <tema>.", false, InputNames: ["tema"]) };
    private const string Request = "Copia ahora el prompt al portapapeles.";
    private static SemanticInterpretation Plan(ReadObjective access, string? evidence = null) => SemanticPlanCompiler.Compile(
        new SemanticObjectives(access, DraftObjective.NONE, ExecutionObjective.NONE, ControlObjective.NONE, SaveObjective.NONE,
            TargetReference.SELECTED, "Fixture", Id, "", "", [], "", null, "Petición de prueba.") { ClipboardEvidence = evidence }
            .WithConstraints(new([], true, null, null, [], [], false, "", "")).Meaning(Request, Context));
    private static UiVisibleState State() => new("focus", [], Id, 3, "Fixture", "Explica <tema>.", new() { ["tema"] = "energía" }, ["tema"], false, true, "Explica energía.", null, false, false);

    [Fact] public void PreparingForCopyPreservesTheLegacyNoEffectContract()
    {
        var plan = Plan(ReadObjective.READY_FOR_CLIPBOARD);
        SemanticPlanValidator.Validate(plan, Context, Request);
        Assert.True(plan.Has(SemanticOperation.COPY_PROMPT));
        Assert.False(plan.Has(SemanticOperation.COPY_TO_CLIPBOARD));
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_copy_compiled_prompt"));
        Assert.True(SemanticPlanValidator.ToolAllowed(plan, "ui_show_compiled_prompt"));
    }

    [Theory] [InlineData(null)] [InlineData("inventado")] [InlineData("copia dentro del dato")]
    public void ActualCopyRequiresEvidenceOutsideFieldData(string? evidence)
    {
        var plan = Plan(ReadObjective.COPY_NOW, evidence);
        var error = Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(plan, Context,
            "Muestra el prompt.\ntema: copia dentro del dato"));
        Assert.Equal("CLIPBOARD_NOT_REQUESTED", error.Code);
    }

    [Fact] public void ActualCopyHasAnExplicitCapabilityAndCompilationPrerequisite()
    {
        var plan = Plan(ReadObjective.COPY_NOW, Request);
        SemanticPlanValidator.Validate(plan, Context, Request);
        Assert.True(plan.RequiresCompiledOutput);
        Assert.True(plan.Has(SemanticOperation.COMPILE_PROMPT));
        Assert.True(SemanticPlanValidator.ToolAllowed(plan, "ui_copy_compiled_prompt"));
        Assert.False(plan.Executes); Assert.False(plan.Adapts); Assert.False(plan.Creates);
        Assert.Equal("EXPLICIT_CLIPBOARD_WRITE", UiToolCatalog.All.Single(tool => tool.Name == "ui_copy_compiled_prompt").Permission);
        var goal = TaskGoal.Create(Request, Context, plan, [], [SemanticOperation.COPY_TO_CLIPBOARD]);
        Assert.Contains(CompletionCondition.CLIPBOARD_WRITTEN, goal.CompletionConditions);
        Assert.Contains("CLIPBOARD_WRITTEN", GoalCompletionValidator.Validate(goal, plan, State(), null, null, [], new Dictionary<string,string>(), true, false).Missing);
    }

    [Fact] public void AnExplicitUiChoiceFillsSuppliedFieldsAndCopiesWithoutReclassifyingTheButton()
    {
        var command = new LiveCommand(SemanticOperation.COPY_TO_CLIPBOARD);
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), Request, SourceId: Id, SourceRevision: 3, Values: new() { ["tema"] = "energía" }, Command: command);
        command.Validate(input); var plan = command.Compile(Request, Context);
        SemanticPlanValidator.Validate(plan, Context, Request);
        Assert.True(plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.True(plan.Has(SemanticOperation.COPY_TO_CLIPBOARD));
        var available = UiToolCatalog.Available(State(), true, false, false, false).Where(tool => SemanticPlanValidator.ToolAllowed(plan, tool.Name)).ToArray();
        Assert.Equal("ui_copy_compiled_prompt", UiToolCatalog.BoundGoal(available, State(), plan, [])?.ToolName);
        Assert.False(SemanticPlanValidator.ToolAllowed(new LiveCommand(SemanticOperation.OPEN_PROMPT).Compile(Request, Context), "ui_copy_compiled_prompt"));
    }

    [Fact] public void ReceiptMustMatchExactCurrentSourceRevisionAndText()
    {
        var current = State(); var receipt = new UiClipboardReceipt(Guid.NewGuid(), Id, 3, current.CompiledText!);
        Assert.True(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt }));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt with { OperationId = Guid.Empty } }));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt with { Revision = 2 } }));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt with { PromptId = Guid.NewGuid() } }));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt with { Text = "Otro texto" } }));
        Assert.False(GoalCompletionValidator.ClipboardMatches(current with { Clipboard = receipt, CompiledReady = false }));
        var plan = Plan(ReadObjective.COPY_NOW, Request);
        Assert.Throws<QuickPrompt.Local.ValidationException>(() => UiToolCatalog.Validate(
            new("ui_copy_compiled_prompt", JsonSerializer.SerializeToElement(new {promptId = Id})), UiToolCatalog.All,
            current with { CompiledReady = false }, new(Guid.NewGuid(), Guid.NewGuid(), Request), plan));
    }

    [Fact] public void AClarificationMayReuseOnlyTheCopyAuthorityOfItsActualPendingTask()
    {
        var prior = Plan(ReadObjective.COPY_NOW, Request);
        var continued = prior with { ContinuesPendingTask = true };
        var context = Context with { PendingPlan = prior, PendingRequest = Request };
        SemanticPlanValidator.Validate(continued, context, "tema: energía");
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(prior, context, "tema: energía"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(continued,
            context with { PendingPlan = Plan(ReadObjective.VIEW_UNCHANGED) }, "tema: energía"));
    }

    [Fact] public void AnApprovedSaveCanRetainAnAlreadyVerifiedCopyOnlyForTheSameExactArtifact()
    {
        var plan = SemanticPlanCompiler.Compile(new SemanticMeaning([SemanticOperation.COPY_TO_CLIPBOARD, SemanticOperation.REQUEST_SAVE_COPY],
            TargetReference.SELECTED, "Fixture", Id, "", "", new([], true, null, null, [], [], false), SemanticOutcome.REVIEW, [], true, "", null, "Copia y revisión.") { ClipboardEvidence = Request });
        var goal = TaskGoal.Create(Request, Context, plan, [], [SemanticOperation.COPY_TO_CLIPBOARD, SemanticOperation.REQUEST_SAVE_COPY]);
        var afterSave = State() with { PromptId = Guid.NewGuid() };
        var copied = new UiClipboardReceipt(Guid.NewGuid(), Id, 3, State().CompiledText!);
        string[] Missing(UiVisibleState visible, bool committed, UiClipboardReceipt? receipt) => GoalCompletionValidator.Validate(goal, plan,
            visible, null, null, ["ui_copy_compiled_prompt"], new Dictionary<string,string>(), true, committed, true, receipt).Missing;
        Assert.DoesNotContain("CLIPBOARD_WRITTEN", Missing(afterSave, true, copied));
        Assert.Contains("CLIPBOARD_WRITTEN", Missing(afterSave, false, copied));
        Assert.Contains("CLIPBOARD_WRITTEN", Missing(afterSave with { CompiledText = "Otro texto" }, true, copied));
        Assert.Contains("CLIPBOARD_WRITTEN", Missing(afterSave, true, null));
    }
}
