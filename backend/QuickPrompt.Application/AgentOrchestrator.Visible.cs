using System.Diagnostics;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed partial class AgentOrchestrator
{
    // Same domain services; this variant delegates navigation to a bounded UI executor, one model decision at a time.
    public async Task RunVisible(LiveStartInput input, string origin, LiveRuns live, IUiDecisionModel model, IVisibleBrowserFactory factory, PromptLibrary library, int maxSteps = 12)
    {
        var visibleAdvice = adviceResponder ?? model as ISemanticAdviceResponder;
        using var performance = NovaTelemetry.Begin(input.RunId);
        var control = live.Active(input.RunId); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(control.Cancellation.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(8)); var ct = timeout.Token; var executed = new List<string>(); var searched = false; var ambiguous = false;
        lock (control.Gate) control.DeletionDeadlineTick = Environment.TickCount64 + (long)TimeSpan.FromMinutes(8).TotalMilliseconds;
        var localAllowed = false; var updateAllowed = false; SemanticInterpretation? semantic = null;
        var inspections = new Dictionary<string, string>(); var inputShapes = new HashSet<string>(); string? lastResult = null;
        var receipts = new Queue<UiObservedEffect>(); var receiptCount = 0;
        UiClipboardReceipt? completedClipboard = null;
        var priorLoaded = false; var conversationCheckpointed = false;
        DraftRef? workingCheckpoint = null;
        var conversationBeforeRun = sessions?.Get(input.SessionId);
        try
        {
            live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Consultando Qwen para interpretar la petición completa y el contexto autorizado.", "SemanticInterpretation");
            // Each planning phase owns its bounded stage deadline; the run owns the
            // global eight-minute deadline. Source refinement follows actual retrieval.
            var resolution = await planning.Interpret(input.Message, input.SessionId, input.SourceId, input.SourceRevision, input.Model, null, ct, input.Command);
            resolution = await planning.Resolve(resolution, input.Message, input.SessionId, input.Model, null, ct);
            semantic = resolution.Interpretation; control.Semantic = semantic;
            if (input.Command?.Operation == SemanticOperation.SEARCH_PROMPT)
                control.SourceChoices = resolution.Candidates.Where(candidate => !candidate.Prompt.IsArchived).Take(6)
                    .Select(candidate => new UiPrompt(candidate.Prompt.Id, candidate.Prompt.Title, candidate.Prompt.Revision)).ToArray();
            var copyTool = semantic.RequiresTemplatePreview ? "ui_copy_template" : "ui_copy_compiled_prompt";
            if (semantic.RequiresTemplatePreview) live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.USER_SELECTED,
                "Elegiste preparar la plantilla reutilizable con sus marcadores.", "CopyRepresentation");
            if (semantic.Authoring is not null && semantic.Goal is null && semantic.DesiredOutcome != SemanticOutcome.CLARIFICATION)
                throw new PreparationToolException("MISSING_TASK_GOAL", "semantic_plan", "La tarea no tiene un contrato de finalización vinculado.");
            if (semantic.ValidationNotes?.Contains("PROVIDED_INPUT_BINDINGS_RESOLVED") == true)
                live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.RULE_SELECTED, "Se usarán los valores que proporcionaste para completar el prompt.", "InputPrerequisite");
            input = input with { AuthorizedPendingRequest = semantic.ContinuesPendingTask ? resolution.Context.PendingRequest : null, InputScope = semantic.InputScope };
            control.AuthorizedTaskRequest = input.TaskRequest; control.VisibleTaskRequest = input.DisplayRequest;
            if (semantic.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT && resolution.ActiveDraft is { } active
                && active.SourceId == resolution.Source?.Id && active.TemplateText != resolution.Source?.TemplateText) {
                control.WorkingDraft = active;
                workingCheckpoint = resolution.Context.DraftReference;
                if (semantic.Goal is { } workingGoal) semantic = semantic with { Goal = workingGoal.BindWorking(active, resolution.Context.DraftReference?.DraftRevision ?? 1) };
                control.Semantic = semantic;
            }
            live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.MODEL_SELECTED, "Interpretación contextual y plan de operaciones recibidos de Qwen.", "SemanticPlan");
            if (semantic.Has(SemanticOperation.CANCEL)) {
                sessions?.Forget(input.SessionId);
                live.Emit(input.RunId, LiveStatus.CANCELLED, LiveActor.SYSTEM_VALIDATION, "Solicitud cancelada. Los prompts guardados y sus versiones se conservan.", "Cancellation"); return;
            }
            if (semantic.Has(SemanticOperation.PAUSE) || semantic.Has(SemanticOperation.RESUME)) {
                if (conversationBeforeRun is not null) sessions?.Put(conversationBeforeRun with { UpdatedAt = DateTime.UtcNow });
                else sessions?.Forget(input.SessionId);
                control.Question = "No hay una ejecución anterior activa. Usa sus controles de pausa o continuación cuando esté en marcha.";
                live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION, control.Question, error: "NO_ACTIVE_BROWSER_RUN"); return;
            }
            if (semantic.DesiredOutcome == SemanticOutcome.ADVICE && (semantic.Goal is null || semantic.Goal.CompletionConditions.All(condition => condition == CompletionCondition.EXPLANATION_READY))) {
                if (sessions?.Get(input.SessionId) is { } context) sessions.Put(context with { PendingPlan = null, PendingRequest = null, PendingAction = null });
                if (semantic.Goal is { } adviceGoal && visibleAdvice is { } advisor) {
                    await live.Boundary(input.RunId, ct); control.Question = await advisor.Explain(input.Message, resolution.Source, resolution.ActiveDraft, input.Model, ct);
                    await live.Boundary(input.RunId, ct);
                    if (!GoalCompletionValidator.ValidateAdvice(adviceGoal, semantic, control.Question, resolution.Source, resolution.Source is { } adviceSource ? library.Get(adviceSource.Id) : null).Complete)
                        throw new PreparationToolException("GOAL_INCOMPLETE", "explain_advice", "No se pudo verificar el origen de la explicación.");
                } else control.Question = semantic.Explanation;
                live.Emit(input.RunId, LiveStatus.COMPLETED, semantic.Goal is null ? LiveActor.MODEL_SELECTED : LiveActor.SYSTEM_VALIDATION,
                    semantic.Goal is null ? semantic.Explanation : "Explicación preparada y origen comprobado.", "Explanation"); return;
            }
            if (semantic.DesiredOutcome == SemanticOutcome.CLARIFICATION) {
                RememberPending();
                control.Question = string.Join(" ", semantic.MissingInformation.DefaultIfEmpty(semantic.Explanation));
                live.Emit(input.RunId, LiveStatus.BLOCKED, semantic.ValidationNotes is { Length: > 0 } ? LiveActor.SYSTEM_VALIDATION : LiveActor.MODEL_SELECTED, "Necesito una aclaración antes de actuar.", error: "NEEDS_CLARIFICATION"); return;
            }
            if (semantic.Creates && input.Mode != "SAME_TAB") throw new ValidationException("La creación visual requiere la pestaña emisora.");
            if (resolution.Source is { } resolved) input = input with { SourceId = resolved.Id, SourceRevision = resolved.Revision,
                Values = (input.SourceId == resolved.Id ? input.Values : null) ?? (sessions?.Get(input.SessionId)?.Source?.Id == resolved.Id ? sessions.Get(input.SessionId)!.Values : null) };
            if (semantic.Goal is not null && semantic.Creates)
                input = input with { SourceId = null, SourceRevision = null,
                    Values = semantic.TargetReference != TargetReference.NONE && semantic.Has(SemanticOperation.FILL_VARIABLES) ? input.Values : null };
            if (semantic.Goal is not null) lock (control.Gate) control.Input = input;
            localAllowed = semantic.Executes; updateAllowed = semantic.Adapts;
            if (input.SourceId is { } requested && library.Get(requested).Revision != input.SourceRevision) throw new ConflictException("El origen seleccionado cambió.");
            await live.Boundary(input.RunId, ct);
            await using var browser = factory.Create(code => {
                lock (control.Gate) {
                    if (LiveRuns.Terminal(live.Get(input.RunId).Status)) return;
                    try { live.Emit(input.RunId, LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, "Se cerró el navegador de NOVA. No se iniciarán nuevas acciones.", "Browser", error: code); }
                    finally { control.Cancellation.Cancel(); control.Resumed.TrySetResult(); control.ApprovalDecision.TrySetResult(false); }
                }
            }); control.Browser = browser;
            live.Emit(input.RunId, LiveStatus.TOOL_RUNNING, LiveActor.RULE_SELECTED, input.Mode == "SAME_TAB" ? "Conectando los controles reales de esta pestaña." : "Abriendo Edge de QA con un contexto aislado.", "Browser");
            var state = await browser.Start(input.RunId, origin, localAllowed, ct, input.DisplayRequest);
            if (semantic.Deletes) {
                live.PublishRuntimeTools(input.RunId, UiToolCatalog.All.Where(tool => tool.Name is "ui_open_prompt" or "ui_review_deletion" or "ui_enable_deletion_confirmation" or "ui_verify_deletion"));
                if (input.Mode != "SAME_TAB" || resolution.Source is not { } deleting)
                    throw new ValidationException("La eliminación requiere un prompt guardado y la revisión en esta pestaña.");
                if (control.WorkingDraft is not null || state.Mode == "create" && (state.PromptId != deleting.Id || state.TemplateText != deleting.TemplateText || state.Title != deleting.Title))
                    throw new ValidationException("Conserva o cierra el borrador antes de revisar una eliminación.");
                var tools = new List<string>();
                if (semantic.Has(SemanticOperation.OPEN_PROMPT) && (state.Mode != "focus" || state.PromptId != deleting.Id)) {
                    if (state.Mode != "discover") state = await browser.Execute("ui_navigate_library", JsonSerializer.SerializeToElement(new { }), ct);
                    if (!state.Candidates.Any(candidate => candidate.Id == deleting.Id))
                        await browser.Execute("ui_search_prompt", JsonSerializer.SerializeToElement(new { query = deleting.Title }), ct);
                    state = await browser.Execute("ui_open_prompt", JsonSerializer.SerializeToElement(new { promptId = deleting.Id }), ct);
                    tools.Add("ui_open_prompt");
                } else if (semantic.Has(SemanticOperation.OPEN_PROMPT)) tools.Add("ui_open_prompt");
                live.ProposeDeletion(input.RunId, deleting);
                var arguments = JsonSerializer.SerializeToElement(new { promptId = deleting.Id, revision = deleting.Revision });
                state = await browser.Execute("ui_review_deletion", arguments, ct);
                live.DeletionPresented(input.RunId, state);
                state = await browser.Execute("ui_enable_deletion_confirmation", arguments, ct);
                control.Observation = state;
                lock (control.Gate) {
                    if (!live.Get(input.RunId).EffectCommitted)
                        live.Emit(input.RunId, LiveStatus.AWAITING_APPROVAL, LiveActor.SYSTEM_VALIDATION, "Esperando tu decisión sobre la eliminación.", "DeletionReview");
                }
                if (!await control.DeletionDecision.Task.WaitAsync(ct)) throw new OperationCanceledException(ct);
                await live.Boundary(input.RunId, ct);
                state = await browser.Execute("ui_verify_deletion", arguments, ct); tools.Add("ui_verify_deletion");
                PromptDocument? remaining;
                try { remaining = library.Get(deleting.Id); } catch (MissingPromptException) { remaining = null; }
                if (!live.Get(input.RunId).EffectCommitted || remaining is not null || state.DeletionPromptId is not null || state.PromptId == deleting.Id
                    || state.Candidates.Any(candidate => candidate.Id == deleting.Id)
                    || semantic.Goal is {} deletionGoal && !GoalCompletionValidator.Validate(deletionGoal, semantic, state, deleting, remaining, tools, new Dictionary<string,string>(), false, true, true).Complete)
                    throw new PreparationToolException("GOAL_INCOMPLETE", "ui_verify_deletion", "No se pudo verificar la eliminación confirmada.");
                control.Observation = state; sessions?.Forget(input.SessionId); conversationCheckpointed = true;
                live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Prompt eliminado tras tu confirmación; ausencia comprobada en la biblioteca y la pantalla.", "DeletionVerified");
                return;
            }
            if (control.WorkingDraft is { } working) {
                if (state.Mode == "create" && state.PromptId == working.SourceId && state.TemplateText is { Length: > 0 }) {
                    if (state.TemplateText != working.TemplateText || state.Title != working.Title)
                        throw new PreparationToolException("MANUAL_INTERVENTION", "ui_load_active_draft", "El borrador visible cambió desde el último checkpoint. Se conserva tu texto para revisarlo.");
                    working = working with { Values = state.Values.Where(value => working.Variables.Any(variable => variable.Name == value.Key) && !string.IsNullOrWhiteSpace(value.Value))
                        .Select(value => new VariableValue(value.Key, value.Value, "USER_INPUT", "VALIDATED")).ToArray() };
                    control.WorkingDraft = working;
                }
                state = await browser.Execute("set_working_context", JsonSerializer.SerializeToElement(new { promptId = working.SourceId, revision = working.SourceRevision,
                    title = working.Title, templateText = working.TemplateText, values = working.Values.ToDictionary(v => v.Name, v => v.Value) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), ct);
            }
            control.Observation = state;
            void RecordRejectedArguments(UiDecision invalid)
            {
                lock (control.Gate) control.Decisions.Add(invalid);
                var name = UiToolCatalog.IsRegistered(invalid.ToolName) ? invalid.ToolName : "UNRECOGNIZED";
                live.Emit(input.RunId, LiveStatus.TOOL_PROPOSED, LiveActor.MODEL_SELECTED, "Qwen propuso: " + UiToolCatalog.Label(name) + ".", "DecisionRejected", name, state.PromptId);
                live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Argumentos incompatibles con el esquema. La acción no se ejecutó.", "Validation", name, state.PromptId, error: "INVALID_ARGUMENTS");
            }
            void ModelRetry(string reason) => live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION,
                reason == "INVALID_ARGUMENTS" ? "Los argumentos fueron rechazados; solicitando una nueva elección estructurada con un único reintento." : reason == "MULTIPLE_ACTIONS" ? "Se rechazó la propuesta de acciones simultáneas; solicitando un único paso estructurado." : "Qwen no incluyó una acción; solicitando una elección estructurada con un único reintento.", "ModelRetry");
            live.Emit(input.RunId, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, input.Mode == "SAME_TAB" ? "Esta pestaña confirmó su estado visible." : "Navegador de QA conectado a la aplicación real.", "Browser", update: r => r with { BrowserOpen = true });
            for (var step = 0; step < maxSteps; step++)
            {
                await live.Boundary(input.RunId, ct);
                live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.RULE_SELECTED, "Comprobando que la sesión visible sigue en el último estado autorizado.", "Session", "ui_read_visible_state");
                state = await browser.Execute("ui_read_visible_state", UiToolCatalog.NoArguments, ct);
                if (input.SourceId is { } expectedTarget && state.PromptId is { } actualTarget && actualTarget != expectedTarget && executed.Contains("ui_open_prompt"))
                    throw new PreparationToolException("UI_TARGET_CHANGED", "ui_read_visible_state", "El objetivo visible cambió; revisa la solicitud antes de continuar.");
                if (state.PromptId is { } selected && library.Get(selected).Revision != state.Revision) throw new ConflictException("El prompt cambió durante la ejecución.");
                var inputTargetVisible = UiToolCatalog.InputTargetVisible(input, state);
                if (inputTargetVisible && semantic.Has(SemanticOperation.FILL_VARIABLES) && state.TemplateText is { Length: > 0 } material && fieldExtractor is { } extractor) {
                    var known = UiToolCatalog.GroundedValues(input, state, semantic);
                    var missing = state.VariableNames.Where(name => !known.ContainsKey(name) && string.IsNullOrWhiteSpace(state.Values.GetValueOrDefault(name))).ToArray();
                    var knownChangesPending = known.Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value);
                    var inputResultReady = semantic.RequiresCompiledOutput ? state.CompiledReady
                        : known.Count > 0 && known.All(value => state.Values.GetValueOrDefault(value.Key) == value.Value);
                    // Ready domain actions precede optional inference. Apply known
                    // bindings first, then re-observe the result. A completed partial
                    // fill has no reason to infer values for unrelated empty fields.
                    if (missing.Length > 0 && !knownChangesPending && !inputResultReady
                        && input.Command is null && semantic.ValidationNotes?.Contains("EXPLICIT_UI_COMMAND") != true
                        && inputShapes.Add(LiveRuns.Hash(new { state.PromptId, material }))) {
                        live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Qwen está vinculando los datos literales de tu petición con los campos reales.", "InputExtraction");
                        var request = new PrepareInput(input.RunId, input.Message, Model: input.Model) {
                            AuthorizedPendingRequest = input.AuthorizedPendingRequest, ContextValues = known, Interpretation = semantic, InputScope = input.InputScope };
                        var extraction = await extractor.ExtractInputs(request, material, missing, ct);
                        if (extraction.Values is null || extraction.Values.Length > missing.Length || extraction.Values.Select(value => value.Name).Distinct().Count() != extraction.Values.Length
                            || extraction.Values.Any(value => !missing.Contains(value.Name) || string.IsNullOrWhiteSpace(value.Value) || value.Value.Length > 8000 || string.IsNullOrWhiteSpace(value.Evidence)
                                || !value.Evidence.Contains(value.Value, StringComparison.Ordinal) || !(input.Message.Contains(value.Evidence, StringComparison.Ordinal) || input.AuthorizedPendingRequest?.Contains(value.Evidence, StringComparison.Ordinal) == true))
                            || extraction.Question is null || extraction.Question.Length > 500)
                        {
                            // A rejected suggestion is not task completion or input
                            // authority. Keep the original task and ask about the real
                            // pending fields instead of exposing an internal contract error.
                            RememberPending(); control.Observation = state;
                            control.Question = semantic.RequiresCompiledOutput
                                ? "¿Puedes aportar " + string.Join(", ", missing) + "?"
                                : "¿Qué valor quieres aplicar? Indica el nombre del campo y su valor.";
                            live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION,
                                "Necesito precisar los datos pendientes antes de aplicarlos.", "InputExtraction", error: "NEEDS_CLARIFICATION");
                            return;
                        }
                        if (!string.IsNullOrWhiteSpace(extraction.Question)) {
                            RememberPending(); control.Question = extraction.Question; control.Observation = state;
                            live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.MODEL_SELECTED, "Necesito precisar un dato antes de completar los campos.", "InputExtraction", error: "NEEDS_CLARIFICATION");
                            return;
                        }
                        input = input with { Values = known.Concat(extraction.Values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value))).ToDictionary(value => value.Key, value => value.Value) };
                    }
                }
                var grounded = inputTargetVisible ? UiToolCatalog.GroundedValues(input, state, semantic) : [];
                if (semantic.Goal is not null && semantic.Has(SemanticOperation.EXPLAIN) && visibleAdvice is { } visibleAdvisor && !executed.Contains("explain_advice")
                    && (!semantic.Adapts || executed.Contains("ui_prepare_update")) && (!semantic.Creates || executed.Contains("ui_prepare_creation"))
                    && state.PromptId == resolution.Source?.Id && state.Mode != "discover") {
                    var visibleDraft = control.GeneratedDraft ?? control.WorkingDraft ?? resolution.ActiveDraft;
                    if (semantic.Adapts && resolution.Source is { } explanationSource && state.TemplateText is not null)
                        visibleDraft = ObservedSourceDraft(state, explanationSource);
                    control.Question = await visibleAdvisor.Explain(input.Message, resolution.Source, visibleDraft, input.Model, ct);
                    await live.Boundary(input.RunId, ct);
                    executed.Add("explain_advice");
                }
                if (semantic.Goal is { } observedGoal) {
                    semantic = semantic with { Goal = observedGoal with { RequiredInformation = state.VariableNames.Where(name => string.IsNullOrWhiteSpace(state.Values.GetValueOrDefault(name))).ToArray() } };
                    control.Semantic = semantic;
                }
                var stateHash = LiveRuns.Hash(new { state.Mode, state.PromptId, state.Revision, state.TemplateText, state.Values, state.ResponseComplete });
                var executionPending = localAllowed && !executed.Contains("ui_execute_local");
                var capabilities = executionPending ? state with { ResponseComplete = false } : state;
                var allowedTools = UiToolCatalog.Available(capabilities, searched, ambiguous, localAllowed, updateAllowed, false, input.SourceId, executed.Contains("ui_prepare_update")).ToList();
                if (input.Mode == "SAME_TAB" && input.SourceId is { } desired && state.PromptId is { } current && desired != current) allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ui_navigate_library" or "ask_clarification").ToList();
                if (semantic.Creates) {
                    allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ask_clarification" or "ui_read_visible_state"
                        || control.GeneratedDraft is not null && (tool.Name is "finish" or "ui_verify_prompt_state"
                            || tool.Name == "ui_fill_variable" && state.VariableNames.Length > 0
                            || state.CompiledReady && (tool.Name is "ui_show_compiled_prompt" or "ui_copy_compiled_prompt" or "ui_open_provider_picker" || tool.Name == "ui_execute_local" && executionPending))
                        || state.Mode != "create" && tool.Name == "ui_open_create_prompt"
                        || state.Mode == "create" && control.GeneratedDraft is null && tool.Name == "ui_prepare_creation").ToList();
                }
                if (input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.SEARCH_PROMPT) && !searched && state.Mode == "discover") allowedTools.RemoveAll(tool => tool.Name == "ui_open_prompt");
                if (control.WorkingDraft is not null && !priorLoaded && (control.WorkingDraft.SourceId is null || state.PromptId == input.SourceId)) allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ui_load_active_draft" or "ui_read_visible_state" or "ask_clarification").ToList();
                if (priorLoaded && updateAllowed && !executed.Contains("ui_prepare_update")) allowedTools.Add(UiToolCatalog.All.Single(tool => tool.Name == "ui_prepare_update"));
                var providerRequested = semantic.Has(SemanticOperation.PREPARE_PROVIDER) || semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || localAllowed;
                var providerPending = !state.ProviderOpen || input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.PREPARE_PROVIDER)
                    && semantic.ProviderId is { } wantedProvider && wantedProvider != "ollama" && state.ProviderId != wantedProvider;
                var goalCompletion = semantic.Goal is { } currentGoal ? GoalCompletionValidator.Validate(currentGoal, semantic, state,
                    resolution.Source, resolution.Source is { } goalSource ? library.Get(goalSource.Id) : null, executed, grounded,
                    executed.Contains("ui_prepare_update") || executed.Contains("ui_prepare_creation"), live.Get(input.RunId).EffectCommitted) : null;
                if (goalCompletion?.Complete == true && semantic.Goal!.CompletionConditions.Contains(CompletionCondition.SEARCH_RESULTS_VISIBLE))
                    allowedTools.Add(UiToolCatalog.All.Single(tool => tool.Name == "finish"));
                var available = allowedTools
                    // Every iteration just read the live controls, and every effect is
                    // independently verified below. Asking the model to request those
                    // same observations again adds inference without new evidence.
                    .Where(tool => tool.Name is not ("ui_read_visible_state" or "ui_verify_prompt_state"))
                    .Where(tool => SemanticPlanValidator.ToolAllowed(semantic, tool.Name))
                    .Where(tool => tool.Name is not ("finish" or "ui_request_save_version") || goalCompletion?.Complete != false)
                    .Where(tool => (tool.Name != "ui_fill_variable" || grounded.Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value))
                        && (!(tool.Name is "ui_execute_local" or "ui_open_provider_picker" or "ui_show_compiled_prompt" or "ui_copy_compiled_prompt") || grounded.All(value => state.Values.GetValueOrDefault(value.Key) == value.Value))
                        && (tool.Name != "ui_open_provider_picker" || providerRequested && providerPending)
                        && (tool.Name != "ui_execute_local" || executionPending)
                        && (tool.Name != copyTool || !executed.Contains(copyTool))
                        && (!(tool.Name is "finish" or "ui_request_save_version") || !semantic.Has(SemanticOperation.COPY_TO_CLIPBOARD) || executed.Contains(copyTool) && GoalCompletionValidator.ClipboardMatches(state, semantic.CopyContent))
                        && (!(tool.Name is "finish" or "ui_request_save_version") || (!updateAllowed || state.DraftChanged && executed.Contains("ui_prepare_update")) && (!localAllowed || state.ResponseComplete && executed.Contains("ui_execute_local")) && (!semantic.RequiresProvidedValues || grounded.Count > 0) && grounded.All(value => state.Values.GetValueOrDefault(value.Key) == value.Value))
                        && (tool.Name != "finish" || (!semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || semantic.Executes || state.ProviderOpen && state.ProviderId == "ollama") && (!semantic.RequiresCompiledOutput || state.CompiledReady)
                            && (!semantic.Has(SemanticOperation.PREPARE_PROVIDER) || !providerPending))
                        && inspections.GetValueOrDefault(tool.Name) != stateHash).ToArray();
                // Once the observed postconditions permit completion, optional questions and
                // repeated preparation cannot extend the task or reopen an already ready picker.
                var completionReady = !ambiguous && available.Any(tool => tool.Name == "finish");
                if (completionReady) available = available.Where(tool => tool.Name is not ("ask_clarification" or "ui_read_visible_state" or "ui_verify_prompt_state" or "ui_show_compiled_prompt")).ToArray();
                // The semantic plan and fresh observation already establish completion.
                // Do not ask a model to confirm a deterministic postcondition. The same
                // contract, identity, revision and effect checks below still apply.
                var deterministicFinish = stopAfterVerifiedResult && completionReady
                    && available.All(tool => tool.Name is "finish" or "ui_request_save_version");
                var plannedAction = executeGroundedPlan ? UiToolCatalog.BoundNavigation(available, state, resolution.Source, searched)
                    ?? UiToolCatalog.BoundInput(available, state, grounded)
                    ?? UiToolCatalog.BoundGoal(available, state, semantic, executed) : null;
                var deterministicDecision = deterministicFinish || plannedAction is not null;
                control.Observation = state;
                live.PublishRuntimeTools(input.RunId, available);
                if (!deterministicDecision) live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Consultando Qwen para elegir la siguiente acción a partir del estado observado.", "Model");
                var timer = Stopwatch.StartNew(); UiDecision decision;
                if (deterministicFinish) decision = new("finish", UiToolCatalog.NoArguments, "verified_postconditions");
                else if (plannedAction is not null) decision = plannedAction;
                else using (var stage = CancellationTokenSource.CreateLinkedTokenSource(ct)) { stage.CancelAfter(TimeSpan.FromSeconds(options.StageTimeoutSeconds)); decision = await model.Decide(new(input.TaskRequest, state, executed.ToArray(), available, input.SourceId, ambiguous ? "Several compatible candidates: ask a concrete clarification; do not choose arbitrarily." : priorLoaded && updateAllowed && !executed.Contains("ui_prepare_update") ? "The previous authorized draft is loaded, but the NEW requested content change has not been applied. Prepare that change with ui_prepare_update before finishing." : state.DraftChanged && executed.Contains("ui_prepare_update") && !localAllowed ? "The requested draft change is observed. Empty placeholders do not block review: finish or request human review without filling them." : null, grounded, lastResult, input.SourceId is { } source ? library.Get(source).Title : null, ModelRetry, localAllowed, updateAllowed, RecordRejectedArguments, semantic,
                    new(input.AuthorizedPendingRequest ?? input.Message,
                        input.AuthorizedPendingRequest is not null && input.AuthorizedPendingRequest != input.Message ? [input.Message] : [],
                        receipts.ToArray(), goalCompletion, receiptCount - receipts.Count, live.Get(input.RunId).Approval, live.Get(input.RunId).EffectCommitted)), stage.Token); }
                lock (control.Gate) control.Decisions.Add(decision);
                var safeTool = UiToolCatalog.IsRegistered(decision.ToolName) ? decision.ToolName : "UNRECOGNIZED";
                live.Emit(input.RunId, LiveStatus.TOOL_PROPOSED, deterministicDecision ? LiveActor.RULE_SELECTED : LiveActor.MODEL_SELECTED,
                    (deterministicDecision ? "Plan verificado: " : "Qwen eligió: ") + UiToolCatalog.Label(safeTool) + ".", "Decision", safeTool, state.PromptId, timer.Elapsed.TotalMilliseconds);
                NovaTelemetry.DebugPoint("ui.tool.context", false, () => new() {
                    ["tool"] = safeTool, ["mode"] = state.Mode, ["visibleId"] = state.PromptId,
                    ["requestedSourceId"] = input.SourceId, ["hasWorkingDraft"] = control.WorkingDraft is not null,
                    ["workingDraftId"] = control.WorkingDraft?.Id, ["workingSourceId"] = control.WorkingDraft?.SourceId,
                    ["hasGoal"] = semantic.Goal is not null, ["hasWorkingTarget"] = semantic.Goal?.WorkingTarget is not null
                });
                var workingReference = control.WorkingDraft is not null && workingCheckpoint is { } reference
                    ? sessions?.RequireWorkingDraft(reference) : null;
                UiToolCatalog.Validate(decision, available, state, input, semantic, workingReference);
                await live.Boundary(input.RunId, ct);
                // The very first action may use a prompt already open in React. Bind the
                // validated observation before dispatch, so execution guards see its identity.
                control.Observation = state;
                live.Emit(input.RunId, LiveStatus.TOOL_APPROVED, LiveActor.SYSTEM_VALIDATION, "Contrato, permisos y objetivo comprobados.", "Validation", decision.ToolName, state.PromptId,
                    update: r => r with { ErrorCode = null, TargetId = state.PromptId, Revision = state.Revision });
                if (decision.ToolName == "ask_clarification")
                {
                    RememberPending();
                    control.Question = decision.Arguments.GetProperty("question").GetString();
                    live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.MODEL_SELECTED, "Necesito una aclaración antes de continuar.", error: "NEEDS_CLARIFICATION"); return;
                }
                if (decision.ToolName is "finish" or "ui_request_save_version")
                {
                    if (updateAllowed && (!state.DraftChanged || !executed.Contains("ui_prepare_update")) || localAllowed && !state.ResponseComplete || state.PromptId is null && !semantic.Creates && control.WorkingDraft is not { SourceId: null }
                        && semantic.Goal?.CompletionConditions.Contains(CompletionCondition.SEARCH_RESULTS_VISIBLE) != true)
                        throw new PreparationToolException("INCOMPLETE_RESULT", decision.ToolName, "La interfaz todavía no confirma todos los efectos solicitados.");
                    if (semantic.Has(SemanticOperation.COPY_TO_CLIPBOARD) && (!executed.Contains(copyTool) || !GoalCompletionValidator.ClipboardMatches(state, semantic.CopyContent)))
                        throw new PreparationToolException("CLIPBOARD_NOT_VERIFIED", decision.ToolName, "No se confirmó la copia del texto actual.");
                    if (grounded.Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value)) throw new PreparationToolException("VALUES_NOT_APPLIED", decision.ToolName, "La interfaz todavía no confirma los valores proporcionados.");
                    if (semantic.RequiresProvidedValues && grounded.Count == 0)
                        throw new PreparationToolException("VALUES_NOT_APPLIED", decision.ToolName, "Faltan datos autorizados para completar los campos solicitados.");
                    if (semantic.RequiresCompiledOutput && !state.CompiledReady
                        || semantic.Has(SemanticOperation.PREPARE_PROVIDER) && !state.ProviderOpen
                        || semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) && !semantic.Executes && (!state.ProviderOpen || state.ProviderId != "ollama")) throw new PreparationToolException("INCOMPLETE_PREPARATION", decision.ToolName, "Faltan resultados visibles requeridos por el plan.");
                    if (semantic.Creates && (control.GeneratedDraft is not { } generated || state.Title != generated.Title || state.TemplateText != generated.TemplateText)) throw new ValidationException("El borrador visible cambió antes de su verificación.");
                    VerifyVisible(state, library, live.Get(input.RunId).EffectCommitted, input.Message, semantic.Requirements,semantic.Goal);
                    if (semantic.Goal is { } goal) {
                        var completion = GoalCompletionValidator.Validate(goal, semantic, state, resolution.Source,
                            resolution.Source is { } sourceBeforeCompletion ? library.Get(sourceBeforeCompletion.Id) : null,
                            executed, grounded, true, live.Get(input.RunId).EffectCommitted);
                        if (!completion.Complete) throw new PreparationToolException("GOAL_INCOMPLETE", decision.ToolName, "No se cumplieron las condiciones: " + string.Join(", ", completion.Missing));
                    }
                    if (updateAllowed && state.TemplateText is { } modified && state.PromptId is { } sourceId
                        && semantic.Constraints.Shortening
                        && modified.Length >= library.Get(sourceId).TemplateText.Length)
                        throw new PreparationToolException("SHORTER_DRAFT_NOT_VERIFIED", decision.ToolName, "La propuesta no es más breve que el original.");
                    live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Resultado visible y revisión persistida comprobados.", "Verification", "ui_verify_prompt_state", state.PromptId);
                    if (input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.PREPARE_PROVIDER) && semantic.ProviderId is { } destination && destination != "ollama" && state.ProviderId != destination) throw new ValidationException("El destino visible no coincide con el solicitado.");
                    var reviewRequested = semantic.Goal is { } saveGoal ? saveGoal.ApprovalRequirements.Length > 0
                        : semantic.Has(SemanticOperation.REQUEST_SAVE_VERSION) || semantic.Has(SemanticOperation.REQUEST_SAVE_COPY);
                    if (state.DraftChanged && reviewRequested)
                    {
                        if (input.Mode == "SAME_TAB" && state.ProviderOpen) {
                            var previousResponse = state.Response; var previousComplete = state.ResponseComplete;
                            state = await browser.Execute("prepare_review", UiToolCatalog.NoArguments, ct);
                            state = state with { Response = previousResponse, ResponseComplete = previousComplete }; control.Observation = state;
                        }
                        RecordConversation(state);
                        live.ProposeSave(input.RunId, state, workingReference: control.GeneratedDraft is null && workingCheckpoint is { } reviewCheckpoint
                            ? sessions?.RequireWorkingDraft(reviewCheckpoint) : null);
                        NovaTelemetry.ResultReady(input.RunId);
                        var approved = await control.ApprovalDecision.Task.WaitAsync(ct);
                        if (!approved) throw new OperationCanceledException(ct);
                        await live.Boundary(input.RunId, ct);
                        // This internal command is never exposed to Qwen. It clicks the actual approval-bound React control.
                        var priorResponse = state.Response; var responseComplete = state.ResponseComplete;
                        state = await browser.Execute("commit_approved", JsonSerializer.SerializeToElement(new {
                            expectedRevision = (control.Proposal!.Revision ?? 0) + 1
                        }), ct);
                        state = state with { Response = priorResponse, ResponseComplete = responseComplete };
                        var saved = library.Get(state.PromptId!.Value);
                        if (!live.Get(input.RunId).EffectCommitted || saved.Revision != (control.Proposal!.Revision ?? 0) + 1 || state.Revision != saved.Revision || saved.TemplateText != control.Proposal.Draft.TemplateText)
                            throw new ConflictException("No se pudo verificar la versión guardada.");
                        control.Observation = state;
                        if (input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.PREPARE_PROVIDER)) {
                            // Saving navigates out of the editor. Restore the already requested destination
                            // through the same UI adapter before claiming that preparation is complete.
                            if (!state.CompiledReady) throw new ValidationException("Completa los campos de la versión guardada antes de preparar el destino.");
                            var response = state.Response; var complete = state.ResponseComplete;
                            state = await browser.Execute("ui_open_provider_picker", JsonSerializer.SerializeToElement(new { promptId = state.PromptId }), ct);
                            state = state with { Response = response, ResponseComplete = complete };
                            if (!state.ProviderOpen || semantic.ProviderId is { } restored && restored != "ollama" && state.ProviderId != restored) throw new ValidationException("No se pudo verificar el destino después de guardar.");
                            control.Observation = state;
                        }
                    }
                    if (semantic.Goal is { } finalGoal) {
                        var completion = GoalCompletionValidator.Validate(finalGoal, semantic, state, resolution.Source,
                            resolution.Source is { } finalSource ? library.Get(finalSource.Id) : null, executed, grounded, true,
                            live.Get(input.RunId).EffectCommitted, includePersistence: true, completedClipboard: completedClipboard);
                        if (!completion.Complete) throw new PreparationToolException("GOAL_INCOMPLETE", "finish", "No se verificó la finalización completa de la tarea.");
                    }
                    RecordConversation(state);
                    NovaTelemetry.ResultReady(input.RunId);
                    live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Listo. Las acciones y sus resultados fueron verificados.", "Completion"); return;
                }
                timer.Restart();
                var beforeAction = state with { Values = new(state.Values) };
                live.Emit(input.RunId, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, UiToolCatalog.Label(decision.ToolName) + " · ejecución en los controles reales.", "Execution", decision.ToolName, state.PromptId);
                var applicableRequirements = updateAllowed && !executed.Contains("ui_prepare_update") ? new PromptRequirements(null, null, []) : semantic.Requirements;
                if (decision.ToolName == "ui_verify_prompt_state") VerifyVisible(state, library, false, input.Message, applicableRequirements,semantic.Goal);
                var priorTemplate = state.TemplateText;
                var adaptsUnsaved = decision.ToolName == "ui_prepare_update" && control.WorkingDraft is { SourceId: null } && input.Mode == "SAME_TAB";
                if (decision.ToolName == "ui_prepare_creation" || decision.ToolName == "ui_prepare_update") {
                    var pipeline = new PromptPreparationPipeline(generator, resolver, evaluator);
                    var basis = (semantic.Goal is not null || decision.ToolName == "ui_prepare_update") && semantic.TargetReference != TargetReference.NONE ? resolution.Source : null;
                    var workingBasis = semantic.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT ? control.WorkingDraft : null;
                    RetrievalCandidate[] creationCandidates = basis is null ? [] : [new(workingBasis is null ? basis : basis with { TemplateText = workingBasis.TemplateText }, 10, "Origen vigente solicitado")];
                    var creationRequirements = semantic.Requirements;
                    var basisText = workingBasis?.TemplateText ?? basis?.TemplateText;
                    if (basisText is not null) creationRequirements = creationRequirements with {
                        Constraints = creationRequirements.Constraints.Concat(TemplateConstraints.Required(basisText)).Distinct().ToArray(),
                        WorkingSourceText = string.IsNullOrWhiteSpace(semantic.RequestedTransformation) ? null : basisText };
                    var creation = new PrepareInput(input.RunId, input.Message, SourcePromptId: decision.ToolName == "ui_prepare_update" ? basis?.Id : null, SourceRevision: decision.ToolName == "ui_prepare_update" ? basis?.Revision : null, Values: input.Values, Model: input.Model, SessionId: input.SessionId) { Interpretation = semantic,
                        WorkingDraft = basis is null ? workingBasis : null, AuthorizedPendingRequest = input.AuthorizedPendingRequest, InputScope = input.InputScope };
                    live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Qwen está generando el contenido del nuevo borrador.", "Generation");
                    PreparedDraft? generated = null;
                    for (var attempt = 0; attempt < 2; attempt++) {
                        try {
                            var understanding = adaptsUnsaved ? semantic.Preparation() with { Intent = PreparationIntent.CREATE_PROMPT } : semantic.Preparation();
                            var proposal = await pipeline.DraftGeneration(creation, understanding, creationCandidates, ct);
                            if (proposal.Decision == PreparationDecision.ASK_CLARIFICATION) {
                                RememberPending(); control.Question = proposal.Clarification;
                                live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.MODEL_SELECTED, "Necesito concretar el propósito del nuevo prompt.", "Generation", error: "NEEDS_CLARIFICATION"); return;
                            }
                            generated = pipeline.PromptCompilation(creation, understanding, proposal, creationCandidates);
                            control.GeneratedDraft = PromptPreparationPipeline.FinalPreparation(generated, creationRequirements,semantic.Goal);
                            break;
                        } catch (ValidationException failure) when (attempt == 0 && !failure.Data.Contains("NovaStageRepairExhausted")) {
                            live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "El dominio rechazó la propuesta; solicitando una única corrección estructurada a Qwen.", "GenerationRetry");
                            creation = creation with { ValidationFeedback = JsonSerializer.Serialize(new { failure = failure.Message,
                                rejectedCandidate=failure.Data["NovaRejectedProposal"],
                                failures = generated is null ? [] : PromptPreparationPipeline.Validation(generated, creationRequirements,semantic.Goal).Errors,
                                correction = "Correct only the reported contract violations in the complete proposal. Preserve the original task, identity, substantive constraints and requested effects. Do not introduce new inputs or content merely to satisfy metadata; input declarations describe placeholders already present in the body." }) };
                        }
                    }
                    generated = control.GeneratedDraft ?? throw new ValidationException("No se obtuvo un borrador validado.");
                    if (semantic.Goal is not null) input = input with { Values = (input.Values ?? []).Concat(generated.Values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value)))
                        .GroupBy(value => value.Key).ToDictionary(group => group.Key, group => group.Last().Value) };
                    await live.Boundary(input.RunId, ct);
                    state = await browser.Execute(decision.ToolName, JsonSerializer.SerializeToElement(new { promptId = decision.ToolName == "ui_prepare_update" ? basis?.Id : null, title = generated.Title, templateText = generated.TemplateText }), ct);
                    if (state.Mode != "create" || state.PromptId != (decision.ToolName == "ui_prepare_update" ? basis?.Id : null) || state.Title != generated.Title || state.TemplateText != generated.TemplateText || !state.VariableNames.SequenceEqual(TemplateCompiler.Names(generated.TemplateText)))
                        throw new ValidationException("El formulario no coincide exactamente con el contenido generado.");
                } else state = await browser.Execute(decision.ToolName, decision.Arguments, ct);
                if (decision.ToolName == "ui_prepare_update" && (!state.DraftChanged || string.IsNullOrWhiteSpace(state.TemplateText)
                    || state.TemplateText.Replace("\r\n", "\n").Trim() == priorTemplate?.Replace("\r\n", "\n").Trim()))
                    throw new PreparationToolException("NO_ADAPTATION_OBSERVED", decision.ToolName, "El editor no muestra un cambio respecto del borrador anterior.");
                if (decision.ToolName == "ui_load_active_draft") {
                    if (state.TemplateText != control.WorkingDraft?.TemplateText || state.Title != control.WorkingDraft?.Title) throw new PreparationToolException("ACTIVE_DRAFT_MISMATCH", decision.ToolName, "El editor no coincide con el borrador autorizado.");
                    priorLoaded = true;
                }
                if (decision.ToolName == copyTool && (!GoalCompletionValidator.ClipboardMatches(state, semantic.CopyContent)
                    || state.Clipboard!.OperationId == beforeAction.Clipboard?.OperationId
                    || state.PromptId != beforeAction.PromptId || state.Revision != beforeAction.Revision
                    || (semantic.RequiresTemplatePreview ? state.TemplateText != beforeAction.TemplateText : state.CompiledText != beforeAction.CompiledText)))
                    throw new PreparationToolException("CLIPBOARD_NOT_VERIFIED", decision.ToolName, "No se recibió una copia nueva ligada al texto actual.");
                if (decision.ToolName == copyTool) completedClipboard = state.Clipboard;
                ct.ThrowIfCancellationRequested();
                VerifyVisible(state, library, false, input.Message, decision.ToolName == "ui_prepare_update" ? semantic.Requirements : applicableRequirements,semantic.Goal);
                if (state.Error is not null) throw new PreparationToolException("UI_REPORTED_ERROR", decision.ToolName, "La interfaz informó un error al ejecutar la acción.");
                if (decision.ToolName == "ui_fill_variable" && !UiToolCatalog.FieldValueMatches(state, decision.Arguments))
                    throw new PreparationToolException("VALUES_NOT_APPLIED", decision.ToolName, "El control no confirma el valor escrito. No se registrará la acción como completada.");
                if (decision.ToolName == "ui_search_prompt")
                {
                    searched = true;
                    var query = decision.Arguments.GetProperty("query").GetString()!;
                    var ranked = retriever.Retrieve(query, null).Where(c => state.Candidates.Any(p => p.Id == c.Prompt.Id)).ToArray();
                    var exact = state.Candidates.Count(p => PromptSearch.Normalize(p.Title) == PromptSearch.Normalize(query));
                    ambiguous = ambiguous || exact > 1 || exact == 0 && ranked.Length > 1 && ranked[1].Score >= ranked[0].Score * .85;
                    if (input.SourceId is { } requestedSource && state.Candidates.Any(candidate => candidate.Id == requestedSource)) ambiguous = false;
                }
                executed.Add(decision.ToolName); control.Observation = state;
                if (receipts.Count == 12) receipts.Dequeue();
                receipts.Enqueue(UiObservedEffect.Observe(++receiptCount, decision.ToolName, beforeAction, state));
                if (decision.ToolName is "ui_prepare_creation" or "ui_prepare_update" or "ui_fill_variable" or "ui_load_active_draft") {
                    RecordConversation(state, decision.ToolName switch {
                        "ui_prepare_creation" => DraftObservationKind.NewArtifact,
                        "ui_prepare_update" => DraftObservationKind.Authored,
                        _ => DraftObservationKind.Snapshot }); conversationCheckpointed = true;
                }
                lastResult = decision.ToolName + ": OK. The observed identity, source revision and current UI contents passed independent verification; no source write occurred.";
                if (decision.ToolName is "ui_read_visible_state" or "ui_verify_prompt_state" or "ui_show_compiled_prompt") inspections[decision.ToolName] = stateHash;
                live.Emit(input.RunId, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, UiToolCatalog.Label(decision.ToolName) + " · resultado comprobado en la interfaz.", "Observation", decision.ToolName, state.PromptId, timer.Elapsed.TotalMilliseconds,
                    update: r => r with { TargetId = state.PromptId, Revision = state.Revision });
            }
            throw new PreparationToolException("UI_STEP_LIMIT", "", "Se alcanzó el límite de decisiones. No se aplicaron cambios pendientes.");
        }
        catch (OperationCanceledException)
        {
            if (!live.Get(input.RunId).EffectCommitted && !conversationCheckpointed) {
                if (conversationBeforeRun is not null) sessions?.Put(conversationBeforeRun with { UpdatedAt = DateTime.UtcNow, Approval = "NOT_GRANTED",
                    PendingPlan = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingPlan,
                    PendingRequest = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingRequest,
                    PendingAction = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingAction });
                else sessions?.Forget(input.SessionId);
            }
            if (!control.Cancellation.IsCancellationRequested)
                control.Question = "El modelo tardó más de lo permitido. Puedes volver a intentarlo; la biblioteca y tu borrador se conservan.";
            live.Emit(input.RunId, control.Cancellation.IsCancellationRequested ? LiveStatus.CANCELLED : LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, control.Cancellation.IsCancellationRequested ? "Ejecución detenida; no habrá nuevas acciones." : "Se agotó el tiempo de ejecución.", error: control.Cancellation.IsCancellationRequested ? "CANCELLED" : "TIMEOUT");
        }
        catch (Exception error)
        {
            if (error is ValidationException) control.Question = error.Message[..Math.Min(500, error.Message.Length)];
            var code = error is PreparationToolException tool ? tool.Code : error is ConflictException ? "STALE_OR_UNVERIFIED" : error is ValidationException ? "INVALID_DECISION" : "BROWSER_FAILURE";
            if (code == "MODEL_UNAVAILABLE")
                control.Question = "El modelo local no está disponible. Revisa que Ollama esté abierto y vuelve a intentarlo. Puedes seguir usando tu biblioteca.";
            live.Emit(input.RunId, code is "INVALID_DECISION" or "UI_TOOL_NOT_ALLOWED" or "REQUEST_BLOCKED" or "MANUAL_INTERVENTION" or "UI_OUTCOME_UNKNOWN" ? LiveStatus.BLOCKED : LiveStatus.FAILED,
                LiveActor.SYSTEM_VALIDATION, "No se pudo completar la ejecución. Revisa la actividad; no se declarará éxito sin verificación.", error: code);
        }
        finally { control.Browser = null; live.Emit(input.RunId, live.Get(input.RunId).Status, LiveActor.SYSTEM_VALIDATION, input.Mode == "SAME_TAB" ? "Control de NOVA finalizado. Tu pestaña y el borrador permanecen abiertos." : "El navegador de QA se cerró.", "Browser", update: r => r with { BrowserOpen = false }); }

        void RememberPending() {
            if (semantic is null || sessions is null) return;
            var current = sessions.Get(input.SessionId) ?? new AgentSession(input.SessionId, null, null, [], DateTime.UtcNow);
            sessions.Put(current with { UpdatedAt = DateTime.UtcNow, PendingPlan = semantic,
                PendingRequest = input.AuthorizedPendingRequest ?? input.Message, Approval = "NOT_GRANTED",
                PendingCommand = input.Command ?? current.PendingCommand });
        }
        void RecordConversation(UiVisibleState state, DraftObservationKind kind = DraftObservationKind.Snapshot) {
            if (state.TemplateText is null) return;
            if (state.PromptId is null) {
                if (state.Mode != "create" || string.IsNullOrWhiteSpace(state.Title) || !state.DraftChanged) return;
                var previous = sessions?.Get(input.SessionId);
                var values = state.Values.Where(value => !string.IsNullOrWhiteSpace(value.Value)).Select(value => new VariableValue(value.Key, value.Value, "USER_INPUT", "VALIDATED")).ToArray();
                var observed = evaluator.Evaluate(new(input.RunId, input.Message, SessionId: input.SessionId),
                    new(PreparationIntent.CREATE_PROMPT, "", "", []), new(PreparationDecision.CREATE_NEW, null, state.Title, "", state.TemplateText, [], "", "Borrador sin guardar observado."), [], values)
                    with { SessionId = input.SessionId, Semantic = semantic };
                workingCheckpoint = sessions?.ObserveDraft(new(input.SessionId, previous?.Source, observed, previous?.Values ?? [], DateTime.UtcNow, previous?.Candidates), kind).WorkingDraftReference;
                return;
            }
            var id = state.PromptId.Value;
            var source = library.Get(id);
            if (source.Revision != state.Revision) throw new ConflictException("La revisión visible cambió antes de actualizar el contexto.");
            var draft = ObservedSourceDraft(state, source);
            var searchContext = (sessions?.Get(input.SessionId)?.Candidates ?? []).Select(candidate => candidate.Id == source.Id ? source : candidate).Take(6).ToArray();
            workingCheckpoint = sessions?.ObserveDraft(new(input.SessionId, source, draft, state.Values, DateTime.UtcNow, searchContext), kind).WorkingDraftReference;
        }
        PreparedDraft ObservedSourceDraft(UiVisibleState state, PromptDocument source) {
            var values = state.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED")).ToArray();
            return evaluator.Evaluate(new(input.RunId, input.Message, SessionId: input.SessionId), semantic!.Preparation(),
                new(state.TemplateText == source.TemplateText ? PreparationDecision.REUSE_EXISTING : PreparationDecision.ADAPT_EXISTING,
                    source.Id, state.Title ?? source.Title, source.Description, state.TemplateText!, [], "", "Resultado observado en el navegador real."), [new(source, 10, "Origen verificado")], values)
                with { SessionId = input.SessionId, Semantic = semantic };
        }
    }
    private static void VerifyVisible(UiVisibleState state, PromptLibrary library, bool committed, string request, PromptRequirements semanticRequirements,TaskGoal? goal=null)
    {
        using var measurement = NovaTelemetry.Measure("verification.visible");
        if (state.Candidates.Length > 24 || state.Values.Count > 60 || state.VariableNames.Length > 60 || state.TemplateText?.Length > 32000 || state.CompiledText?.Length > 512000 || state.Response?.Length > 512000) throw new ValidationException("Estado visible fuera de límites.");
        if (state.PromptId is not { } id) {
            if (state.Mode == "create" && state.TemplateText is { } body && (!state.VariableNames.SequenceEqual(TemplateCompiler.Names(body))
                || state.CompiledReady && TemplateCompiler.Render(body, state.Values) != state.CompiledText)) throw new ValidationException("El borrador visible no coincide con sus campos y compilación.");
            return;
        }
        var source = library.Get(id);
        if (source.Revision != state.Revision || source.IsArchived) throw new PreparationToolException("UI_SOURCE_REVISION_MISMATCH", "ui_verify_prompt_state", "Identidad o revisión visible inválida.");
        if (!committed && !state.DraftChanged && state.TemplateText != source.TemplateText) throw new PreparationToolException("UI_SOURCE_CONTENT_MISMATCH", "ui_verify_prompt_state", "El contenido visible no coincide con el original.");
        if (state.DraftChanged && state.TemplateText is { } changed && TemplateConstraints.Missing(source.TemplateText, changed).Length > 0) throw new PreparationToolException("UI_SOURCE_CONSTRAINT_OMITTED", "ui_verify_prompt_state", "El borrador omitió una restricción del original.");
        if (state.DraftChanged && state.TemplateText is { } templateDraft)
        {
            var values = state.Values.Where(value => !string.IsNullOrWhiteSpace(value.Value)).Select(value => new VariableValue(value.Key, value.Value, "USER_INPUT", "VALIDATED")).ToArray();
            var prepared = new DeterministicPromptEvaluator().Evaluate(new(Guid.NewGuid(), request), new(PreparationIntent.IMPROVE_PROMPT, "", ""),
                new(PreparationDecision.ADAPT_EXISTING, source.Id, state.Title ?? source.Title, source.Description, templateDraft, [], "", "Borrador observado en React."),
                [new(source, 10, "Origen verificado")], values);
            var requirements = semanticRequirements;
            requirements = requirements with { Constraints = requirements.Constraints.Concat(TemplateConstraints.Required(source.TemplateText)).Distinct().ToArray() };
            if (!PromptPreparationPipeline.Validation(prepared, requirements,goal).Valid) throw new PreparationToolException("UI_DRAFT_REQUIREMENTS_MISMATCH", "ui_verify_prompt_state", "El borrador visible no conserva todos los requisitos de la petición.");
        }
        if (state.CompiledReady && state.TemplateText is { } template)
        {
            var values = source.Defaults.Concat(state.Values).GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.Last().Value);
            if (TemplateCompiler.Render(template, values) != state.CompiledText) throw new ValidationException("La vista previa no coincide con el compilador determinista.");
        }
    }
}
