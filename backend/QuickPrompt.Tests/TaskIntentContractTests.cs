using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Nova.Evaluation.Retired;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class TaskIntentContractTests
{
    private const string Request = "Prepara la copia. No copies al portapapeles.";
    private static readonly SemanticEntity Source = new(Guid.NewGuid(), 1, "Sintético", "Instrucción sin variables.", false, InputNames: []);
    private static SemanticContext Context => SemanticContext.Empty with { Selected = Source };
    private static TaskExecutionRequest Declare(SemanticOperation operation, string request) => new(
        [new(operation, request)], [], TargetReference.NONE, null, "", [], "",
        new(null, null, "", "", [], [], [], false), null, false, "", "");
    private static SemanticInterpretation Prepare() => TaskRequestCompiler.Compile(Declare(SemanticOperation.COPY_PROMPT, "Prepara la copia.") with {
        Forbidden = [new(SemanticOperation.COPY_TO_CLIPBOARD, "No copies al portapapeles.")]
    }, Request, Context);
    private static UiVisibleState State(bool compiled) => new("focus", [], Source.Id, Source.Revision, Source.Title, Source.Text,
        [], [], false, compiled, compiled ? Source.Text : null, null, false, false);
    private static PromptDocument Document() => new(Source.Id, Source.Title, "", Source.Text, 0, false, false, Source.Revision,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());

    [Fact] public void ContractIsCapturedBeforePlanningAndDoesNotReadAnExecutionPlan()
    {
        var roots = new[] { SemanticOperation.COPY_PROMPT };
        var forbidden = new[] { SemanticOperation.COPY_TO_CLIPBOARD };
        var meaning = new SemanticMeaning(roots, TargetReference.SELECTED, Source.Title, Source.Id, "", "",
            new(forbidden, true, null, null, [], [], false), SemanticOutcome.READ, [], false, "", null, "");
        var contract = TaskIntentContract.Accept(UserActionRequest.FromOperations(Request, roots), meaning);
        roots[0] = SemanticOperation.OPEN_PROMPT;
        forbidden[0] = SemanticOperation.COPY_PROMPT;
        Assert.Equal(new[] { SemanticOperation.COPY_PROMPT }, contract.RequestedEffects.ToArray());
        Assert.Contains(CompletionCondition.COMPILED_VISIBLE, contract.RequiredOutcomes);
        Assert.Contains(SemanticOperation.COPY_TO_CLIPBOARD, contract.ForbiddenEffects);
    }

    [Fact] public void AReadPlanCannotApproveItsOwnReplacementGoal()
    {
        var original = Prepare();
        var reading = TaskRequestCompiler.Compile(Declare(SemanticOperation.OPEN_PROMPT, "Abre la fuente."), "Abre la fuente.", Context);
        var attempt = reading with { IntentContract = original.IntentContract, Goal = original.Goal };
        var failure = Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(attempt, Context, Request));
        Assert.Equal("INTENT_PLAN_DIVERGED", failure.Code);
        Assert.Contains("GOAL_PLAN_DIVERGED", GoalCompletionValidator.Validate(original.Goal!, attempt, State(true),
            Document(), Document(), [], new Dictionary<string, string>(), false, false).Missing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingOrRemovingTheGoalCannotDetachTheOriginalIntent(bool remove)
    {
        var original = Prepare();
        var reading = TaskRequestCompiler.Compile(Declare(SemanticOperation.OPEN_PROMPT, "Abre la fuente."), "Abre la fuente.", Context);
        var attempt = original with { Goal = remove ? null : reading.Goal };
        Assert.Equal("INTENT_PLAN_DIVERGED", Assert.Throws<PreparationToolException>(() =>
            SemanticPlanValidator.Validate(attempt, Context, Request)).Code);
    }

    [Fact] public void ErasingGoalConditionsCannotCompleteAnUncompiledRequestedCopy()
    {
        var plan = Prepare();
        var copiedGoal = plan.Goal! with { CompletionConditions = [], RequiredEffects = [], ForbiddenEffects = [] };
        var result = GoalCompletionValidator.Validate(copiedGoal, plan, State(false), Document(), Document(),
            ["ui_open_prompt"], new Dictionary<string, string>(), false, false);
        Assert.False(result.Complete);
        Assert.Contains("COMPILED_VISIBLE", result.Missing);
        Assert.Contains(SemanticOperation.COPY_TO_CLIPBOARD, copiedGoal.Scope.ForbiddenEffects);
    }

    [Fact] public void AClipboardRequestRequiresAnActualReceiptEvenWithReadyCompiledText()
    {
        const string request = "Copia al portapapeles.";
        var plan = TaskRequestCompiler.Compile(Declare(SemanticOperation.COPY_TO_CLIPBOARD, request), request, Context);
        Assert.Contains("CLIPBOARD_WRITTEN", GoalCompletionValidator.Validate(plan.Goal!, plan, State(true),
            Document(), Document(), ["ui_copy_compiled_prompt"], new Dictionary<string, string>(), false, false).Missing);
    }

    [Fact] public void AddingAnUnrequestedEffectOrChangingIdentityFailsBeforeExecution()
    {
        var plan = Prepare();
        Assert.False(plan.Goal!.IntentContract!.Allows(plan with { Intentions = [..plan.Intentions, SemanticOperation.COPY_TO_CLIPBOARD] }));
        Assert.False(plan.Goal.IntentContract.Allows(plan with { ResolvedTargetId = Guid.NewGuid() }));
    }

    [Fact] public void AReceiptOfAnUnrequestedWriteCannotBeHiddenByAnOtherwiseSuccessfulPlan()
    {
        var plan = Prepare();
        var result = GoalCompletionValidator.Validate(plan.Goal!, plan, State(true), Document(), Document(),
            ["ui_open_prompt", "ui_copy_compiled_prompt"], new Dictionary<string, string>(), false, false);
        Assert.False(result.Complete);
        Assert.Contains("UNREQUESTED_UI_EFFECT", result.Missing);
    }

    [Fact] public void AGenericUiSnapshotCannotClaimThatSessionControlCompleted()
    {
        const string request = "Detén la tarea de esta sesión.";
        var plan = TaskRequestCompiler.Compile(Declare(SemanticOperation.CANCEL, request), request, Context);
        Assert.Contains("SESSION_CONTROL_RECEIPT_REQUIRED", GoalCompletionValidator.Validate(plan.Goal!, plan,
            State(true), null, null, [], new Dictionary<string, string>(), false, false).Missing);
    }

    [Fact] public void SuspendedIntentCannotCompleteByAnsweringItsOwnClarification()
    {
        const string request = "Prepara la copia.";
        var plan = TaskRequestCompiler.Compile(Declare(SemanticOperation.COPY_PROMPT, request), request, SemanticContext.Empty);
        Assert.Equal(SemanticOutcome.CLARIFICATION, plan.DesiredOutcome);
        Assert.Contains(SemanticOperation.COPY_PROMPT, plan.Goal!.IntentContract!.RequestedEffects);
        Assert.False(GoalCompletionValidator.Validate(plan.Goal, plan, State(true), null, null, [],
            new Dictionary<string, string>(), false, false).Complete);
    }

    [Theory]
    [InlineData(SemanticOperation.CANCEL)]
    [InlineData(SemanticOperation.PAUSE)]
    [InlineData(SemanticOperation.RESUME)]
    public void SessionControlDoesNotRequireAContentContinuation(SemanticOperation operation)
    {
        const string request = "Controla la tarea de esta sesión.";
        var plan = TaskRequestCompiler.Compile(Declare(operation, request) with { ContinuesPendingTask = true }, request, Context);
        Assert.Equal([operation], plan.Intentions);
        Assert.False(plan.ContinuesPendingTask);
        Assert.Empty(plan.Goal!.TargetReferences);
        Assert.Equal(TargetReference.NONE, plan.TargetReference);
    }

    [Fact] public void MissingSourceSuspendsAuthoringWithoutErasingOriginalContentAuthority()
    {
        const string request = "Reformula la instrucción para que sea comprensible.";
        var plan = TaskRequestCompiler.Compile(Declare(SemanticOperation.PROPOSE_ADAPTATION, request) with {
            ContentEvidence = request
        }, request, SemanticContext.Empty);
        Assert.Equal(SemanticOutcome.CLARIFICATION, plan.DesiredOutcome);
        Assert.Equal(new[] { SemanticOperation.PROPOSE_ADAPTATION }, plan.Goal!.IntentContract!.RequestedEffects.ToArray());
        Assert.Equal(request, plan.Goal.ContentRequest);
        Assert.DoesNotContain(SemanticOperation.PROPOSE_ADAPTATION, plan.Intentions);
    }

    [Fact] public void ExistingQueryNormalizationDoesNotContradictTheFrozenIntent()
    {
        const string request = "Busca y abre la instrucción de informes.";
        var plan = TaskRequestCompiler.Compile(Declare(SemanticOperation.SEARCH_PROMPT, request) with {
            Effects = [new(SemanticOperation.SEARCH_PROMPT, request), new(SemanticOperation.OPEN_PROMPT, request)], Query = request
        }, request, SemanticContext.Empty);
        Assert.Equal(TargetReference.NAMED, plan.TargetReference);
        Assert.Equal(TargetReference.NAMED, plan.IntentContract!.Reference);
        Assert.True(plan.IntentContract.Allows(plan));
        var suspended = TaskClarification.Apply(plan, "¿Cuál de los dos registros quieres abrir?");
        Assert.True(plan.IntentContract.Allows(suspended));
        SemanticPlanValidator.ValidateIntent(suspended);
        Assert.Same(plan.IntentContract, suspended.Goal!.IntentContract);
    }

    [Fact] public void RequiringAndForbiddingTheSameEffectIsRejectedAsIntentContradiction()
    {
        var error = Assert.Throws<PreparationToolException>(() => TaskRequestCompiler.Compile(
            Declare(SemanticOperation.COPY_TO_CLIPBOARD, Request) with {
                Forbidden = [new(SemanticOperation.COPY_TO_CLIPBOARD, "No copies al portapapeles.")]
            }, Request, Context));
        Assert.Equal("INTENT_CONTRADICTION", error.Code);
    }

    [Theory]
    [InlineData(SemanticOperation.EXECUTE_LOCAL, null)]
    [InlineData(SemanticOperation.EXECUTE_LOCAL, "qwen3:8b")]
    [InlineData(SemanticOperation.PREPARE_LOCAL_EXECUTION, "not-a-provider")]
    [InlineData(SemanticOperation.OPEN_PROMPT, "irrelevant-provider-metadata")]
    public void DomainOwnsTheDestinationOfLocalOrDestinationFreeGoals(SemanticOperation operation, string? metadata)
    {
        const string request = "Realiza la operación solicitada en esta instrucción.";
        var plan = TaskRequestCompiler.Compile(Declare(operation, request) with { ProviderId = metadata }, request, Context);
        Assert.Equal(operation == SemanticOperation.OPEN_PROMPT ? null : "ollama", plan.ProviderId);
        Assert.False(plan.IntentContract!.Allows(plan with { ProviderId = "other-destination" }));
    }

    [Fact] public void UnknownExternalDestinationStillFailsClosed()
    {
        const string request = "Prepara el destino de la instrucción.";
        Assert.Equal("UNKNOWN_PROVIDER", Assert.Throws<PreparationToolException>(() => TaskRequestCompiler.Compile(
            Declare(SemanticOperation.PREPARE_PROVIDER, request) with { ProviderId = "not-configured" }, request, Context)).Code);
    }
}
