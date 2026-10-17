using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LiveExecutionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-live-tests-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    private readonly LiveRunStore records;
    private readonly LiveRuns live;
    public LiveExecutionTests()
    { store = new(Path.Combine(directory, "library.sqlite")); var prompts = new NovaPromptStore(store); library = new(prompts, prompts); records = new(store); live = new(records, library); }
    private LiveStartInput Start(string message = "PRIVATE_REQUEST_SENTINEL") { var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message); live.Create(input); return input; }
    private UiVisibleState State(PromptDocument source, bool changed = false) => new("create", [], source.Id, source.Revision, source.Title,
        changed ? "Nueva instrucción breve, sin inventar información." : source.TemplateText, [], [], changed, false, null, null, false, false);
    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    public void SelectedVisibleSourceCanBeOpenedDirectlyAndIsInspectedBeforeAskingItsPurpose(bool visible, bool searched, bool canOpen, bool canAsk)
    {
        var id = Guid.NewGuid(); var candidates = visible ? new[] { new UiPrompt(id, "Seleccionado", 1) } : new[] { new UiPrompt(Guid.NewGuid(), "Otro prompt", 1) };
        var state = new UiVisibleState("discover", candidates, null, null, null, null, [], [], false, false, null, null, false, false);
        var allowed = UiToolCatalog.Available(state, searched, false, false, true, requestedSourceId: id);
        Assert.Equal(canOpen, allowed.Any(tool => tool.Name == "ui_open_prompt")); Assert.Equal(canAsk, allowed.Any(tool => tool.Name == "ask_clarification"));
        Assert.Contains(allowed, tool => tool.Name == "ui_search_prompt");
    }
    [Fact] public async Task EventsAreAtomicMonotonicAndReplayDoesNotRepeatAnyEffect()
    {
        var input = Start();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Resultado comprobado."))));
        var events = live.Events(input.RunId, 0); Assert.Equal(33, events.Length); Assert.Equal(Enumerable.Range(1, 33).Select(i => (long)i), events.Select(e => e.Sequence));
        Assert.Equal(events.Skip(15).Select(e => e.EventId), live.Events(input.RunId, 15).Select(e => e.EventId));
        Assert.Equal(33, live.Create(input).Sequence); Assert.DoesNotContain("PRIVATE_REQUEST_SENTINEL", JsonSerializer.Serialize(events)); Assert.DoesNotContain("PRIVATE_REQUEST_SENTINEL", JsonSerializer.Serialize(live.Get(input.RunId)));
    }
    [Fact] public void ReusingARunIdForAnotherRequestAndLaunchingTwiceAreRejected()
    {
        var input = Start(); Assert.True(live.TryLaunch(input.RunId)); Assert.False(live.TryLaunch(input.RunId));
        Assert.Throws<ConflictException>(() => live.Create(input with { Message = "Otra solicitud" }));
        Assert.Throws<ConflictException>(() => live.Create(new(Guid.NewGuid(), Guid.NewGuid(), "Segunda ejecución simultánea")));
    }
    [Fact] public async Task PausePreventsNewOperationsUntilResumeAndCancellationReleasesWaiters()
    {
        var input = Start(); live.Pause(input.RunId); var waiting = live.Boundary(input.RunId, CancellationToken.None); Assert.False(waiting.IsCompleted);
        Assert.Equal(LiveStatus.PAUSED, live.Get(input.RunId).Status); live.Resume(input.RunId); await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        live.Pause(input.RunId); var cancelled = live.Boundary(input.RunId, CancellationToken.None); live.Cancel(input.RunId);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled); Assert.Equal(LiveStatus.CANCELLED, live.Get(input.RunId).Status);
    }
    [Fact] public void RestartBlocksInterruptedRunsWithoutReplayingAndPreservesEvents()
    {
        var input = Start(); live.Emit(input.RunId, LiveStatus.TOOL_RUNNING, LiveActor.MODEL_SELECTED, "Acción validada.", tool: "ui_open_prompt");
        var recovered = new LiveRunStore(new LocalStore(Path.Combine(directory, "library.sqlite")));
        Assert.Equal(LiveStatus.BLOCKED, recovered.Get(input.RunId)!.Status); Assert.Equal("INTERRUPTED_NO_REPLAY", recovered.Get(input.RunId)!.ErrorCode);
        Assert.False(recovered.TryCreate(live.Get(input.RunId))); Assert.Equal(3, recovered.Events(input.RunId, 0).Length);
    }
    [Fact] public void ApprovalBindsRunProposalArgumentsAndRevisionAndCommitIsSingleUse()
    {
        var source = library.Create(new("Ejemplo", "Instrucción original extensa. No inventes información.")); var input = Start();
        live.ProposeSave(input.RunId, State(source, true)); var proposal = live.View(input.RunId).Proposal!;
        Assert.Throws<ValidationException>(() => live.Approve(input.RunId, new(Guid.NewGuid(), proposal.Hash, source.Revision, true)));
        Assert.Throws<ValidationException>(() => live.Approve(input.RunId, new(proposal.Id, "otra propuesta", source.Revision, true)));
        Assert.Throws<ValidationException>(() => live.Approve(input.RunId, new(proposal.Id, proposal.Hash, source.Revision, false)));
        Assert.Throws<ConflictException>(() => live.Commit(input.RunId, proposal.Draft));
        live.Approve(input.RunId, new(proposal.Id, proposal.Hash, source.Revision, true));
        Assert.Throws<ConflictException>(() => live.Commit(input.RunId, proposal.Draft with { TemplateText = "contenido cambiado después de aprobar" }));
        var saved = live.Commit(input.RunId, proposal.Draft); Assert.Equal(source.Id, saved.Id); Assert.Equal(2, saved.Revision); Assert.Equal(2, store.Versions(source.Id).Count);
        Assert.Throws<ConflictException>(() => live.Commit(input.RunId, proposal.Draft)); Assert.Equal(2, library.Get(source.Id).Revision);
    }
    [Fact] public void CancelledOrPausedRunsCannotCommitApprovedPendingWrites()
    {
        var source = library.Create(new("Ejemplo", "Instrucción original extensa.")); var input = Start(); live.ProposeSave(input.RunId, State(source, true)); var p = live.View(input.RunId).Proposal!;
        live.Pause(input.RunId); Assert.Throws<ConflictException>(() => live.Approve(input.RunId, new(p.Id, p.Hash, p.Revision, true)));
        live.Resume(input.RunId); live.Approve(input.RunId, new(p.Id, p.Hash, p.Revision, true)); live.Cancel(input.RunId);
        Assert.Throws<ConflictException>(() => live.Commit(input.RunId, p.Draft)); Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id))); Assert.Single(store.Versions(source.Id));
    }
    [Fact] public void ChangedSourceDuringPauseBlocksResumeWithoutOverwritingIt()
    {
        var source = library.Create(new("Ejemplo", "Original.")); var input = Start(); live.ProposeSave(input.RunId, State(source, true)); live.Pause(input.RunId);
        var manual = library.Update(source.Id, new(source.Title, "Cambio manual autorizado.", Revision: source.Revision)); live.Resume(input.RunId);
        Assert.Equal(LiveStatus.BLOCKED, live.Get(input.RunId).Status); Assert.Equal(JsonSerializer.Serialize(manual), JsonSerializer.Serialize(library.Get(source.Id)));
    }
    [Fact] public void EditingAPendingProposalInvalidatesItsOldApprovalAndBindsTheNewContents()
    {
        var source = library.Create(new("Ejemplo", "Instrucción original extensa.")); var input = Start(); var state = State(source, true); live.Active(input.RunId).Observation = state;
        live.ProposeSave(input.RunId, state); var first = live.View(input.RunId).Proposal!;
        live.RefreshProposal(input.RunId, first.Draft with { TemplateText = "Mi edición revisada por el usuario." }); var revised = live.View(input.RunId).Proposal!;
        Assert.NotEqual(first.Id, revised.Id); Assert.NotEqual(first.Hash, revised.Hash); Assert.Equal(1, library.Get(source.Id).Revision);
        Assert.Throws<ValidationException>(() => live.Approve(input.RunId, new(first.Id, first.Hash, first.Revision, true)));
        live.Approve(input.RunId, new(revised.Id, revised.Hash, revised.Revision, true)); var result = live.Commit(input.RunId, revised.Draft);
        Assert.Equal("Mi edición revisada por el usuario.", result.TemplateText); Assert.Equal(2, result.Revision);
    }
    [Fact] public void SalutationsCannotBecomeVariableValuesAndNewLabeledDataOverridesEarlierContext()
    {
        var source = library.Create(new("Correo", "Escribe a <destinatario> sobre <tema>.")); var state = State(source) with { VariableNames = ["destinatario", "tema"] };
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "NOVA, hazlo más breve."); Assert.Empty(UiToolCatalog.GroundedValues(input, state));
        var updated = input with { Message = "Completa sus variables.\ndestinatario: Ana", Values = new() { ["destinatario"] = "Luis" } };
        Assert.Equal("Ana", UiToolCatalog.GroundedValues(updated, state)["destinatario"]);
    }
    [Fact] public void NewAuthorizedValuesMustReachTheUiBeforeLocalExecutionEvenWhenADefaultCompiles()
    {
        var source = library.Create(new("Ejemplo", "Revisa <proyecto>.")); var state = State(source) with { Mode = "focus", VariableNames = ["proyecto"], Values = new() { ["proyecto"] = "anterior" }, CompiledReady = true, CompiledText = "Revisa anterior." };
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Utilízalo con Qwen local.", Values: new() { ["proyecto"] = "nuevo" });
        var allowed = UiToolCatalog.Available(state, true, false, true, false); var action = new UiDecision("ui_execute_local", JsonSerializer.SerializeToElement(new { promptId = source.Id }));
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(action, allowed, state, input));
        UiToolCatalog.Validate(action, allowed, state with { Values = new() { ["proyecto"] = "nuevo" }, CompiledText = "Revisa nuevo." }, input,
            SemanticFixture.Plan([SemanticOperation.COMPILE_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL], outcome: SemanticOutcome.LOCAL_RESPONSE) with { ExecutionEvidence = input.Message });
    }
    [Fact] public void CombinedAdaptAndRunCannotUseAnOldCompiledPreviewOrAnUnvalidatedPriorDraft()
    {
        var source = library.Create(new("Ejemplo", "Revisa <proyecto>."));
        var state = State(source) with { Mode = "create", VariableNames = ["proyecto"], Values = new() { ["proyecto"] = "API" }, CompiledReady = true, CompiledText = "Revisa API.", DraftChanged = true };
        var before = UiToolCatalog.Available(state, true, false, true, true, requestedSourceId: source.Id);
        Assert.DoesNotContain(before, t => t.Name is "ui_execute_local" or "ui_open_provider_picker" or "ui_show_compiled_prompt" or "ui_fill_variable");
        var after = UiToolCatalog.Available(state, true, false, true, true, requestedSourceId: source.Id, changeValidated: true);
        Assert.Contains(after, t => t.Name == "ui_execute_local"); Assert.Contains(after, t => t.Name == "ui_fill_variable");
        Assert.DoesNotContain(UiToolCatalog.Available(state with { DraftChanged = false }, true, false, true, true, changeValidated: true), t => t.Name == "ui_execute_local");
        var wrongTarget = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Ejecuta ahora.", SourceId: Guid.NewGuid(), SourceRevision: 1);
        var plan = SemanticFixture.Plan([SemanticOperation.EXECUTE_LOCAL]) with { ExecutionEvidence = "Ejecuta ahora." };
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(new("ui_execute_local", JsonSerializer.SerializeToElement(new { promptId = source.Id })), after, state, wrongTarget, plan));
    }
    [Fact] public void MetadataNamesTheConfiguredDirectModelAndBrowserModeNeverSubstitutesAnotherModel()
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Mejora el prompt.", "LIVE", Model: "qwen2.5:7b-instruct");
        Assert.Equal("qwen2.5:7b-instruct", live.Create(input).Model); live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Prueba de metadatos, sin inferencia.");
        Assert.Throws<ValidationException>(() => live.Create(new(Guid.NewGuid(), Guid.NewGuid(), "Abre un prompt.", Model: "qwen2.5:7b-instruct")));
    }
    [Fact] public void ExpiredTemporaryContentIsNotReturnedByTheDiagnosticView()
    {
        var input = Start(); live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Finalizado."); live.Active(input.RunId).Touched = DateTime.UtcNow.AddHours(-3);
        var view = live.View(input.RunId); Assert.Null(view.Request); Assert.Null(view.Observation); Assert.Null(view.Proposal); Assert.Empty(view.Decisions); Assert.Equal(LiveStatus.COMPLETED, view.Run.Status);
    }
    [Fact] public void QuotedTitlesAndSuppliedMaterialCannotGrantEditingPermission()
    {
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "ui_prepare_update"));
    }
    [Fact] public void ObservedAmbiguityCannotBeBypassedByNarrowingTheSearchWithoutUserClarification()
    {
        var state = new UiVisibleState("discover", [new(Guid.NewGuid(), "Correo para clientes", 1), new(Guid.NewGuid(), "Correo para equipo", 1)], null, null, null, null, [], [], false, false, null, null, false, false);
        var actions = UiToolCatalog.Available(state, true, true, false, false); Assert.Contains(actions, action => action.Name == "ask_clarification");
        Assert.DoesNotContain(actions, action => action.Name is "ui_search_prompt" or "ui_open_prompt" or "finish");
    }
    [Fact] public void RestoreCannotDiscardALiveRunAndKeepsItsReplayReceiptAfterCancellation()
    {
        var archive = store.Export(); var input = Start();
        Assert.Throws<ConflictException>(() => store.NovaRestore(new(JsonDocument.Parse(archive).RootElement, true, store.NovaSnapshot())));
        live.Cancel(input.RunId); store.NovaRestore(new(JsonDocument.Parse(archive).RootElement, true, store.NovaSnapshot()));
        Assert.False(records.TryCreate(new(input.RunId, input.SessionId, "different", "BROWSER", LiveStatus.CREATED, 0, DateTime.UtcNow)));
        var next = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Otra solicitud después de restaurar."); Assert.Equal(next.RunId, live.Create(next).RunId);
    }
    [Fact]
    public void ReadOnlyToolCatalogDoesNotExposeExecution()
    { Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "ui_execute_local")); Assert.DoesNotContain(UiToolCatalog.Available(new("focus", [], Guid.NewGuid(), 1, "Ejemplo", "Original.", [], [], false, true, "Original.", null, false, false), true, false, false, false), t => t.Name == "ui_execute_local"); }
    [Fact]
    public void ReadPlanRejectsUnplannedUpdate()
    { Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(SemanticFixture.Read, "propose_prompt_update")); }
    [Fact] public void SchemasRejectUnknownSelectorsScriptsIdsAndUngroundedValues()
    {
        var source = library.Create(new("Correo", "Escribe a <persona>.")); var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "persona: Ana");
        var state = State(source) with { Mode = "focus", Values = new() { ["persona"] = "" }, VariableNames = ["persona"] };
        var allowed = UiToolCatalog.Available(state, true, false, false, false);
        Assert.Throws<PreparationToolException>(() => UiToolCatalog.Validate(new("page.evaluate", JsonSerializer.SerializeToElement(new { script = "PRIVATE_PAYLOAD" })), allowed, state, input));
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(new("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = source.Id, name = "persona", value = "Luis", evidence = "persona: Ana" })), allowed, state, input));
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(new("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = source.Id, name = "persona", value = "Ana", evidence = "persona: Ana", selector = "#secret" })), allowed, state, input));
        UiToolCatalog.Validate(new("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = source.Id, name = "persona", value = "Ana", evidence = "persona: Ana" })), allowed, state, input);
    }
    [Fact] public async Task InvalidModelToolHasNoFallbackAndClosesTheWorkerWithoutMutation()
    {
        var input = Start("Abre un prompt."); var inference = new UnusedInference(); var browser = new FaultBrowser();
        var fakeSemantic = new FixedSemantic(SemanticFixture.Plan([SemanticOperation.SEARCH_PROMPT], query: "arquitectura"));
        var engine = new AgentOrchestrator(inference, new SqlitePromptRetriever(store), inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), semanticPlanning: new(fakeSemantic, new SqlitePromptRetriever(store), new(), fakeSemantic));
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Equal(LiveStatus.BLOCKED, live.Get(input.RunId).Status); Assert.Equal("UI_TOOL_NOT_ALLOWED", live.Get(input.RunId).ErrorCode); Assert.True(browser.Disposed);
        Assert.Equal(["ui_read_visible_state"], browser.Tools); Assert.Empty(store.Templates()); Assert.False(live.Get(input.RunId).BrowserOpen);
        Assert.DoesNotContain("PRIVATE_INVALID_TOOL", JsonSerializer.Serialize(live.Events(input.RunId, 0)));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyVisibleQuestionRetainsTheLiteralPendingTaskAndAdviceDoesNotOpenABrowser(bool advice)
    {
        const string request = "Orienta la instrucción anterior a una API.";
        var input = Start(request); var inference = new UnusedInference(); var browser = new FaultBrowser(); var sessions = new AgentSessions();
        var pending = SemanticPlanning.Clarify(SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.PREVIOUS_RESULT, outcome: SemanticOutcome.DRAFT), "¿Cuál instrucción?");
        var plan = advice ? SemanticFixture.Plan([SemanticOperation.EXPLAIN], outcome: SemanticOutcome.ADVICE) with { Explanation = "Puedes revisar el borrador antes de guardarlo." } : pending;
        var model = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, semanticPlanning: new(model, retriever, sessions, model));
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Empty(browser.Tools); Assert.False(browser.Disposed); Assert.Empty(store.Templates());
        if (advice) { Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status); Assert.Null(sessions.Get(input.SessionId)?.PendingRequest); }
        else { Assert.Equal(LiveStatus.BLOCKED, live.Get(input.RunId).Status); Assert.Equal(request, sessions.Get(input.SessionId)!.PendingRequest); Assert.NotNull(sessions.Get(input.SessionId)!.PendingPlan); }
    }
    [Theory]
    [InlineData(SemanticOperation.CANCEL, SemanticOutcome.CANCELLED)]
    [InlineData(SemanticOperation.PAUSE, SemanticOutcome.PAUSED)]
    [InlineData(SemanticOperation.RESUME, SemanticOutcome.RESUMED)]
    public async Task SourceFreeVisibleControlsNeverLaunchABrowser(SemanticOperation operation, SemanticOutcome outcome)
    {
        var source = library.Create(new("Original", "Texto guardado intacto.")); var input = Start("Control de una tarea anterior.");
        var inference = new UnusedInference(); var browser = new FaultBrowser(); var sessions = new AgentSessions();
        sessions.Put(new(input.SessionId, source, null, [], DateTime.UtcNow, PendingRequest: "Petición pendiente literal."));
        var model = new FixedSemantic(SemanticFixture.Plan([operation], outcome: outcome)); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, semanticPlanning: new(model, retriever, sessions, model));
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Empty(browser.Tools); Assert.False(browser.Disposed); Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id))); Assert.Single(store.Versions(source.Id));
        Assert.Equal(operation == SemanticOperation.CANCEL ? LiveStatus.CANCELLED : LiveStatus.BLOCKED, live.Get(input.RunId).Status);
        if (operation == SemanticOperation.CANCEL) Assert.Null(sessions.Get(input.SessionId));
        else Assert.Equal("Petición pendiente literal.", sessions.Get(input.SessionId)!.PendingRequest);
    }
    [Fact] public async Task AnUncertainExtractedValueAsksBeforeWritingInsteadOfSilentlyUsingAFragment()
    {
        var source = library.Create(new("Ficha", "Describe <tema>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Completa la ficha con una base de datos SQLite.", SourceId: source.Id, SourceRevision: source.Revision);
        live.Create(input); var inference = new UncertainExtraction(); var sessions = new AgentSessions();
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES, SemanticOperation.COMPILE_PROMPT], TargetReference.SELECTED, source.Id);
        var semantic = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, semanticPlanning: new(semantic, retriever, sessions, semantic));
        var state = State(source) with { Mode = "focus", Values = new() { ["tema"] = "" }, VariableNames = ["tema"] };
        var browser = new ObservedBrowser(state);
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Equal(LiveStatus.BLOCKED, live.Get(input.RunId).Status); Assert.Equal("NEEDS_CLARIFICATION", live.Get(input.RunId).ErrorCode);
        Assert.Equal("¿Qué descripción quieres usar?", live.View(input.RunId).Question);
        Assert.Equal(["ui_read_visible_state"], browser.Tools); Assert.Equal("", live.View(input.RunId).Observation!.Values["tema"]);
        Assert.Equal(input.Message, sessions.Get(input.SessionId)!.PendingRequest);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id))); Assert.Single(store.Versions(source.Id));
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CompletedLocalExecutionDoesNotReopenAnExternalPickerOrAskForUnneededData(bool previousResponse, bool bindSource)
    {
        var source = library.Create(new("Ficha", "Explica <tema>."));
        const string message = "Ejecuta con el modelo local ahora.\ntema: SQLite";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, SourceId: bindSource ? source.Id : null, SourceRevision: bindSource ? source.Revision : null); live.Create(input);
        var inference = new UnusedInference(); var sessions = new AgentSessions();
        sessions.Put(new(input.SessionId, source, null, new() { ["tema"] = "SQLite" }, DateTime.UtcNow));
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES, SemanticOperation.COMPILE_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.LOCAL_RESPONSE)
            with { ProviderId = "ollama", ExecutionEvidence = "Ejecuta con el modelo local ahora." };
        var semantic = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, semanticPlanning: new(semantic, retriever, sessions, semantic));
        var state = State(source) with { Mode = "focus", Values = new() { ["tema"] = "SQLite" }, VariableNames = ["tema"], CompiledReady = true, CompiledText = "Explica SQLite.", ProviderOpen = true, ProviderId = "chatgpt", ResponseComplete = previousResponse, Response = previousResponse ? "Respuesta anterior." : null };
        var browser = new LocalResultBrowser(state, () => {
            Assert.Equal(source.Id, live.Get(input.RunId).TargetId);
            Assert.Equal(source.Revision, live.Get(input.RunId).Revision);
            Assert.Equal(state.CompiledText, live.Active(input.RunId).Observation!.CompiledText);
        }); var decisions = new CompleteLocalDecision(source.Id);
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, decisions, browser, library);
        Assert.Equal(2, decisions.Calls); Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status);
        Assert.Equal(1, browser.Tools.Count(tool => tool == "ui_execute_local")); Assert.DoesNotContain("ui_open_provider_picker", browser.Tools);
        Assert.Equal("Respuesta observada.", live.View(input.RunId).Observation!.Response);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id)));
    }
    private sealed class CompleteLocalDecision(Guid id) : IUiDecisionModel
    {
        public int Calls;
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct)
        {
            Calls++;
            Assert.DoesNotContain(context.Tools, tool => tool.Name == "ui_open_provider_picker");
            if (Calls == 1) return Task.FromResult(new UiDecision("ui_execute_local", JsonSerializer.SerializeToElement(new { promptId = id })));
            Assert.DoesNotContain(context.Tools, tool => tool.Name == "ask_clarification");
            Assert.Contains(context.Tools, tool => tool.Name == "finish");
            return Task.FromResult(new UiDecision("finish", JsonSerializer.SerializeToElement(new { })));
        }
    }
    private sealed class LocalResultBrowser(UiVisibleState initial, Action? beforeExecute = null) : IVisibleBrowser, IVisibleBrowserFactory
    {
        private UiVisibleState state = initial; public List<string> Tools = [];
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(state);
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct) { Tools.Add(tool); if (tool == "ui_execute_local") { beforeExecute?.Invoke(); state = state with { Response = "Respuesta observada.", ResponseComplete = true }; } return Task.FromResult(state); }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class UncertainExtraction : IIntentInterpreter, IPromptGenerator, IInputValueExtractor
    {
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new NotSupportedException();
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct) => Task.FromResult(new InputValueExtraction([new("tema", "SQLite", "SQLite")], "¿Qué descripción quieres usar?"));
    }
    private sealed class ObservedBrowser(UiVisibleState state) : IVisibleBrowser, IVisibleBrowserFactory
    {
        public List<string> Tools = [];
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(state);
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct) { Tools.Add(tool); return Task.FromResult(state); }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class UnusedInference : IIntentInterpreter, IPromptGenerator
    { public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException(); public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new NotSupportedException(); }
    private sealed class InvalidDecision : IUiDecisionModel { public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct) => Task.FromResult(new UiDecision("PRIVATE_INVALID_TOOL", JsonSerializer.SerializeToElement(new { }))); }
    private sealed class FaultBrowser : IVisibleBrowser, IVisibleBrowserFactory
    {
        public bool Disposed; public List<string> Tools = []; private static readonly UiVisibleState State = new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false);
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(State);
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct) { Tools.Add(tool); return Task.FromResult(State); }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
