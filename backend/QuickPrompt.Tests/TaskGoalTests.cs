using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class TaskGoalTests
{
    [Fact]
    public void WorkflowCanAuthorizeContentFromAnUninterpretedCompleteWorkOrder()
    {
        const string original = "Crea un prompt para resumir notas. No inventes datos. No resumas ahora.";
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", original,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));
        var goal = TaskGoal.Create(original, SemanticContext.Empty, plan,
            [new(TaskClauseRole.WORKFLOW, "Crea un prompt"), new(TaskClauseRole.WORK_ORDER, original)]);
        Assert.Equal(original, goal.ContentRequest);
        Assert.Empty(goal.Constraints.LiteralClauses);
        goal.RecordContentAssessment("Resume las notas. No inventes datos.");
        Assert.False((goal with { Clauses = [new(TaskClauseRole.CONTENT, "resumir notas")] }).HasContentAssessment("Resume las notas. No inventes datos."));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkOrderCannotBeNarrowedOrMixedWithAnInferredContentBrief(bool mixed)
    {
        const string original = "Crea un prompt para resumir notas. No inventes datos.";
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", original,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));
        TaskClause[] clauses = mixed ? [new(TaskClauseRole.WORK_ORDER, original), new(TaskClauseRole.CONTENT, "resumir notas")]
            : [new(TaskClauseRole.WORK_ORDER, "Crea un prompt para resumir notas.")];
        var failure = Assert.Throws<PreparationToolException>(() => TaskGoal.Create(original, SemanticContext.Empty, plan, clauses));
        Assert.Equal("INVALID_WORK_ORDER", failure.Code);
    }
    private readonly PromptDocument source = new(Guid.NewGuid(), "Sintético", "", "Describe los resultados en detalle. No inventes datos.", 0, false, false, 2, DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());
    private const string request = "Reduce el texto. Mantén el borrador abierto bajo el identificador de revisión X9Q7. No guardes.";
    private SemanticInterpretation Plan(SemanticOperation[]? goals = null) => SemanticPlanCompiler.Compile(new(goals ?? [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT],
        TargetReference.SELECTED, source.Title, source.Id, "", "Reducir el texto", new([SemanticOperation.EXECUTE_LOCAL, SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY], true, null, null, [], [], true),
        SemanticOutcome.DRAFT, [], false, "", null, "Preparar un borrador breve."));
    private TaskGoal Goal(SemanticInterpretation plan) {
        var goal=TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.CONTENT, "Reduce el texto."), new(TaskClauseRole.WORKFLOW, "Mantén el borrador abierto bajo el identificador de revisión X9Q7."), new(TaskClauseRole.WORKFLOW, "No guardes.")]);
        // These unit fixtures explicitly supply a controlled semantic assessment.
        // Real-Qwen content quality is evaluated separately through production inference.
        goal.RecordContentAssessment(State().TemplateText!);return goal;
    }
    private UiVisibleState State() => new("create", [], source.Id, source.Revision, source.Title, "Resume. No inventes datos.", [], [], true, true, "Resume. No inventes datos.", null, false, false);
    [Fact] public void ContentAssessmentBindsTheActualBodyAndAuthorityInsteadOfASuccessClaim()
    {
        var plan=Plan();var goal=TaskGoal.Create(request,SemanticContext.Empty with {Selected=SemanticEntity.From(source)},plan,
            [new(TaskClauseRole.CONTENT,"Reduce el texto."),new(TaskClauseRole.WORKFLOW,"No guardes.")]);
        var state=State();string[] executed=["ui_open_prompt","ui_prepare_update","ui_verify_prompt_state"];
        Assert.Contains("CONTENT_VALID",GoalCompletionValidator.Validate(goal,plan,state,source,source,executed,new Dictionary<string,string>(),true,false).Missing);
        goal.RecordContentAssessment(state.TemplateText!);
        Assert.True(GoalCompletionValidator.Validate(goal,plan,state,source,source,executed,new Dictionary<string,string>(),true,false).Complete);
        Assert.False(goal.HasContentAssessment("Texto diferente."));
        Assert.False((goal with {GoalId=Guid.NewGuid()}).HasContentAssessment(state.TemplateText!));
        Assert.False((goal with {UserRequest="Otra petición."}).HasContentAssessment(state.TemplateText!));
    }
    [Fact] public void SemanticAssessmentReplacesLanguageAndFormatKeywordHeuristicsOnlyForItsExactArtifact()
    {
        var goal=Goal(Plan());var template=State().TemplateText!;
        var draft=new PreparedDraft(Guid.NewGuid(),PreparationState.READY,PreparationIntent.IMPROVE_PROMPT,PreparationDecision.ADAPT_EXISTING,
            source.Title,"",template,template,[],[],[],"","",[new(source,10,"Origen real")],source.Id,source.Revision,0,false,DateTime.UtcNow);
        var requirements=new PromptRequirements("en","MARKDOWN",["No inventes datos."]);
        Assert.Contains("FORMAT",PromptPreparationPipeline.Validation(draft,requirements).Errors);
        Assert.Contains("OUTPUT_LANGUAGE_INSTRUCTION",PromptPreparationPipeline.Validation(draft,requirements).Errors);
        Assert.True(PromptPreparationPipeline.Validation(draft,requirements,goal).Valid);
        Assert.False(PromptPreparationPipeline.Validation(draft with {TemplateText="Texto modificado.",CompiledText="Texto modificado."},requirements,goal).Valid);
        Assert.Contains("CONSTRAINT",PromptPreparationPipeline.Validation(draft with {TemplateText="Resume.",CompiledText="Resume."},requirements,goal).Errors);
    }
    [Fact] public void OpenOnlyCannotCompleteARequiredTransformationOrCompiledResult()
    {
        var plan = Plan(); var goal = Goal(plan); plan = plan with { Goal = goal };
        var openOnly = State() with { Mode = "focus", TemplateText = source.TemplateText, DraftChanged = false, CompiledReady = false, CompiledText = null };
        var result = GoalCompletionValidator.Validate(goal, plan, openOnly, source, source, ["ui_open_prompt"], new Dictionary<string, string>(), false, false);
        Assert.False(result.Complete); Assert.Contains("DRAFT_CHANGED", result.Missing); Assert.Contains("COMPILED_VISIBLE", result.Missing); Assert.Contains("CONTENT_VALID", result.Missing);
        Assert.True(GoalCompletionValidator.Validate(goal, plan, State(), source, source, ["ui_prepare_update"], new Dictionary<string, string>(), true, false).Complete);
    }
    [Fact] public void GoalCannotBeErasedByAReadPlanOrForgedCompletionAndProtectsSourceRevision()
    {
        var plan = Plan(); var goal = Goal(plan);
        var reading = Plan([SemanticOperation.OPEN_PROMPT]) with { Goal = goal };
        Assert.Contains("GOAL_PLAN_DIVERGED", GoalCompletionValidator.Validate(goal, reading, State(), source, source, ["ui_prepare_update"], new Dictionary<string, string>(), true, false).Missing);
        Assert.Contains("SOURCE_REVISION_CHANGED", GoalCompletionValidator.Validate(goal, plan, State(), source, source with { Revision = 3 }, ["ui_prepare_update"], new Dictionary<string, string>(), true, false).Missing);
        Assert.Contains("UNREQUESTED_PERSISTENCE", GoalCompletionValidator.Validate(goal, plan, State(), source, source, ["ui_prepare_update"], new Dictionary<string, string>(), true, true).Missing);
    }
    [Fact] public void ContentAuthorityExcludesArbitraryWorkflowMetadataWithoutPhraseFilters()
    {
        var plan = Plan(); var goal = Goal(plan);
        Assert.Equal("Reduce el texto.", goal.ContentRequest); Assert.DoesNotContain("X9Q7", goal.ContentRequest); Assert.DoesNotContain("No guardes", goal.ContentRequest);
        Assert.DoesNotContain(request, JsonSerializer.Serialize(plan with { Goal = goal }));
        Assert.Throws<PreparationToolException>(() => TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.CONTENT, "invented authority")]));
        Assert.Throws<PreparationToolException>(() => TaskGoal.Create(request, SemanticContext.Empty, plan,
            [new(TaskClauseRole.CONTENT, request), new(TaskClauseRole.WORKFLOW, "No guardes.")]));
        Assert.Throws<PreparationToolException>(() => TaskGoal.Create(request, SemanticContext.Empty, plan with { Constraints = plan.Constraints with { LiteralClauses = ["No guardes."] } }, goal.Clauses));
    }
    [Fact] public void ExecutionNeedsAnActualCurrentToolReceiptNotAPreviousResponse()
    {
        var plan = Plan([SemanticOperation.OPEN_PROMPT, SemanticOperation.EXECUTE_LOCAL]) with { DesiredOutcome = SemanticOutcome.LOCAL_RESPONSE };
        var goal = Goal(plan); var state = State() with { Mode = "focus", TemplateText = source.TemplateText, DraftChanged = false, ResponseComplete = true, Response = "respuesta anterior" };
        Assert.Contains("LOCAL_RESPONSE", GoalCompletionValidator.Validate(goal, plan, state, source, source, ["ui_open_prompt"], new Dictionary<string, string>(), true, false).Missing);
    }
    [Fact] public void CoupledAuthorityEvidenceUsesASeparateAuthoringInstructionWithoutGrantingExtraEffects()
    {
        const string directive="Reorienta el texto para principiantes y déjalo listo para copiar sin guardar.";
        const string change="Reorientar la instrucción para principiantes.";
        var plan=Plan() with {RequestedTransformation=change};
        TaskClause[] clauses=[new(TaskClauseRole.CONTENT,directive){ContentChange=change},new(TaskClauseRole.WORKFLOW,directive)];
        var goal=TaskGoal.Create(directive,SemanticContext.Empty,plan,clauses);
        Assert.Equal(directive,goal.ContentRequest);Assert.Equal(change,plan.RequestedTransformation);
        Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL,goal.RequiredEffects);Assert.Empty(goal.ApprovalRequirements);
        Assert.Throws<PreparationToolException>(()=>TaskGoal.Create(directive,SemanticContext.Empty,plan,
            [new(TaskClauseRole.CONTENT,"texto inventado"){ContentChange=change}]));
        Assert.Throws<PreparationToolException>(()=>TaskGoal.Create(directive,SemanticContext.Empty,plan,
            [new(TaskClauseRole.CONTENT,directive){ContentChange="Cambiar otra tarea."}]));
        Assert.Throws<PreparationToolException>(()=>TaskGoal.Create(directive,SemanticContext.Empty,plan,
            [new(TaskClauseRole.CONTENT,directive),new(TaskClauseRole.WORKFLOW,"sin guardar.")]));
    }
    [Fact] public async Task ProductionEditorReceivesCompleteContextWithoutGainingWorkflowOrApprovalAuthority()
    {
        var plan = Plan(); plan = plan with { Goal = Goal(plan) };
        var transport = new AuthoringReviewTestFixture.Transport((packet, purpose, callNumber) => {
            var material = AuthoringReviewTestFixture.Material(packet);
            Assert.Equal("GenerateDraftProposal", purpose);
            Assert.False(packet.GetProperty("think").GetBoolean()); Assert.False(packet.TryGetProperty("tools", out _));
            Assert.Equal(request, material.GetProperty("originalRequest").GetString());
            Assert.False(material.TryGetProperty("obligations", out _));
            foreach (var branch in packet.GetProperty("format").GetProperty("anyOf").EnumerateArray()) {
                var properties = branch.GetProperty("properties");
                Assert.False(properties.TryGetProperty("approval", out _)); Assert.False(properties.TryGetProperty("operations", out _));
                Assert.False(properties.TryGetProperty("sourceId", out _));
            }
            return AuthoringReviewTestFixture.Definition("Resume. No inventes datos.", "Sintético", "Propuesta breve.");
        });
        var forbidden = new AuthoringReviewTestFixture.ForbiddenDependencies();
        var generator = new PromptDraftGenerator(transport, forbidden, forbidden, new DraftFidelityReviewer(transport));
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: source.Id, SourceRevision: source.Revision) {
            Interpretation = plan with { UserDefinedInputs = [] }, ValidationFeedback = "EVALUATION_ONLY_MARKER" };
        var proposal = await generator.GenerateAfterSyntheticReview(input, plan.Preparation(), [new(source, 10, "synthetic authorized source")], default);
        Assert.Contains("Resume.", proposal.TemplateText); Assert.Contains("No inventes datos.", proposal.TemplateText);
        Assert.DoesNotContain("X9Q7", proposal.TemplateText); Assert.DoesNotContain("No guardes", proposal.TemplateText);
        Assert.DoesNotContain("EVALUATION_ONLY_MARKER", proposal.TemplateText);
        Assert.True(plan.Goal!.HasAuthoringStructure(proposal.TemplateText)); Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText));
    }
    [Fact] public void RequiredFieldAndNativePreviewPostconditionsCannotBeSubstitutedByASuccessMessage()
    {
        var plan = Plan([SemanticOperation.FILL_VARIABLES, SemanticOperation.COPY_PROMPT]); var goal = Goal(plan);
        var state = State() with { TemplateText = "Describe <dato>.", Values = new() { ["dato"] = "" }, VariableNames = ["dato"], Response = "success" };
        var completion = GoalCompletionValidator.Validate(goal, plan, state, source, source, [], new Dictionary<string, string> { ["dato"] = "valor autorizado" }, true, false);
        Assert.False(completion.Complete); Assert.Contains("VALUES_APPLIED", completion.Missing);
        var withoutFields = State() with { VariableNames = [], Values = [] };
        Assert.Contains("VALUES_APPLIED", GoalCompletionValidator.Validate(goal, plan, withoutFields, source, source, [], new Dictionary<string, string>(), true, false).Missing);
    }
    [Fact] public void AnExplanationCannotCompleteOtherRequiredEffects()
    {
        var plan = Plan([SemanticOperation.EXPLAIN, SemanticOperation.COPY_PROMPT]) with { DesiredOutcome = SemanticOutcome.ADVICE };
        var goal = Goal(plan);
        Assert.Contains("UI_EFFECTS_PENDING", GoalCompletionValidator.ValidateAdvice(goal, plan, "Una explicación válida.", source, source).Missing);
        var pureAdvice = Plan([SemanticOperation.EXPLAIN]) with { DesiredOutcome = SemanticOutcome.ADVICE };
        Assert.True(GoalCompletionValidator.ValidateAdvice(Goal(pureAdvice), pureAdvice, "Una explicación válida.", source, source).Complete);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialInputsCompleteOnlyTheRequestedFillAndCannotCompleteAConsumer(bool copyRequested)
    {
        var plan = Plan(copyRequested ? [SemanticOperation.FILL_VARIABLES, SemanticOperation.COPY_PROMPT] : [SemanticOperation.FILL_VARIABLES]);
        var goal = Goal(plan);
        var state = State() with { TemplateText = "Describe <tema> para <publico>.", VariableNames = ["tema", "publico"],
            Values = new() { ["tema"] = "SQLite", ["publico"] = "" }, CompiledReady = false, CompiledText = null };
        var completion = GoalCompletionValidator.Validate(goal, plan, state, source, source, [], new Dictionary<string, string> { ["tema"] = "SQLite" }, true, false);
        Assert.Equal(!copyRequested, completion.Complete);
        Assert.DoesNotContain("VALUES_APPLIED", completion.Missing);
        Assert.Equal(copyRequested, completion.Missing.Contains("COMPILED_VISIBLE"));
        Assert.Contains("VALUES_APPLIED", GoalCompletionValidator.Validate(goal, plan, state, source, source, [],
            new Dictionary<string, string> { ["tema"] = "PostgreSQL" }, true, false).Missing);
        Assert.Contains("VALUES_APPLIED", GoalCompletionValidator.Validate(goal, plan, state, source, source, [],
            new Dictionary<string, string>(), true, false).Missing);
    }
    [Fact] public void ClarificationRetainsTheUnfinishedUserGoalWithoutGrantingEffects()
    {
        var intended = Plan();
        var pending = intended with { Intentions = [SemanticOperation.ASK_CLARIFICATION], DesiredOutcome = SemanticOutcome.CLARIFICATION,
            MissingInformation = ["¿Qué enfoque necesitas?"], DeferredGoals = [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT], DeferredOutcome = SemanticOutcome.DRAFT };
        var goal = Goal(pending);
        Assert.Contains(SemanticOperation.PROPOSE_ADAPTATION, goal.RequiredEffects); Assert.Contains(SemanticOperation.COPY_PROMPT, goal.RequiredEffects);
        Assert.Equal(SemanticOutcome.DRAFT, goal.RequestedOutcome); Assert.Contains(CompletionCondition.DRAFT_CHANGED, goal.CompletionConditions);
        Assert.Contains(CompletionCondition.COMPILED_VISIBLE, goal.CompletionConditions);
        Assert.False(pending.Adapts); Assert.False(pending.Executes); Assert.Empty(goal.ApprovalRequirements);
        Assert.Contains("GOAL_PLAN_DIVERGED", GoalCompletionValidator.Validate(goal, pending, State(), source, source, [], new Dictionary<string, string>(), true, false).Missing);
        Assert.False(GoalCompletionValidator.ValidateAdvice(goal, pending, "¿Qué enfoque necesitas?", source, source).Complete);
    }
    [Fact] public void AResolveDependencyCannotBypassAnExplicitProhibitionOnOpeningTheItem()
    {
        var plan = Plan([SemanticOperation.SEARCH_PROMPT]) with { Query = source.Title };
        plan = plan with { Constraints = plan.Constraints with { ForbiddenOperations = [..plan.Constraints.ForbiddenOperations, SemanticOperation.OPEN_PROMPT] } };
        var goal = Goal(plan); plan = plan with { Goal = goal };
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_open_prompt"));
        Assert.Contains(CompletionCondition.SEARCH_RESULTS_VISIBLE, goal.CompletionConditions);
        var listed = State() with { Mode = "discover", PromptId = null, Revision = null, Candidates = [new(source.Id, source.Title, source.Revision)] };
        Assert.True(GoalCompletionValidator.Validate(goal, plan, listed, source, source, ["ui_search_prompt"], new Dictionary<string, string>(), true, false).Complete);
    }
}
