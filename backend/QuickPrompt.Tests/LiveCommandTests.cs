using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class LiveCommandTests
{
    private readonly SemanticEntity source = new(Guid.NewGuid(), 3, "Fuente elegida", "Resume <texto>.", false, InputNames: ["texto"]);

    [Theory]
    [InlineData(CopyContent.TEMPLATE, false)]
    [InlineData(CopyContent.RENDERED, true)]
    public void ExplicitAdaptationAndCopyPreservesBothGoalsWithoutImplicitSaveOrClipboard(CopyContent copyContent, bool needsValues)
    {
        const string content = "Centra la instrucción en decisiones.";
        var command = new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION, content,
            RequestedOutcomes: [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT], CopyContent: copyContent);
        command.Validate(new(Guid.NewGuid(), Guid.NewGuid(), content, SourceId: source.Id, SourceRevision: source.Revision));
        var plan = command.Compile(content, SemanticContext.Empty with { Selected = source });
        Assert.Equal([SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT], plan.Goal!.RequestedEffects);
        Assert.Equal(needsValues, plan.RequiresCompiledOutput);
        Assert.Equal(needsValues, plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.Empty(plan.Goal.ApprovalRequirements);
        Assert.DoesNotContain(SemanticOperation.COPY_TO_CLIPBOARD, plan.Intentions);
        Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL, plan.Intentions);
        Assert.Contains(copyContent == CopyContent.TEMPLATE ? CompletionCondition.TEMPLATE_VISIBLE : CompletionCondition.COMPILED_VISIBLE, plan.Goal.EffectiveCompletionConditions);
        Assert.True(plan.Goal.AllowsPlan(plan));
        Assert.False(plan.Goal.AllowsPlan(plan with { CopyContent = needsValues ? CopyContent.TEMPLATE : CopyContent.RENDERED }));
    }

    [Fact]
    public void TemplateReceiptCannotProveRenderedCopyAndRequiresAnExactCurrentBody()
    {
        var id = Guid.NewGuid();
        var state = new UiVisibleState("focus", [], source.Id, source.Revision, source.Title, source.Text,
            [], ["texto"], false, false, null, null, false, false) {
            Clipboard = new(id, source.Id, source.Revision, source.Text) { CopyContent = CopyContent.TEMPLATE }, TemplatePreviewText = source.Text
        };
        Assert.True(GoalCompletionValidator.ClipboardMatches(state, CopyContent.TEMPLATE));
        Assert.False(GoalCompletionValidator.ClipboardMatches(state));
        Assert.False(GoalCompletionValidator.ClipboardMatches(state with { TemplateText = "Contenido cambiado" }, CopyContent.TEMPLATE));
        Assert.False(GoalCompletionValidator.ClipboardMatches(state with { Revision = source.Revision + 1 }, CopyContent.TEMPLATE));
    }

    [Fact]
    public void CompoundUiChoiceCannotAcquireExecutionOrPersistenceAsAnAdditionalOutcome()
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Cambio", SourceId: source.Id, SourceRevision: source.Revision);
        foreach (var extra in new[] { SemanticOperation.EXECUTE_LOCAL, SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_DELETE })
            Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION, "Cambio",
                RequestedOutcomes: [SemanticOperation.PROPOSE_ADAPTATION, extra]).Validate(input));
    }

    [Fact]
    public void TemplateCopyDoesNotConsumeHiddenCaseValuesAndLegacyCommandFingerprintStaysStable()
    {
        const string message = "Copia la plantilla.";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, SourceId: source.Id, SourceRevision: source.Revision,
            Values: new() { ["texto"] = "Datos de otro caso" });
        var plan = new LiveCommand(SemanticOperation.COPY_TO_CLIPBOARD, CopyContent: CopyContent.TEMPLATE)
            .Compile(message, SemanticContext.Empty with { Selected = source });
        var state = new UiVisibleState("focus", [], source.Id, source.Revision, source.Title, source.Text, [], ["texto"], false, false, null, null, false, false);
        Assert.Empty(UiToolCatalog.GroundedValues(input, state, plan));
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_copy_compiled_prompt"));
        Assert.DoesNotContain(UiToolCatalog.All, tool => tool.Name == "ui_copy_template" || tool.Name == "ui_show_template");
        Assert.Equal(LiveRuns.Hash(new { Operation = SemanticOperation.COPY_TO_CLIPBOARD, Content = "", ProviderId = (string?)null }),
            LiveRuns.Hash(new LiveCommand(SemanticOperation.COPY_TO_CLIPBOARD)));
    }

    [Theory]
    [InlineData(SemanticOperation.OPEN_PROMPT, false)]
    [InlineData(SemanticOperation.FILL_VARIABLES, false)]
    [InlineData(SemanticOperation.EXECUTE_LOCAL, true)]
    [InlineData(SemanticOperation.PREPARE_LOCAL_EXECUTION, false)]
    public void ExplicitOperationSurvivesCompilationAndRetainsRealIdentity(SemanticOperation operation, bool executes)
    {
        const string request = "Acción elegida por el usuario.";
        var command = new LiveCommand(operation);
        command.Validate(new(Guid.NewGuid(), Guid.NewGuid(), request, SourceId: source.Id, SourceRevision: source.Revision));
        var plan = command.Compile(request, SemanticContext.Empty with { Selected = source });
        Assert.Contains(operation, plan.Intentions);
        Assert.Equal(executes, plan.Executes);
        Assert.Equal(source.Id, plan.ResolvedTargetId);
        Assert.True(plan.Goal!.AllowsPlan(plan));
        Assert.Empty(plan.Goal.ApprovalRequirements);
        if (executes) Assert.Contains(CompletionCondition.LOCAL_RESPONSE, plan.Goal.CompletionConditions);
    }

    [Theory]
    [InlineData(SemanticOperation.CREATE_DRAFT, SemanticOperation.REQUEST_SAVE_COPY)]
    [InlineData(SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.REQUEST_SAVE_VERSION)]
    public void AuthorshipRequiresReviewAndNeverAuthorizesExecution(SemanticOperation operation, SemanticOperation persistence)
    {
        const string content = "Resume documentos y conserva sus hechos.";
        var context = operation == SemanticOperation.CREATE_DRAFT ? SemanticContext.Empty : SemanticContext.Empty with { Selected = source };
        var plan = new LiveCommand(operation, content).Compile(content, context);
        Assert.Contains(persistence, plan.Goal!.ApprovalRequirements);
        Assert.Contains(CompletionCondition.APPROVED_WRITE, plan.Goal.CompletionConditions);
        Assert.False(plan.Executes);
        Assert.True(plan.RequiresConfirmation);
        Assert.Equal(content, plan.Goal.ContentRequest);
    }

    [Fact]
    public void MissingSourceInventedContentAndUnsupportedEffectsAreRejected()
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Petición real.");
        Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.EXECUTE_LOCAL).Validate(input));
        Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.CREATE_DRAFT, "Inventado").Validate(input));
        Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.REQUEST_SAVE_VERSION).Validate(input));
    }

    [Fact]
    public void ExecutingAnInstructionWithoutInputsDoesNotRequireInventedFields()
    {
        var entity = source with { Text = "Devuelve LISTO.", InputNames = [] };
        var plan = new LiveCommand(SemanticOperation.EXECUTE_LOCAL).Compile("Ejecutar ahora.", SemanticContext.Empty with { Selected = entity });
        Assert.Contains(CompletionCondition.LOCAL_RESPONSE, plan.Goal!.CompletionConditions);
        Assert.DoesNotContain(CompletionCondition.VALUES_APPLIED, plan.Goal.CompletionConditions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFieldReplyResumesOnlyWhenItContainsNoNewTaskDirective(bool newDirective)
    {
        var document = new PromptDocument(source.Id, source.Title, "", source.Text, 0, false, false, source.Revision,
            DateTime.UtcNow, DateTime.UtcNow, 0, null, ["texto"], new Dictionary<string, string>());
        var sessionId = Guid.NewGuid(); const string request = "Preparar para el modelo local.";
        var command = new LiveCommand(SemanticOperation.PREPARE_LOCAL_EXECUTION, ProviderId: "ollama");
        var plan = command.Compile(request, SemanticContext.Empty with { Selected = source });
        var sessions = new AgentSessions();
        sessions.Put(new(sessionId, document, null, [], DateTime.UtcNow, PendingPlan: plan, PendingRequest: request) { PendingCommand = command });
        var inference = new NoInference();
        var planning = new NovaTaskPlanner(inference, new OneSource(document), sessions, inference);
        var reply = (newDirective ? "Ahora quiero otra actividad.\n" : "") + "texto: Un documento real";
        if (newDirective) {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => planning.Interpret(reply, sessionId, source.Id, source.Revision, "qwen3:8b", null, CancellationToken.None));
            Assert.Contains("explicit command", failure.Message); // The semantic interpreter was consulted, not bypassed.
            return;
        }
        var result = await planning.Interpret(reply, sessionId, source.Id, source.Revision, "qwen3:8b", null, CancellationToken.None);
        Assert.True(result.Interpretation.ContinuesPendingTask);
        Assert.Contains(SemanticOperation.PREPARE_LOCAL_EXECUTION, result.Interpretation.Intentions);
        Assert.False(result.Interpretation.Executes);
        Assert.Equal(source.Id, result.Interpretation.ResolvedTargetId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NaturalFieldContinuationPreservesAllEffectsWithoutReinterpretingTheTask(bool sourceChanged)
    {
        var original = new PromptDocument(source.Id, source.Title, "", source.Text, 0, false, false, source.Revision,
            DateTime.UtcNow, DateTime.UtcNow, 0, null, ["texto"], new Dictionary<string, string>());
        var current = sourceChanged ? original with { Revision = original.Revision + 1 } : original;
        var sessionId = Guid.NewGuid(); const string request = "Preparar y ejecutar con Ollama.";
        var plan = new LiveCommand(SemanticOperation.EXECUTE_LOCAL).Compile(request, SemanticContext.Empty with { Selected = source });
        var sessions = new AgentSessions();
        // No PendingCommand: this checkpoint can originate from natural interpretation.
        sessions.Put(new(sessionId, original, null, [], DateTime.UtcNow, PendingPlan: plan, PendingRequest: request));
        var inference = new NoInference(); var planning = new NovaTaskPlanner(inference, new OneSource(current), sessions, inference);
        if (sourceChanged) {
            await Assert.ThrowsAsync<ConflictException>(() => planning.Interpret("texto: Documento nuevo", sessionId, source.Id,
                current.Revision, "qwen3:8b", null, CancellationToken.None));
            return;
        }
        var result = await planning.Interpret("texto: Documento nuevo", sessionId, source.Id, source.Revision, "qwen3:8b", null, CancellationToken.None);
        Assert.True(result.Interpretation.ContinuesPendingTask);
        Assert.Equal(plan.Intentions, result.Interpretation.Intentions);
        Assert.Same(plan.Goal, result.Interpretation.Goal);
        Assert.Equal(plan.ProviderId, result.Interpretation.ProviderId);
        Assert.Equal(request, result.Context.PendingRequest);
    }

    [Theory]
    [InlineData(1200)]
    [InlineData(8000)]
    public void LiteralUiContentUsesTheUiBudgetWithoutBeingTreatedAsAModelSummary(int length)
    {
        var content = new string('x', length); var command = new LiveCommand(SemanticOperation.CREATE_DRAFT, content);
        command.Validate(new(Guid.NewGuid(), Guid.NewGuid(), content));
        var plan = command.Compile(content, SemanticContext.Empty);
        Assert.Equal(content, plan.Goal!.ContentRequest);
        Assert.Equal(content, plan.RequestedTransformation);
    }

    [Fact]
    public void ExplicitCreationBindsTheWholeWorkOrderWithoutDeclaringTitleAndWorkflowAsFutureBody()
    {
        const string request = "Crea un prompt titulado «Acciones» con cuerpo literal «Resume <notas>.». Pide revisión antes de guardarlo.";
        var command = new LiveCommand(SemanticOperation.CREATE_DRAFT, request);
        command.Validate(new(Guid.NewGuid(), Guid.NewGuid(), request));
        var plan = command.Compile(request, SemanticContext.Empty);
        var clause = Assert.Single(plan.Goal!.Clauses);
        Assert.Equal(TaskClauseRole.WORK_ORDER, clause.Role);
        Assert.Equal(request, clause.Quote); Assert.Equal(request, plan.Goal.ContentRequest);
        Assert.Null(clause.ContentChange);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_COPY, plan.Goal.ApprovalRequirements);
        Assert.False(plan.Executes); Assert.False(plan.Has(SemanticOperation.COPY_TO_CLIPBOARD));
    }
    private sealed class NoInference : ISemanticIntentInterpreter, ICandidateSelector
    {
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct) => throw new InvalidOperationException("The explicit command already owns this task.");
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct) => throw new InvalidOperationException("No search is needed for a bound identity.");
    }
    private sealed class OneSource(PromptDocument document) : IPromptRetriever
    {
        public RetrievalCandidate[] Retrieve(string query, Guid? sourceId) => [new(document, 10, "Origen real de prueba")];
    }
}
