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
    [InlineData("MODEL_FAILED")]
    [InlineData("MODEL_TIMEOUT")]
    [InlineData("MODEL_UNAVAILABLE")]
    [InlineData("LOCAL_DISPATCH_TIMEOUT")]
    public async Task GenerationProviderFailuresKeepTheirDiagnosisAndReleaseControlWithoutSaving(string code)
    {
        const string request = "Crea una instrucción para resumir notas.";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), request,
            Command: new(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] });
        live.Create(input); var inference = new FailingContent(code); var sessions = new AgentSessions();
        var engine = new AgentOrchestrator(new UnusedInference(), new SqlitePromptRetriever(store), inference,
            new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(),
            sessions: sessions, executeGroundedPlan: true, stopAfterVerifiedResult: true);
        var browser = new CreationBrowser(new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false));
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Equal(code, live.Get(input.RunId).ErrorCode); Assert.Equal(LiveStatus.FAILED, live.Get(input.RunId).Status);
        Assert.False(live.Get(input.RunId).BrowserOpen); Assert.False(live.Get(input.RunId).EffectCommitted);
        Assert.Equal(1, inference.Calls); Assert.Empty(store.Templates()); Assert.Null(live.View(input.RunId).Proposal);
        Assert.DoesNotContain("ui_prepare_creation", browser.Tools); Assert.DoesNotContain("commit_approved", browser.Tools);
    }

    private sealed class FailingContent(string code) : IPromptGenerator
    {
        public int Calls;
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        { Calls++; throw new LlmUnavailableException("Synthetic provider failure", code); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisibleReadDoesNotDemandOrApplyIncidentalCaseValues(bool withGoal)
    {
        var source=library.Create(new("Lectura protegida","Explica <tema>."));
        var before=JsonSerializer.Serialize(library.Get(source.Id));
        const string request="Abre este prompt sólo para leerlo.\ntema: dato incidental";
        var input=new LiveStartInput(Guid.NewGuid(),Guid.NewGuid(),request,SourceId:source.Id,SourceRevision:source.Revision);
        live.Create(input);var sessions=new AgentSessions();var inference=new RejectedExtraction();var retriever=new SqlitePromptRetriever(store);
        var plan=SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT,SemanticOperation.OPEN_PROMPT],TargetReference.SELECTED,source.Id,outcome:SemanticOutcome.READ);
        if(withGoal)plan=plan with {Goal=TaskGoal.Create(request,SemanticContext.Empty with {Selected=SemanticEntity.From(source)},plan,
            [new(TaskClauseRole.WORKFLOW,request)],[SemanticOperation.OPEN_PROMPT])};
        var model=new FixedSemantic(plan);
        var engine=new AgentOrchestrator(inference,retriever,inference,new GroundedVariableResolver(),new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store),new(),sessions:sessions,taskPlanner:new NovaTaskPlanner(model,retriever,sessions,model),executeGroundedPlan:true,stopAfterVerifiedResult:true);
        var browser=new ObservedBrowser(State(source) with {Mode="focus",VariableNames=["tema"],Values=new(){["tema"]=""}});
        live.Active(input.RunId).Cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        await engine.RunVisible(input,"http://127.0.0.1:5226",live,new InvalidDecision(),browser,library);
        Assert.True(live.Get(input.RunId).Status==LiveStatus.COMPLETED,JsonSerializer.Serialize(live.View(input.RunId)));
        Assert.DoesNotContain("ui_fill_variable",browser.Tools);Assert.DoesNotContain("ui_execute_local",browser.Tools);
        Assert.Equal("",live.View(input.RunId).Observation!.Values["tema"]);
        Assert.Equal(before,JsonSerializer.Serialize(library.Get(source.Id)));Assert.Null(live.View(input.RunId).Proposal);
    }
    [Theory]
    [InlineData("session")]
    [InlineData("draft")]
    [InlineData("revision")]
    [InlineData("content")]
    [InlineData("source")]
    [InlineData("visible")]
    public void AForeignOrChangedWorkingCheckpointCannotBecomeASaveProposal(string invalid)
    {
        var input = Start();
        var draft = new DeterministicPromptEvaluator().Evaluate(new(input.RunId, "Crear explicación.", SessionId: input.SessionId),
            new(PreparationIntent.CREATE_PROMPT, "", ""), new(PreparationDecision.CREATE_NEW, null, "Explicación", "", "Explica el tema sin inventar datos.", [], "", "Creación sintética verificada."), [], []) with { SessionId = input.SessionId };
        live.Active(input.RunId).WorkingDraft = invalid == "source" ? draft with { SourceId = Guid.NewGuid(), SourceRevision = 1 } : draft;
        var reference = new DraftRef(invalid == "draft" ? Guid.NewGuid() : draft.Id, invalid == "session" ? Guid.NewGuid() : input.SessionId,
            invalid == "revision" ? 0 : 1, null, invalid == "content" ? "Otro contenido." : draft.TemplateText);
        var state = new UiVisibleState("create", [], null, null, draft.Title, invalid == "visible" ? "Cambio manual." : draft.TemplateText, [], [], true, false, null, null, false, false);
        Assert.Throws<ConflictException>(() => live.ProposeSave(input.RunId, state, workingReference: reference));
        Assert.Null(live.View(input.RunId).Proposal); Assert.Empty(store.Templates());
    }
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
        var engine = new AgentOrchestrator(inference, new SqlitePromptRetriever(store), inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), taskPlanner: new NovaTaskPlanner(fakeSemantic, new SqlitePromptRetriever(store), new(), fakeSemantic));
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
        var pending = TaskClarification.Apply(SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.PREVIOUS_RESULT, outcome: SemanticOutcome.DRAFT), "¿Cuál instrucción?");
        var plan = advice ? SemanticFixture.Plan([SemanticOperation.EXPLAIN], outcome: SemanticOutcome.ADVICE) with { Explanation = "Puedes revisar el borrador antes de guardarlo." } : pending;
        var model = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(model, retriever, sessions, model));
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
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(model, retriever, sessions, model));
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
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic));
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
    public Task CompletedLocalExecutionDoesNotReopenAnExternalPickerOrAskForUnneededData(bool previousResponse, bool bindSource)
        => VerifyCompletedLocalExecution(previousResponse, bindSource, false);
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task VerifiedLocalResultCompletesWithoutASecondModelDecision(bool previousResponse, bool bindSource)
        => VerifyCompletedLocalExecution(previousResponse, bindSource, true);
    private async Task VerifyCompletedLocalExecution(bool previousResponse, bool bindSource, bool stopAfterVerifiedResult)
    {
        var source = library.Create(new("Ficha", "Explica <tema>."));
        const string message = "Ejecuta con el modelo local ahora.\ntema: SQLite";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, SourceId: bindSource ? source.Id : null, SourceRevision: bindSource ? source.Revision : null); live.Create(input);
        var inference = new UnusedInference(); var sessions = new AgentSessions();
        sessions.Put(new(input.SessionId, source, null, new() { ["tema"] = "SQLite" }, DateTime.UtcNow));
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES, SemanticOperation.COMPILE_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.LOCAL_RESPONSE)
            with { ProviderId = "ollama", ExecutionEvidence = "Ejecuta con el modelo local ahora." };
        var semantic = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic), stopAfterVerifiedResult: stopAfterVerifiedResult);
        var state = State(source) with { Mode = "focus", Values = new() { ["tema"] = "SQLite" }, VariableNames = ["tema"], CompiledReady = true, CompiledText = "Explica SQLite.", ProviderOpen = true, ProviderId = "chatgpt", ResponseComplete = previousResponse, Response = previousResponse ? "Respuesta anterior." : null };
        var browser = new LocalResultBrowser(state, () => {
            Assert.Equal(source.Id, live.Get(input.RunId).TargetId);
            Assert.Equal(source.Revision, live.Get(input.RunId).Revision);
            Assert.Equal(state.CompiledText, live.Active(input.RunId).Observation!.CompiledText);
        }); var decisions = new CompleteLocalDecision(source.Id);
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, decisions, browser, library);
        Assert.Equal(stopAfterVerifiedResult ? 1 : 2, decisions.Calls); Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status);
        Assert.Equal(1, browser.Tools.Count(tool => tool == "ui_execute_local")); Assert.DoesNotContain("ui_open_provider_picker", browser.Tools);
        Assert.Equal("Respuesta observada.", live.View(input.RunId).Observation!.Response);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id)));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentCreationFromFocusDoesNotInheritTheOldSourceOrItsFieldValues(bool useSourceBasis)
    {
        var message = useSourceBasis ? "Crea otra instrucción basada en la actual para explicar de forma sencilla." : "Crea una instrucción independiente para explicar un tema.";
        var source = library.Create(new("Anterior", "Describe <tema>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, "SAME_TAB", SourceId: source.Id, SourceRevision: source.Revision,
            Values: new() { ["tema"] = "SYNTHETIC_PREVIOUS_VALUE" }); live.Create(input);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], useSourceBasis ? TargetReference.SELECTED : TargetReference.NONE, "", useSourceBasis ? source.Id : null, "", message,
            new([SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY, SemanticOperation.EXECUTE_LOCAL], true, null, null, [], [], false),
            SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear un borrador independiente."));
        plan = plan with { Goal = TaskGoal.Create(message, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan, [new(TaskClauseRole.CONTENT, message)]) };
        var semantic = new FixedSemantic(plan); var inference = new CreationOnlyInference(useSourceBasis ? source : null); var retriever = new SqlitePromptRetriever(store); var sessions = new AgentSessions();
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(),
            sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic), executeGroundedPlan: true);
        var browser = new CreationBrowser(State(source) with { Mode = "focus" });
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.True(live.Get(input.RunId).Status == LiveStatus.COMPLETED,
            $"{live.Get(input.RunId).ErrorCode}: {live.View(input.RunId).Question}; tools: {string.Join(',', browser.Tools)}");
        Assert.False(live.Get(input.RunId).EffectCommitted);
        Assert.Null(live.Active(input.RunId).Input.SourceId); Assert.Null(live.Active(input.RunId).Input.Values);
        Assert.Equal("", live.View(input.RunId).Observation!.Values["tema"]); Assert.DoesNotContain("ui_fill_variable", browser.Tools);
        Assert.Null(live.View(input.RunId).Observation!.PromptId); Assert.Single(store.Templates());
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id)));
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task VisibleUnsavedDraftSurvivesAFollowupAndInterruptedGeneration(bool interrupt, bool withGoal)
    {
        var sessions = new AgentSessions(); var retriever = new SqlitePromptRetriever(store); var sessionId = Guid.NewGuid();
        var browser = new CreationBrowser(new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false));
        var creation = new CreationOnlyInference();
        var first = new LiveStartInput(Guid.NewGuid(), sessionId, "Crea una explicación.", "SAME_TAB"); live.Create(first);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "Crear una explicación",
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        plan = plan with { Goal = TaskGoal.Create(first.Message, SemanticContext.Empty, plan, [new(TaskClauseRole.CONTENT, first.Message)], [SemanticOperation.CREATE_DRAFT]) };
        AgentOrchestrator Engine(IIntentInterpreter interpreter, IPromptGenerator generator, SemanticInterpretation task) {
            var semantic = new FixedSemantic(task);
            return new(interpreter, retriever, generator, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(),
                sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic), executeGroundedPlan: true);
        }
        await Engine(creation, creation, plan).RunVisible(first, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Equal(LiveStatus.COMPLETED, live.Get(first.RunId).Status);
        var original = sessions.Get(sessionId)!.UnsavedDraft!; Assert.NotNull(original); Assert.Null(original.SourceRevision);
        var reference = sessions.Get(sessionId)!.WorkingDraftReference!;
        Assert.Equal(sessionId, reference.SessionId); Assert.Equal(1, reference.DraftRevision); Assert.Null(reference.SourceRef);
        var next = new LiveStartInput(Guid.NewGuid(), sessionId, "Adapta este borrador para principiantes.", "SAME_TAB"); live.Create(next);
        var edit = SemanticPlanCompiler.Compile(new([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.ACTIVE_DRAFT, "", null, "", "Explicar para principiantes",
            new([], true, null, null, [], [], false), SemanticOutcome.DRAFT, [], false, "", null, "Adaptar"));
        var context = SemanticContext.Empty with { ActiveDraft = original.TemplateText, ActiveDraftInputNames = ["tema"] };
        if (withGoal) edit = edit with { Goal = TaskGoal.Create(next.Message, context, edit, [new(TaskClauseRole.CONTENT, next.Message)], [SemanticOperation.PROPOSE_ADAPTATION]) };
        var generation = new WorkingAdaptInference(original, interrupt);
        await Engine(generation, generation, edit).RunVisible(next, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.True(live.Get(next.RunId).Status == (interrupt ? LiveStatus.FAILED : LiveStatus.COMPLETED),
            $"{live.Get(next.RunId).ErrorCode}: {live.View(next.RunId).Question}; tools: {string.Join(',', browser.Tools)}");
        var retained = sessions.Get(sessionId)!.UnsavedDraft!;
        Assert.Equal(original.Id, retained.Id); Assert.Null(retained.SourceId); Assert.Empty(store.Templates());
        Assert.Equal(interrupt ? 1 : 2, sessions.Get(sessionId)!.WorkingDraftReference!.DraftRevision);
        Assert.Equal(interrupt ? original.TemplateText : WorkingAdaptInference.Body, retained.TemplateText);
        Assert.DoesNotContain("ui_execute_local", browser.Tools); Assert.DoesNotContain("ui_request_save_version", browser.Tools);
        if (!interrupt) {
            var save = new LiveStartInput(Guid.NewGuid(), sessionId, "Solicita aprobación para guardar este borrador como una copia.", "SAME_TAB"); live.Create(save);
            var savePlan = SemanticPlanCompiler.Compile(new([SemanticOperation.REQUEST_SAVE_COPY], TargetReference.ACTIVE_DRAFT, "", null, "", "",
                new([SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.EXECUTE_LOCAL], true, null, null, [], [], false), SemanticOutcome.REVIEW, [], true, "", null, "Guardar copia"));
            if (withGoal) savePlan = savePlan with { Goal = TaskGoal.Create(save.Message, context, savePlan, [new(TaskClauseRole.WORKFLOW, save.Message)], [SemanticOperation.REQUEST_SAVE_COPY]) };
            var task = Engine(generation, generation, savePlan).RunVisible(save, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!task.IsCompleted && live.Get(save.RunId).Status != LiveStatus.AWAITING_APPROVAL) await Task.Delay(10, deadline.Token);
            try {
                Assert.True(live.Get(save.RunId).Status == LiveStatus.AWAITING_APPROVAL, $"{live.Get(save.RunId).ErrorCode}: {live.View(save.RunId).Question}");
                var proposal = live.View(save.RunId).Proposal!;
                Assert.Null(proposal.PromptId); Assert.Equal(retained.TemplateText, proposal.Draft.TemplateText); Assert.Empty(store.Templates());
            } finally { live.Cancel(save.RunId); await task; }
        }
    }
    [Fact] public async Task LegacyDraftOnlyCreationCompletesWithoutRequestingUnauthorizedPersistence()
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Redacta una instrucción; conserva solo el borrador.", "SAME_TAB"); live.Create(input);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "Crear una explicación",
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear borrador."));
        Assert.Null(plan.Goal);
        var inference = new CreationOnlyInference(); var semantic = new FixedSemantic(plan); var retriever = new SqlitePromptRetriever(store); var sessions = new AgentSessions();
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(),
            sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic), executeGroundedPlan: true);
        var browser = new CreationBrowser(new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false));
        live.Active(input.RunId).Cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.True(live.Get(input.RunId).Status == LiveStatus.COMPLETED,
            $"{live.Get(input.RunId).ErrorCode}: {live.View(input.RunId).Question}; tools: {string.Join(',', browser.Tools)}");
        Assert.False(live.Get(input.RunId).EffectCommitted); Assert.Null(live.View(input.RunId).Proposal);
        Assert.Empty(store.Templates()); Assert.NotNull(sessions.Get(input.SessionId)!.UnsavedDraft);
    }
    private sealed class WorkingAdaptInference(PreparedDraft expected, bool interrupt) : IIntentInterpreter, IPromptGenerator
    {
        public const string Body = "Explica <tema> para principiantes con ejemplos cotidianos. No inventes datos.";
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) {
            Assert.Equal(expected.Id, input.WorkingDraft!.Id); Assert.Equal(expected.TemplateText, input.WorkingDraft.TemplateText);
            Assert.Null(input.SourcePromptId); Assert.Empty(candidates);
            if (interrupt) throw new OperationCanceledException();
            input.Interpretation!.Goal?.RecordContentAssessment(Body);
            return Task.FromResult(new PromptProposal(PreparationDecision.CREATE_NEW, null, expected.Title, "", Body, [], "", "Adaptación controlada."));
        }
    }
    private sealed class CreationOnlyInference(PromptDocument? expectedBasis = null) : IIntentInterpreter, IPromptGenerator
    {
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) {
            Assert.Null(input.SourcePromptId); Assert.Null(input.Values);
            if (expectedBasis is null) Assert.Empty(candidates);
            else { Assert.Single(candidates); Assert.Equal(expectedBasis.Id, candidates[0].Prompt.Id); Assert.Equal(expectedBasis.TemplateText, candidates[0].Prompt.TemplateText); }
            const string text="Explica <tema> de forma sencilla. No inventes datos.";
            input.Interpretation?.Goal?.RecordContentAssessment(text); // Controlled reviewer for this UI/identity fixture.
            return Task.FromResult(new PromptProposal(PreparationDecision.CREATE_NEW, null, "Explicar", "", text, [], "", "Nueva instrucción."));
        }
    }
    private sealed class CreationBrowser(UiVisibleState initial) : IVisibleBrowser, IVisibleBrowserFactory
    {
        private UiVisibleState state = initial; public List<string> Tools = [];
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(state);
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct) {
            Tools.Add(tool);
            if (tool == "ui_fill_variable") throw new InvalidOperationException("An old source value must not enter an independent draft.");
            if (tool == "ui_open_create_prompt") state = new("create", [], null, null, "", "", [], [], false, false, null, null, false, false);
            if (tool is "ui_prepare_creation" or "ui_prepare_update") state = state with { Title = args.GetProperty("title").GetString(), TemplateText = args.GetProperty("templateText").GetString(),
                DraftChanged = true, VariableNames = ["tema"], Values = new() { ["tema"] = "" } };
            return Task.FromResult(state);
        }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class CompleteLocalDecision(Guid id) : IUiDecisionModel
    {
        public int Calls;
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct)
        {
            Calls++;
            Assert.DoesNotContain(context.Tools, tool => tool.Name is "ui_read_visible_state" or "ui_verify_prompt_state");
            Assert.DoesNotContain(context.Tools, tool => tool.Name == "ui_open_provider_picker");
            if (Calls == 1) return Task.FromResult(new UiDecision("ui_execute_local", JsonSerializer.SerializeToElement(new { promptId = id })));
            Assert.DoesNotContain(context.Tools, tool => tool.Name == "ask_clarification");
            Assert.DoesNotContain(context.Tools, tool => tool.Name is "ui_read_visible_state" or "ui_verify_prompt_state" or "ui_show_compiled_prompt");
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
    [Theory]
    [InlineData(SemanticOperation.FILL_VARIABLES, LiveStatus.COMPLETED)]
    [InlineData(SemanticOperation.PREPARE_LOCAL_EXECUTION, LiveStatus.BLOCKED)]
    [InlineData(SemanticOperation.EXECUTE_LOCAL, LiveStatus.BLOCKED)]
    public async Task APartialFieldTaskDoesNotAskForUnrequestedInputsOrPermitIncompleteExecution(SemanticOperation operation, LiveStatus expected)
    {
        var source = library.Create(new("Ficha parcial", "Explica <tema> para <publico>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Realiza la acción seleccionada.\ntema: SQLite", SourceId: source.Id, SourceRevision: source.Revision,
            Values: new() { ["tema"] = "SQLite" }, Command: new(operation));
        live.Create(input); var sessions = new AgentSessions(); var inference = new UnusedInference();
        var retriever = new SqlitePromptRetriever(store); var semantic = new FixedSemantic(SemanticFixture.Plan([operation], TargetReference.SELECTED, source.Id));
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic), executeGroundedPlan: true, stopAfterVerifiedResult: true);
        var browser = new FillingBrowser(State(source) with { Mode = "focus", VariableNames = ["tema", "publico"], Values = new() { ["tema"] = "", ["publico"] = "" } });
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.True(expected == live.Get(input.RunId).Status, JsonSerializer.Serialize(new { expected, view = live.View(input.RunId), browser.Tools }));
        Assert.True(live.View(input.RunId).Observation!.Values["tema"] == "SQLite", JsonSerializer.Serialize(new { view = live.View(input.RunId), browser.Tools }));
        Assert.Equal("", live.View(input.RunId).Observation!.Values["publico"]);
        Assert.Single(browser.Tools, tool => tool == "ui_fill_variable");
        Assert.DoesNotContain("ui_execute_local", browser.Tools);
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        if (expected == LiveStatus.BLOCKED) {
            Assert.Contains("publico", live.View(input.RunId).Question!);
            Assert.Contains(operation, sessions.Get(input.SessionId)!.PendingPlan!.Intentions);
            // The resumed task is not dependent on the entry point that created it.
            sessions.Put(sessions.Get(input.SessionId)! with { PendingCommand = null });
            var reply = new LiveStartInput(Guid.NewGuid(), input.SessionId, "publico: principiantes", SourceId: source.Id, SourceRevision: source.Revision);
            live.Create(reply);
            await engine.RunVisible(reply, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
            Assert.True(live.Get(reply.RunId).Status == LiveStatus.COMPLETED, JsonSerializer.Serialize(live.View(reply.RunId)));
            Assert.Equal("SQLite", live.View(reply.RunId).Observation!.Values["tema"]);
            Assert.Equal("principiantes", live.View(reply.RunId).Observation!.Values["publico"]);
            Assert.Equal(operation == SemanticOperation.EXECUTE_LOCAL ? 1 : 0, browser.Tools.Count(tool => tool == "ui_execute_local"));
            Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        } else Assert.Null(live.View(input.RunId).Question);
    }
    private sealed class FillingBrowser(UiVisibleState initial) : IVisibleBrowser, IVisibleBrowserFactory
    {
        private UiVisibleState state = initial; public List<string> Tools = [];
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(state);
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct) {
            Tools.Add(tool);
            if (tool == "ui_fill_variable") {
                var values = new Dictionary<string, string>(state.Values) { [args.GetProperty("name").GetString()!] = args.GetProperty("value").GetString()! };
                var ready = state.VariableNames.All(name => !string.IsNullOrWhiteSpace(values.GetValueOrDefault(name)));
                state = state with { Values = values, CompiledReady = ready, CompiledText = ready ? TemplateCompiler.Render(state.TemplateText!, values) : null };
            }
            if (tool == "ui_open_provider_picker") state = state with { ProviderOpen = true, ProviderId = "ollama" };
            if (tool == "ui_execute_local") state = state with { Response = "Respuesta local observada.", ResponseComplete = true };
            return Task.FromResult(state);
        }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [Fact]
    public async Task AReadyLiteralFieldActionPrecedesInferenceAndCompletesWithoutInventingOtherInputs()
    {
        var source = library.Create(new("Ficha con datos", "Describe <tema> para <publico>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Completa los datos.\ntema: SQLite", SourceId: source.Id, SourceRevision: source.Revision);
        live.Create(input); var sessions = new AgentSessions(); var inference = new UnexpectedExtraction();
        var authorship = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id);
        var prior = new DeterministicPromptEvaluator().Evaluate(new(Guid.NewGuid(), "Preparación anterior.", SessionId: input.SessionId),
            authorship.Preparation(), new(PreparationDecision.ADAPT_EXISTING, source.Id, source.Title, "", source.TemplateText, [], "", "Borrador de conversación anterior."),
            [new(source, 10, "Origen")], []) with { SessionId = input.SessionId, Semantic = authorship };
        sessions.Put(new(input.SessionId, source, prior, [], DateTime.UtcNow, LastProposal: authorship) { DraftRevision = 4 });
        var retriever = new SqlitePromptRetriever(store);
        var model = new FixedSemantic(SemanticFixture.Plan([SemanticOperation.FILL_VARIABLES], TargetReference.SELECTED, source.Id));
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(model, retriever, sessions, model), executeGroundedPlan: true, stopAfterVerifiedResult: true);
        var browser = new FillingBrowser(State(source) with { Mode = "focus", VariableNames = ["tema", "publico"], Values = new() { ["tema"] = "anterior", ["publico"] = "" } });
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status);
        Assert.Equal("SQLite", live.View(input.RunId).Observation!.Values["tema"]);
        Assert.Equal("", live.View(input.RunId).Observation!.Values["publico"]);
        Assert.Single(browser.Tools, tool => tool == "ui_fill_variable");
        Assert.Equal(0, inference.Extractions);
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        var conversation = sessions.Get(input.SessionId)!;
        Assert.Equal(prior.Id, conversation.WorkingDraftReference!.DraftId);
        Assert.Equal(4, conversation.WorkingDraftReference.DraftRevision);
        Assert.Same(authorship, conversation.Draft!.Semantic);
        Assert.Same(authorship, conversation.LastProposal);
    }
    private sealed class UnexpectedExtraction : IIntentInterpreter, IPromptGenerator, IInputValueExtractor
    {
        public int Extractions;
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new NotSupportedException();
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct) { Extractions++; throw new InvalidOperationException("No missing input is required by the partial-fill goal."); }
    }
    [Fact]
    public async Task AnUnboundFillCannotCompleteByReusingAnAlreadyFilledFormOrAModelSuccessClaim()
    {
        var source = library.Create(new("Ficha previa", "Explica <tema>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Completa los campos de la ficha.", SourceId: source.Id, SourceRevision: source.Revision);
        live.Create(input); var sessions = new AgentSessions(); var inference = new UnusedInference();
        var retriever = new SqlitePromptRetriever(store);
        var model = new FixedSemantic(SemanticFixture.Plan([SemanticOperation.FILL_VARIABLES], TargetReference.SELECTED, source.Id));
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(model, retriever, sessions, model), executeGroundedPlan: true, stopAfterVerifiedResult: true);
        var browser = new ObservedBrowser(State(source) with { Mode = "focus", VariableNames = ["tema"], Values = new() { ["tema"] = "anterior" }, CompiledReady = true, CompiledText = "Explica anterior." });
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new ClaimFinish(), browser, library);
        Assert.Equal(LiveStatus.BLOCKED, live.Get(input.RunId).Status);
        Assert.DoesNotContain("ui_fill_variable", browser.Tools);
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
    }
    private sealed class ClaimFinish : IUiDecisionModel
    {
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct) => Task.FromResult(new UiDecision("finish", UiToolCatalog.NoArguments));
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TaskMemoryContainsOnlyVerifiedEffectsAndNeverAdoptsAnUnrelatedPendingRequest(bool ignoreWrite, bool continuation)
    {
        var source = library.Create(new("Ficha con dos campos", "Explica <tema> para <publico>."));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Completa esta ficha.\ntema: SQLite\npublico: desarrolladores",
            SourceId: source.Id, SourceRevision: source.Revision,
            Values: new() { ["tema"] = "SQLite", ["publico"] = "desarrolladores" });
        live.Create(input); var sessions = new AgentSessions();
        var plan = SemanticFixture.Plan([SemanticOperation.FILL_VARIABLES], TargetReference.SELECTED, source.Id);
        const string original = "Completa la ficha con los datos que te proporcionaré.";
        sessions.Put(new(input.SessionId, source, null, [], DateTime.UtcNow,
            PendingRequest: continuation ? original : "UNRELATED_PENDING_TASK_DO_NOT_ADOPT", PendingPlan: continuation ? plan : null));
        var inference = new UnusedInference(); var retriever = new SqlitePromptRetriever(store);
        var semantic = new FixedSemantic(plan with { ContinuesPendingTask = continuation });
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic));
        var initial = State(source) with { Mode = "focus", Values = new() { ["tema"] = "", ["publico"] = "" }, VariableNames = ["tema", "publico"], CompiledReady = false, CompiledText = null };
        IVisibleBrowserFactory browser = ignoreWrite ? new ObservedBrowser(initial) : new FillingBrowser(initial);
        var model = new FillObservedFields();
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, model, browser, library);
        Assert.Equal(continuation ? original : input.Message, model.Contexts[0].Memory!.OriginalRequest);
        Assert.Equal(continuation ? new[] { input.Message } : [], model.Contexts[0].Memory!.Clarifications);
        Assert.Empty(model.Contexts[0].Memory!.Receipts);
        Assert.DoesNotContain("UNRELATED_PENDING_TASK", JsonSerializer.Serialize(model.Contexts.Select(context => context.Memory)));
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        if (ignoreWrite) {
            Assert.Single(model.Contexts);
            Assert.Equal(LiveStatus.FAILED, live.Get(input.RunId).Status);
            Assert.Equal("VALUES_NOT_APPLIED", live.Get(input.RunId).ErrorCode);
            Assert.DoesNotContain(live.Events(input.RunId, 0), item => item.Stage == "Observation" && item.ToolName == "ui_fill_variable");
        } else {
            Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status);
            Assert.Equal(3, model.Contexts.Count);
            var first = Assert.Single(model.Contexts[1].Memory!.Receipts);
            Assert.Equal("ui_fill_variable", first.ToolName);
            Assert.Equal("SQLite", first.WrittenValues["tema"]);
            Assert.Equal(source.Id, first.PromptId);
            Assert.Equal(source.Revision, first.Revision);
            Assert.Equal(2, model.Contexts[2].Memory!.Receipts.Length);
            Assert.Equal("desarrolladores", model.Contexts[2].Memory!.Receipts[1].WrittenValues["publico"]);
        }
    }
    private sealed class FillObservedFields : IUiDecisionModel
    {
        public List<UiDecisionContext> Contexts = [];
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct)
        {
            Contexts.Add(context);
            var pending = context.AuthorizedValues!.FirstOrDefault(value => context.State.Values.GetValueOrDefault(value.Key) != value.Value);
            return Task.FromResult(pending.Key is null ? new UiDecision("finish", UiToolCatalog.NoArguments)
                : new UiDecision("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = context.State.PromptId, name = pending.Key, value = pending.Value, evidence = pending.Value })));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedInputSuggestionsPreserveTheTaskAndAskWithoutWritingOrExecuting(bool executes)
    {
        var source = library.Create(new("Ficha protegida", "Explica <tema>."));
        const string message = "Aplica los datos y ejecuta el prompt con el modelo local ahora.";
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, SourceId: source.Id, SourceRevision: source.Revision);
        live.Create(input); var sessions = new AgentSessions(); var inference = new RejectedExtraction();
        var retriever = new SqlitePromptRetriever(store);
        var plan = SemanticFixture.Plan(executes ? [SemanticOperation.FILL_VARIABLES, SemanticOperation.COMPILE_PROMPT, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL]
            : [SemanticOperation.FILL_VARIABLES], TargetReference.SELECTED, source.Id, outcome: executes ? SemanticOutcome.LOCAL_RESPONSE : SemanticOutcome.READ)
            with { ProviderId = executes ? "ollama" : null, ExecutionEvidence = executes ? message : "" };
        var model = new FixedSemantic(plan);
        var engine = new AgentOrchestrator(inference, retriever, inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(model, retriever, sessions, model), executeGroundedPlan: true, stopAfterVerifiedResult: true);
        var browser = new ObservedBrowser(State(source) with { Mode = "focus", VariableNames = ["tema"], Values = new() { ["tema"] = "" } });
        await engine.RunVisible(input, "http://127.0.0.1:5226", live, new InvalidDecision(), browser, library);
        Assert.True(live.Get(input.RunId).Status == LiveStatus.BLOCKED, JsonSerializer.Serialize(live.View(input.RunId)));
        Assert.Equal("NEEDS_CLARIFICATION", live.Get(input.RunId).ErrorCode);
        Assert.Equal(message, sessions.Get(input.SessionId)!.PendingRequest);
        Assert.Equal(executes, sessions.Get(input.SessionId)!.PendingPlan!.Executes);
        Assert.DoesNotContain("ui_fill_variable", browser.Tools); Assert.DoesNotContain("ui_execute_local", browser.Tools);
        Assert.Equal("", live.View(input.RunId).Observation!.Values["tema"]);
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
    }
    private sealed class RejectedExtraction : IIntentInterpreter, IPromptGenerator, IInputValueExtractor
    {
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new NotSupportedException();
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
            => Task.FromResult(new InputValueExtraction([new("tema", "INVENTED_VALUE", "INVENTED_VALUE")], ""));
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
