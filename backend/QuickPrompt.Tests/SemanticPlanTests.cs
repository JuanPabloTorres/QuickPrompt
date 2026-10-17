using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

internal static class SemanticFixture
{
    public static SemanticInterpretation Plan(SemanticOperation[] operations, TargetReference reference = TargetReference.NONE, Guid? id = null, string query = "", SemanticOutcome outcome = SemanticOutcome.READ) =>
        new(operations, reference, "Origen de prueba", id, query, operations.Contains(SemanticOperation.PROPOSE_ADAPTATION) ? "Cambio solicitado" : "",
            new([], true, null, null, [], [], false), outcome,
            operations.Select((op, index) => new SemanticStep(index + 1, op, index == 0 ? [] : [index])).ToArray(), [], false, "", null, "Interpretación controlada para probar validación y efectos; no mide calidad de Qwen.");
    public static SemanticInterpretation Read => Plan([SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], TargetReference.NAMED, query: "correo");
}
internal sealed class FixedSemantic(SemanticInterpretation plan) : ISemanticIntentInterpreter, ICandidateSelector
{
    public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct) => Task.FromResult(plan);
    public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct) => Task.FromResult(new SemanticCandidateChoice(candidates.Where(c => c.Score >= 2).Take(3).Select(c => c.Prompt.Id).ToArray(), "Candidatos de prueba."));
}
public sealed class SemanticPlanTests
{
    [Theory]
    [InlineData(SemanticOperation.PREPARE_LOCAL_EXECUTION)]
    [InlineData(SemanticOperation.PREPARE_PROVIDER)]
    [InlineData(SemanticOperation.EXECUTE_LOCAL)]
    public void VerifiedPreparationResolvesProvidedBindingsBeforeCompilationAsADomainDependency(SemanticOperation operation)
    {
        var meaning = new SemanticMeaning([operation], TargetReference.SELECTED, "Fuente", Guid.NewGuid(), "", "", SemanticFixture.Read.Constraints,
            operation == SemanticOperation.EXECUTE_LOCAL ? SemanticOutcome.LOCAL_RESPONSE : operation == SemanticOperation.PREPARE_PROVIDER ? SemanticOutcome.READY_PROVIDER : SemanticOutcome.READY_LOCAL,
            [], false, operation == SemanticOperation.EXECUTE_LOCAL ? "Ejecuta con Qwen ahora." : "", operation == SemanticOperation.PREPARE_PROVIDER ? "claude" : "ollama", "Objetivo validado.");
        Assert.False(SemanticPlanCompiler.Compile(meaning).Has(SemanticOperation.FILL_VARIABLES));
        var plan = SemanticPlanCompiler.Compile(meaning, true);
        Assert.True(plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.True(Array.IndexOf(plan.Intentions, SemanticOperation.FILL_VARIABLES) < Array.IndexOf(plan.Intentions, SemanticOperation.COMPILE_PROMPT));
        Assert.Contains("PROVIDED_INPUT_BINDINGS_RESOLVED", plan.ValidationNotes!);
    }
    [Fact]
    public void AvailableBindingsDoNotExpandAReadOnlyGoal()
    {
        var meaning = new SemanticMeaning([SemanticOperation.OPEN_PROMPT], TargetReference.SELECTED, "Fuente", Guid.NewGuid(), "", "", SemanticFixture.Read.Constraints,
            SemanticOutcome.READ, [], false, "", null, "Consultar.");
        Assert.False(SemanticPlanCompiler.Compile(meaning, true).Has(SemanticOperation.FILL_VARIABLES));
    }
    [Fact]
    public void AProhibitionOnFillingOverridesProvidedBindingsAndPreparationDependencies()
    {
        var meaning = new SemanticMeaning([SemanticOperation.PREPARE_LOCAL_EXECUTION], TargetReference.SELECTED, "Fuente", Guid.NewGuid(), "", "",
            SemanticFixture.Read.Constraints with { ForbiddenOperations = [SemanticOperation.FILL_VARIABLES] }, SemanticOutcome.READY_LOCAL,
            [], false, "", "ollama", "Preparar sin rellenar.");
        var plan = SemanticPlanCompiler.Compile(meaning, true);
        Assert.False(plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(plan, "ui_fill_variable"));
    }
    [Fact] public void InvalidDependenciesUnknownOperationsAndMissingValidationAreRejectedBeforeTools()
    {
        var plan = SemanticFixture.Read;
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(plan with { ProposedSteps = [new(1, SemanticOperation.OPEN_PROMPT, [2])] }, SemanticContext.Empty, "solicitud"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(plan with { Intentions = [(SemanticOperation)999] }, SemanticContext.Empty, "solicitud"));
        var edit = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.PROPOSE_ADAPTATION], outcome: SemanticOutcome.DRAFT);
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(edit, SemanticContext.Empty, "solicitud"));
    }
    [Fact] public void PrepareCannotBecomeExecuteAndNegativeOperationsCannotBePlanned()
    {
        var prepare = SemanticFixture.Plan([SemanticOperation.COMPILE_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION], outcome: SemanticOutcome.READY_LOCAL);
        SemanticPlanValidator.Validate(prepare, SemanticContext.Empty, "solicitud");
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(prepare, "execute_local_llm"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(prepare with { Constraints = prepare.Constraints with { ForbiddenOperations = [SemanticOperation.PREPARE_LOCAL_EXECUTION] } }, SemanticContext.Empty, "solicitud"));
    }
    [Fact] public void ArbitraryIdsUnregisteredToolsAndUnconfirmedSavePlansAreRejected()
    {
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(SemanticFixture.Read with { ResolvedTargetId = Guid.NewGuid() }, SemanticContext.Empty, "solicitud"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "execute_sql"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(SemanticFixture.Plan([SemanticOperation.REQUEST_SAVE_VERSION]), SemanticContext.Empty, "guarda"));
    }
    [Fact] public void ReadContractCannotAuthorizeEditingRegardlessOfMessageSpelling()
    {
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "ui_prepare_update"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "propose_new_prompt"));
    }
    [Fact] public void StructuredSchemaRejectsUnknownSelectorsAndAdditionalApprovalFields()
    {
        var data = JsonSerializer.SerializeToNode(SemanticFixture.Read, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        data["approved"] = true;
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(JsonSerializer.SerializeToElement(data), PreparationInference.SemanticSchema));
    }
    [Fact] public void ContextIsBoundedAndExpiredWithoutPersistingConversation()
    {
        var sessions = new AgentSessions(); var id = Guid.NewGuid();
        sessions.Put(new(id, null, null, [], DateTime.UtcNow.AddHours(-3)));
        Assert.Null(sessions.Get(id));
        var plan = SemanticFixture.Read;
        for (var index = 0; index < 120; index++) sessions.Put(new(Guid.NewGuid(), null, null, [], DateTime.UtcNow, LastProposal: plan));
        Assert.Null(sessions.Get(id));
    }
    [Fact] public void FillingUsesItsOwnGoalWithoutRewritingOrRunningAndMissingOriginsAskBeforeEffects()
    {
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Ejemplo", "Explica <tema>.", false);
        var context = SemanticContext.Empty with { Selected = source };
        var objectives = new SemanticObjectives(ReadObjective.NONE, DraftObjective.NONE, ExecutionObjective.NONE, ControlObjective.NONE, SaveObjective.NONE,
            TargetReference.SELECTED, "Ejemplo", source.Id, "", "", [], "", null, "Completar campos", ValuesObjective.FILL_TEMPLATE_INPUTS);
        var constraints = new SemanticConstraintInput([], true, null, null, [], [], false, "", "");
        var plan = SemanticPlanCompiler.Compile(objectives.WithConstraints(constraints).Meaning("Completa los campos.", context));
        SemanticPlanValidator.Validate(plan, context, "Completa los campos.");
        Assert.Contains(SemanticOperation.FILL_VARIABLES, plan.Intentions); Assert.Contains(SemanticOperation.COMPILE_PROMPT, plan.Intentions);
        Assert.False(plan.Adapts); Assert.False(plan.Executes); Assert.False(SemanticPlanValidator.ToolAllowed(plan, "execute_local_llm"));
        var unresolved = SemanticPlanCompiler.Compile((objectives with { TargetReference = TargetReference.NONE, ResolvedTargetId = null }).WithConstraints(constraints).Meaning("Completa.", SemanticContext.Empty));
        Assert.Equal(SemanticOutcome.CLARIFICATION, unresolved.DesiredOutcome); Assert.Contains("MISSING_TARGET_REFERENCE", unresolved.ValidationNotes!);
        Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, unresolved.Intentions);
    }
    [Fact] public void NewArtifactCanBeValidatedPreparedAndExecutedWithoutInventingAnExistingSource()
    {
        var meaning = new SemanticMeaning([SemanticOperation.CREATE_DRAFT, SemanticOperation.EXECUTE_LOCAL], TargetReference.NONE, "Nueva tarea", null, "", "Crear una explicación.",
            new([], true, null, null, [], [], false), SemanticOutcome.LOCAL_RESPONSE, [], false, "Ejecuta ahora", "ollama", "Nueva propuesta y ejecución explícita.");
        var plan = SemanticPlanCompiler.Compile(meaning); SemanticPlanValidator.Validate(plan, SemanticContext.Empty, "Ejecuta ahora");
        Assert.DoesNotContain(SemanticOperation.RESOLVE_PROMPT, plan.Intentions); Assert.Null(plan.ResolvedTargetId);
        var operations = plan.ProposedSteps.Select(s => s.Operation).ToArray();
        Assert.True(Array.IndexOf(operations, SemanticOperation.VALIDATE_DRAFT) < Array.IndexOf(operations, SemanticOperation.EXECUTE_LOCAL));
        Assert.True(Array.IndexOf(operations, SemanticOperation.COMPILE_PROMPT) < Array.IndexOf(operations, SemanticOperation.EXECUTE_LOCAL));
    }
    [Theory]
    [InlineData("read")]
    [InlineData("fill")]
    [InlineData("cancel")]
    public void NoInstructionWritingTaskCannotDiscardAnIndependentWorkflowGoal(string scenario)
    {
        var text = new SemanticTextGoal(SemanticTextResult.NO_IDENTIFIABLE_TASK, "", "", "No se pide redactar contenido nuevo.");
        var workflow = new SemanticWorkflowObjectives(scenario == "read" ? ReadObjective.VIEW_UNCHANGED : ReadObjective.NONE,
            scenario == "fill" ? ValuesObjective.FILL_TEMPLATE_INPUTS : ValuesObjective.NONE, ExecutionObjective.NONE,
            scenario == "cancel" ? ControlObjective.CANCEL : ControlObjective.NONE, SaveObjective.NONE, [], "", null, "Objetivo independiente.");
        var reference = scenario == "cancel" ? new SemanticReferenceChoice(TargetReference.NONE, null, "", false, "")
            : new SemanticReferenceChoice(TargetReference.NAMED, null, "correo", false, "");
        var objectives = SemanticStageComposition.Compose(text, workflow, reference, "Origen");
        Assert.Equal(scenario == "cancel" ? ControlObjective.CANCEL : ControlObjective.NONE, objectives.Control);
        Assert.Equal(DraftObjective.NONE, objectives.Draft); Assert.Equal(workflow.Access, objectives.Access); Assert.Equal(workflow.Values, objectives.Values);
        var constraints = new SemanticConstraintInput([], true, null, null, [], [], false, "", "");
        var plan = SemanticPlanCompiler.Compile(objectives.WithConstraints(constraints).Meaning("Contrato controlado", SemanticContext.Empty));
        Assert.Contains(scenario == "read" ? SemanticOperation.OPEN_PROMPT : scenario == "fill" ? SemanticOperation.FILL_VARIABLES : SemanticOperation.CANCEL, plan.Intentions);
        Assert.False(plan.Adapts); Assert.False(plan.Executes);
    }
    [Fact] public void AdviceCompositionCannotInheritWorkflowEffectsAndContinuationRequiresActualTaskAuthority()
    {
        var text = new SemanticTextGoal(SemanticTextResult.EXPLANATION_WITHOUT_PROPOSAL, "", "", "Consejos sin propuesta.");
        var effects = new SemanticWorkflowObjectives(ReadObjective.NONE, ValuesObjective.NONE, ExecutionObjective.RUN_LOCAL_QWEN_NOW, ControlObjective.NONE, SaveObjective.REVIEW_PERSIST_EXISTING, [], "Ejecuta", "ollama", "No autorizado por una consulta.");
        var objectives = SemanticStageComposition.Compose(text, effects, new(TargetReference.NONE, null, "", false, ""), "");
        Assert.Equal(ExecutionObjective.NONE, objectives.Execution); Assert.Equal(SaveObjective.NONE, objectives.Save); Assert.Equal(DraftObjective.NONE, objectives.Draft);
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(SemanticFixture.Read with { ContinuesPendingTask = true }, SemanticContext.Empty, "Solicitud"));
    }
    [Fact] public void IndependentCreationCannotSilentlyOpenAnUnidentifiedSource()
    {
        var invalid = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT], TargetReference.NONE, outcome: SemanticOutcome.NEW_DRAFT);
        var error = Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(invalid, SemanticContext.Empty, "Petición controlada"));
        Assert.Equal("SOURCE_ACCESS_REQUIRES_REFERENCE", error.Code);
    }
    [Fact] public void LiteralContentRequirementsCannotBeInventedAndReadOnlyPlansAlwaysProtectTheOriginal()
    {
        var proposed = new SemanticConstraintInput([], false, null, null, ["No inventes datos."], [], false, "", "");
        Assert.Throws<PreparationToolException>(() => proposed.ValidateEvidence("Prepara una explicación.", SemanticContext.Empty));
        proposed.ValidateEvidence("Prepara una explicación. No inventes datos.", SemanticContext.Empty);
        var prior = SemanticContext.Empty with { Constraints = new([], true, null, null, proposed.LiteralClauses, [], false) };
        proposed.ValidateEvidence("Conserva las restricciones anteriores.", prior);
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Ejemplo", "Explica el tema.", false);
        var objectives = new SemanticObjectives(ReadObjective.VIEW_UNCHANGED, DraftObjective.NONE, ExecutionObjective.NONE, ControlObjective.NONE, SaveObjective.NONE,
            TargetReference.SELECTED, source.Title, source.Id, "", "", [], "", null, "Solo lectura.");
        var meaning = objectives.WithConstraints(proposed with { LiteralClauses = [] }).Meaning("Muestra el prompt.", SemanticContext.Empty with { Selected = source });
        Assert.True(meaning.Constraints.PreserveOriginal); Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION, meaning.Constraints.ForbiddenOperations);
    }
}
