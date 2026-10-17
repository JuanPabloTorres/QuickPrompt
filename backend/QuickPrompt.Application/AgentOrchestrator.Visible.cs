using System.Diagnostics;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed partial class AgentOrchestrator
{
    // Same domain services; this variant delegates navigation to a bounded UI executor, one model decision at a time.
    public async Task RunVisible(LiveStartInput input, string origin, LiveRuns live, IUiDecisionModel model, IVisibleBrowserFactory factory, PromptLibrary library, int maxSteps = 12)
    {
        var control = live.Active(input.RunId); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(control.Cancellation.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(8)); var ct = timeout.Token; var executed = new List<string>(); var searched = false; var ambiguous = false;
        var localAllowed = false; var updateAllowed = false; SemanticInterpretation? semantic = null;
        var inspections = new Dictionary<string, string>(); var inputShapes = new HashSet<string>(); string? lastResult = null;
        var priorLoaded = false;
        var conversationBeforeRun = sessions?.Get(input.SessionId);
        try
        {
            live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Consultando Qwen para interpretar la petición completa y el contexto autorizado.", "SemanticInterpretation");
            // Each planning phase owns its bounded stage deadline; the run owns the
            // global eight-minute deadline. Source refinement follows actual retrieval.
            var resolution = await planning.Interpret(input.Message, input.SessionId, input.SourceId, input.SourceRevision, input.Model, null, ct);
            resolution = await planning.Resolve(resolution, input.Message, input.SessionId, input.Model, null, ct);
            semantic = resolution.Interpretation; control.Semantic = semantic;
            if (semantic.ValidationNotes?.Contains("PROVIDED_INPUT_BINDINGS_RESOLVED") == true)
                live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.RULE_SELECTED, "Se usarán los valores que proporcionaste para completar el prompt.", "InputPrerequisite");
            input = input with { AuthorizedPendingRequest = semantic.ContinuesPendingTask ? resolution.Context.PendingRequest : null };
            control.AuthorizedTaskRequest = input.TaskRequest; control.VisibleTaskRequest = input.DisplayRequest;
            if (semantic.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT && resolution.ActiveDraft is { } active
                && active.SourceId == resolution.Source?.Id && active.TemplateText != resolution.Source?.TemplateText) control.WorkingDraft = active;
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
            if (semantic.DesiredOutcome == SemanticOutcome.ADVICE) {
                if (sessions?.Get(input.SessionId) is { } context) sessions.Put(context with { PendingPlan = null, PendingRequest = null, PendingAction = null });
                control.Question = semantic.Explanation;
                live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.MODEL_SELECTED, semantic.Explanation, "Explanation"); return;
            }
            if (semantic.DesiredOutcome == SemanticOutcome.CLARIFICATION) {
                RememberPending();
                control.Question = string.Join(" ", semantic.MissingInformation.DefaultIfEmpty(semantic.Explanation));
                live.Emit(input.RunId, LiveStatus.BLOCKED, semantic.ValidationNotes is { Length: > 0 } ? LiveActor.SYSTEM_VALIDATION : LiveActor.MODEL_SELECTED, "Necesito una aclaración antes de actuar.", error: "NEEDS_CLARIFICATION"); return;
            }
            if (semantic.Creates && input.Mode != "SAME_TAB") throw new ValidationException("La creación visual requiere la pestaña emisora.");
            if (resolution.Source is { } resolved) input = input with { SourceId = resolved.Id, SourceRevision = resolved.Revision,
                Values = (input.SourceId == resolved.Id ? input.Values : null) ?? (sessions?.Get(input.SessionId)?.Source?.Id == resolved.Id ? sessions.Get(input.SessionId)!.Values : null) };
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
            if (control.WorkingDraft is { } working) state = await browser.Execute("set_working_context", JsonSerializer.SerializeToElement(new { promptId = working.SourceId, revision = working.SourceRevision,
                title = working.Title, templateText = working.TemplateText, values = working.Values.ToDictionary(v => v.Name, v => v.Value) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), ct);
            control.Observation = state;
            void RecordRejectedArguments(UiDecision invalid)
            {
                lock (control.Gate) control.Decisions.Add(invalid);
                var name = UiToolCatalog.All.Any(tool => tool.Name == invalid.ToolName) ? invalid.ToolName : "UNRECOGNIZED";
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
                if (semantic.Has(SemanticOperation.FILL_VARIABLES) && state.TemplateText is { Length: > 0 } material && generator is IInputValueExtractor extractor
                    && inputShapes.Add(LiveRuns.Hash(new { state.PromptId, material }))) {
                    var known = UiToolCatalog.GroundedValues(input, state);
                    var missing = state.VariableNames.Where(name => !known.ContainsKey(name) && string.IsNullOrWhiteSpace(state.Values.GetValueOrDefault(name))).ToArray();
                    if (missing.Length > 0) {
                        live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Qwen está vinculando los datos literales de tu petición con los campos reales.", "InputExtraction");
                        var request = new PrepareInput(input.RunId, input.Message, Model: input.Model) { AuthorizedPendingRequest = input.AuthorizedPendingRequest };
                        var extraction = await extractor.ExtractInputs(request, material, missing, ct);
                        if (extraction.Values is null || extraction.Values.Length > missing.Length || extraction.Values.Select(value => value.Name).Distinct().Count() != extraction.Values.Length
                            || extraction.Values.Any(value => !missing.Contains(value.Name) || string.IsNullOrWhiteSpace(value.Value) || value.Value.Length > 8000 || string.IsNullOrWhiteSpace(value.Evidence)
                                || !value.Evidence.Contains(value.Value, StringComparison.Ordinal) || !(input.Message.Contains(value.Evidence, StringComparison.Ordinal) || input.AuthorizedPendingRequest?.Contains(value.Evidence, StringComparison.Ordinal) == true))
                            || extraction.Question is null || extraction.Question.Length > 500)
                            throw new ValidationException("Los valores deben citar literalmente datos autorizados y corresponder a los campos reales.");
                        if (!string.IsNullOrWhiteSpace(extraction.Question)) {
                            RememberPending(); control.Question = extraction.Question; control.Observation = state;
                            live.Emit(input.RunId, LiveStatus.BLOCKED, LiveActor.MODEL_SELECTED, "Necesito precisar un dato antes de completar los campos.", "InputExtraction", error: "NEEDS_CLARIFICATION");
                            return;
                        }
                        input = input with { Values = known.Concat(extraction.Values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value))).ToDictionary(value => value.Key, value => value.Value) };
                    }
                }
                var grounded = UiToolCatalog.GroundedValues(input, state);
                var stateHash = LiveRuns.Hash(new { state.Mode, state.PromptId, state.Revision, state.TemplateText, state.Values, state.ResponseComplete });
                var executionPending = localAllowed && !executed.Contains("ui_execute_local");
                var capabilities = executionPending ? state with { ResponseComplete = false } : state;
                var allowedTools = UiToolCatalog.Available(capabilities, searched, ambiguous, localAllowed, updateAllowed, false, input.SourceId, executed.Contains("ui_prepare_update")).ToList();
                if (input.Mode == "SAME_TAB" && input.SourceId is { } desired && state.PromptId is { } current && desired != current) allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ui_navigate_library" or "ask_clarification").ToList();
                if (semantic.Creates) {
                    allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ask_clarification" or "ui_read_visible_state"
                        || control.GeneratedDraft is not null && (tool.Name is "finish" or "ui_verify_prompt_state"
                            || tool.Name == "ui_fill_variable" && state.VariableNames.Length > 0
                            || state.CompiledReady && (tool.Name is "ui_show_compiled_prompt" or "ui_open_provider_picker" || tool.Name == "ui_execute_local" && executionPending))
                        || state.Mode != "create" && tool.Name == "ui_open_create_prompt"
                        || state.Mode == "create" && control.GeneratedDraft is null && tool.Name == "ui_prepare_creation").ToList();
                }
                if (input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.SEARCH_PROMPT) && !searched && state.Mode == "discover") allowedTools.RemoveAll(tool => tool.Name == "ui_open_prompt");
                if (control.WorkingDraft is not null && !priorLoaded && state.PromptId == input.SourceId) allowedTools = UiToolCatalog.All.Where(tool => tool.Name is "ui_load_active_draft" or "ui_read_visible_state" or "ask_clarification").ToList();
                if (priorLoaded && updateAllowed && !executed.Contains("ui_prepare_update")) allowedTools.Add(UiToolCatalog.All.Single(tool => tool.Name == "ui_prepare_update"));
                var providerRequested = semantic.Has(SemanticOperation.PREPARE_PROVIDER) || semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || localAllowed;
                var providerPending = !state.ProviderOpen || input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.PREPARE_PROVIDER)
                    && semantic.ProviderId is { } wantedProvider && wantedProvider != "ollama" && state.ProviderId != wantedProvider;
                var available = allowedTools
                    // Every iteration just read the live controls, and every effect is
                    // independently verified below. Asking the model to request those
                    // same observations again adds inference without new evidence.
                    .Where(tool => tool.Name is not ("ui_read_visible_state" or "ui_verify_prompt_state"))
                    .Where(tool => SemanticPlanValidator.ToolAllowed(semantic, tool.Name))
                    .Where(tool => (tool.Name != "ui_fill_variable" || grounded.Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value))
                        && (!(tool.Name is "ui_execute_local" or "ui_open_provider_picker" or "ui_show_compiled_prompt") || grounded.All(value => state.Values.GetValueOrDefault(value.Key) == value.Value))
                        && (tool.Name != "ui_open_provider_picker" || providerRequested && providerPending)
                        && (tool.Name != "ui_execute_local" || executionPending)
                        && (!(tool.Name is "finish" or "ui_request_save_version") || (!updateAllowed || state.DraftChanged && executed.Contains("ui_prepare_update")) && (!localAllowed || state.ResponseComplete && executed.Contains("ui_execute_local")) && grounded.All(value => state.Values.GetValueOrDefault(value.Key) == value.Value))
                        && (tool.Name != "finish" || (!(semantic.Has(SemanticOperation.FILL_VARIABLES) || semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION)) || state.CompiledReady)
                            && (!semantic.Has(SemanticOperation.PREPARE_PROVIDER) || !providerPending))
                        && inspections.GetValueOrDefault(tool.Name) != stateHash).ToArray();
                // Once the observed postconditions permit completion, optional questions and
                // repeated preparation cannot extend the task or reopen an already ready picker.
                var completionReady = !ambiguous && available.Any(tool => tool.Name == "finish");
                if (completionReady) available = available.Where(tool => tool.Name is not ("ask_clarification" or "ui_read_visible_state" or "ui_verify_prompt_state" or "ui_show_compiled_prompt")).ToArray();
                live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Consultando Qwen para elegir la siguiente acción a partir del estado observado.", "Model");
                var timer = Stopwatch.StartNew(); UiDecision decision;
                using (var stage = CancellationTokenSource.CreateLinkedTokenSource(ct)) { stage.CancelAfter(TimeSpan.FromSeconds(options.StageTimeoutSeconds)); decision = await model.Decide(new(input.TaskRequest, state, executed.ToArray(), available, input.SourceId, ambiguous ? "Several compatible candidates: ask a concrete clarification; do not choose arbitrarily." : priorLoaded && updateAllowed && !executed.Contains("ui_prepare_update") ? "The previous authorized draft is loaded, but the NEW requested content change has not been applied. Prepare that change with ui_prepare_update before finishing." : state.DraftChanged && executed.Contains("ui_prepare_update") && !localAllowed ? "The requested draft change is observed. Empty placeholders do not block review: finish or request human review without filling them." : null, grounded, lastResult, input.SourceId is { } source ? library.Get(source).Title : null, ModelRetry, localAllowed, updateAllowed, RecordRejectedArguments, semantic), stage.Token); }
                lock (control.Gate) control.Decisions.Add(decision);
                var safeTool = UiToolCatalog.All.Any(tool => tool.Name == decision.ToolName) ? decision.ToolName : "UNRECOGNIZED";
                live.Emit(input.RunId, LiveStatus.TOOL_PROPOSED, LiveActor.MODEL_SELECTED, "Qwen eligió: " + UiToolCatalog.Label(safeTool) + ".", "Decision", safeTool, state.PromptId, timer.Elapsed.TotalMilliseconds);
                UiToolCatalog.Validate(decision, available, state, input, semantic);
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
                    if (updateAllowed && (!state.DraftChanged || !executed.Contains("ui_prepare_update")) || localAllowed && !state.ResponseComplete || state.PromptId is null && !semantic.Creates)
                        throw new PreparationToolException("INCOMPLETE_RESULT", decision.ToolName, "La interfaz todavía no confirma todos los efectos solicitados.");
                    if (grounded.Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value)) throw new PreparationToolException("VALUES_NOT_APPLIED", decision.ToolName, "La interfaz todavía no confirma los valores proporcionados.");
                    if ((semantic.Has(SemanticOperation.FILL_VARIABLES) || semantic.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION)) && !state.CompiledReady
                        || semantic.Has(SemanticOperation.PREPARE_PROVIDER) && !state.ProviderOpen) throw new PreparationToolException("INCOMPLETE_PREPARATION", decision.ToolName, "Faltan resultados visibles requeridos por el plan.");
                    if (semantic.Creates && (control.GeneratedDraft is not { } generated || state.Title != generated.Title || state.TemplateText != generated.TemplateText)) throw new ValidationException("El borrador visible cambió antes de su verificación.");
                    VerifyVisible(state, library, live.Get(input.RunId).EffectCommitted, input.Message, semantic.Requirements);
                    if (updateAllowed && state.TemplateText is { } modified && state.PromptId is { } sourceId
                        && semantic.Constraints.Shortening
                        && modified.Length >= library.Get(sourceId).TemplateText.Length)
                        throw new PreparationToolException("SHORTER_DRAFT_NOT_VERIFIED", decision.ToolName, "La propuesta no es más breve que el original.");
                    live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Resultado visible y revisión persistida comprobados.", "Verification", "ui_verify_prompt_state", state.PromptId);
                    if (input.Mode == "SAME_TAB" && semantic.Has(SemanticOperation.PREPARE_PROVIDER) && semantic.ProviderId is { } destination && destination != "ollama" && state.ProviderId != destination) throw new ValidationException("El destino visible no coincide con el solicitado.");
                    if (state.DraftChanged && (updateAllowed || semantic.Creates))
                    {
                        if (input.Mode == "SAME_TAB" && state.ProviderOpen) {
                            var previousResponse = state.Response; var previousComplete = state.ResponseComplete;
                            state = await browser.Execute("prepare_review", UiToolCatalog.NoArguments, ct);
                            state = state with { Response = previousResponse, ResponseComplete = previousComplete }; control.Observation = state;
                        }
                        RecordConversation(state);
                        live.ProposeSave(input.RunId, state);
                        var approved = await control.ApprovalDecision.Task.WaitAsync(ct);
                        if (!approved) throw new OperationCanceledException(ct);
                        await live.Boundary(input.RunId, ct);
                        // This internal command is never exposed to Qwen. It clicks the actual approval-bound React control.
                        var priorResponse = state.Response; var responseComplete = state.ResponseComplete;
                        state = await browser.Execute("commit_approved", UiToolCatalog.NoArguments, ct);
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
                    RecordConversation(state);
                    live.Emit(input.RunId, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Listo. Las acciones y sus resultados fueron verificados.", "Completion"); return;
                }
                timer.Restart();
                live.Emit(input.RunId, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, UiToolCatalog.Label(decision.ToolName) + " · ejecución en los controles reales.", "Execution", decision.ToolName, state.PromptId);
                var applicableRequirements = updateAllowed && !executed.Contains("ui_prepare_update") ? new PromptRequirements(null, null, []) : semantic.Requirements;
                if (decision.ToolName == "ui_verify_prompt_state") VerifyVisible(state, library, false, input.Message, applicableRequirements);
                var priorTemplate = state.TemplateText;
                if (decision.ToolName == "ui_prepare_creation") {
                    var pipeline = new PromptPreparationPipeline(generator, resolver, evaluator);
                    var creation = new PrepareInput(input.RunId, input.Message, Model: input.Model, SessionId: input.SessionId) { Interpretation = semantic, AuthorizedPendingRequest = input.AuthorizedPendingRequest };
                    live.Emit(input.RunId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Qwen está generando el contenido del nuevo borrador.", "Generation");
                    PreparedDraft? generated = null;
                    for (var attempt = 0; attempt < 2; attempt++) {
                        try {
                            var proposal = await pipeline.DraftGeneration(creation, semantic.Preparation(), [], ct);
                            generated = pipeline.PromptCompilation(creation, semantic.Preparation(), proposal, []);
                            control.GeneratedDraft = PromptPreparationPipeline.FinalPreparation(generated, semantic.Requirements);
                            break;
                        } catch (ValidationException failure) when (attempt == 0) {
                            live.Emit(input.RunId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "El dominio rechazó la propuesta; solicitando una única corrección estructurada a Qwen.", "GenerationRetry");
                            creation = creation with { ValidationFeedback = JsonSerializer.Serialize(new { failure = failure.Message,
                                failures = generated is null ? [] : PromptPreparationPipeline.Validation(generated, semantic.Requirements).Errors,
                                correction = "Return a complete corrected proposal. requiredInputs contains bare variable names, never angle brackets; each name must be accepted by the domain placeholder parser. templateText contains placeholders as <name>. Preserve the requested task and its constraints." }) };
                        }
                    }
                    generated = control.GeneratedDraft ?? throw new ValidationException("No se obtuvo un borrador validado.");
                    await live.Boundary(input.RunId, ct);
                    state = await browser.Execute(decision.ToolName, JsonSerializer.SerializeToElement(new { title = generated.Title, templateText = generated.TemplateText }), ct);
                    if (state.Mode != "create" || state.PromptId is not null || state.Title != generated.Title || state.TemplateText != generated.TemplateText || !state.VariableNames.SequenceEqual(TemplateCompiler.Names(generated.TemplateText)))
                        throw new ValidationException("El formulario no coincide exactamente con el contenido generado.");
                } else state = await browser.Execute(decision.ToolName, decision.Arguments, ct);
                if (decision.ToolName == "ui_prepare_update" && (!state.DraftChanged || string.IsNullOrWhiteSpace(state.TemplateText)
                    || state.TemplateText.Replace("\r\n", "\n").Trim() == priorTemplate?.Replace("\r\n", "\n").Trim()))
                    throw new PreparationToolException("NO_ADAPTATION_OBSERVED", decision.ToolName, "El editor no muestra un cambio respecto del borrador anterior.");
                if (decision.ToolName == "ui_load_active_draft") {
                    if (state.TemplateText != control.WorkingDraft?.TemplateText || state.Title != control.WorkingDraft?.Title) throw new PreparationToolException("ACTIVE_DRAFT_MISMATCH", decision.ToolName, "El editor no coincide con el borrador autorizado.");
                    priorLoaded = true;
                }
                ct.ThrowIfCancellationRequested();
                VerifyVisible(state, library, false, input.Message, decision.ToolName == "ui_prepare_update" ? semantic.Requirements : applicableRequirements);
                if (state.Error is not null) throw new PreparationToolException("UI_REPORTED_ERROR", decision.ToolName, "La interfaz informó un error al ejecutar la acción.");
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
                lastResult = decision.ToolName + ": OK. The observed identity, source revision and current UI contents passed independent verification; no source write occurred.";
                if (decision.ToolName is "ui_read_visible_state" or "ui_verify_prompt_state" or "ui_show_compiled_prompt") inspections[decision.ToolName] = stateHash;
                live.Emit(input.RunId, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, UiToolCatalog.Label(decision.ToolName) + " · resultado comprobado en la interfaz.", "Observation", decision.ToolName, state.PromptId, timer.Elapsed.TotalMilliseconds,
                    update: r => r with { TargetId = state.PromptId, Revision = state.Revision });
            }
            throw new PreparationToolException("UI_STEP_LIMIT", "", "Se alcanzó el límite de decisiones. No se aplicaron cambios pendientes.");
        }
        catch (OperationCanceledException)
        {
            if (!live.Get(input.RunId).EffectCommitted) {
                if (conversationBeforeRun is not null) sessions?.Put(conversationBeforeRun with { UpdatedAt = DateTime.UtcNow, Approval = "NOT_GRANTED",
                    PendingPlan = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingPlan,
                    PendingRequest = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingRequest,
                    PendingAction = semantic?.ContinuesPendingTask == true ? null : conversationBeforeRun.PendingAction });
                else sessions?.Forget(input.SessionId);
            }
            live.Emit(input.RunId, control.Cancellation.IsCancellationRequested ? LiveStatus.CANCELLED : LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, control.Cancellation.IsCancellationRequested ? "Ejecución detenida; no habrá nuevas acciones." : "Se agotó el tiempo de ejecución.", error: control.Cancellation.IsCancellationRequested ? "CANCELLED" : "TIMEOUT");
        }
        catch (Exception error)
        {
            if (error is ValidationException) control.Question = error.Message;
            var code = error is PreparationToolException tool ? tool.Code : error is ConflictException ? "STALE_OR_UNVERIFIED" : error is ValidationException ? "INVALID_DECISION" : "BROWSER_FAILURE";
            live.Emit(input.RunId, code is "INVALID_DECISION" or "UI_TOOL_NOT_ALLOWED" or "REQUEST_BLOCKED" or "MANUAL_INTERVENTION" or "UI_OUTCOME_UNKNOWN" ? LiveStatus.BLOCKED : LiveStatus.FAILED,
                LiveActor.SYSTEM_VALIDATION, "No se pudo completar la ejecución. Revisa la actividad; no se declarará éxito sin verificación.", error: code);
        }
        finally { control.Browser = null; live.Emit(input.RunId, live.Get(input.RunId).Status, LiveActor.SYSTEM_VALIDATION, input.Mode == "SAME_TAB" ? "Control de NOVA finalizado. Tu pestaña y el borrador permanecen abiertos." : "El navegador de QA se cerró.", "Browser", update: r => r with { BrowserOpen = false }); }

        void RememberPending() {
            if (semantic is null || sessions is null) return;
            var current = sessions.Get(input.SessionId) ?? new AgentSession(input.SessionId, null, null, [], DateTime.UtcNow);
            sessions.Put(current with { UpdatedAt = DateTime.UtcNow, PendingPlan = semantic,
                PendingRequest = input.AuthorizedPendingRequest ?? input.Message, Approval = "NOT_GRANTED" });
        }
        void RecordConversation(UiVisibleState state) {
            if (state.PromptId is not { } id || state.TemplateText is null) return;
            var source = library.Get(id);
            if (source.Revision != state.Revision) throw new ConflictException("La revisión visible cambió antes de actualizar el contexto.");
            var values = state.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED")).ToArray();
            var draft = evaluator.Evaluate(new(input.RunId, input.Message, SessionId: input.SessionId), semantic!.Preparation(),
                new(state.TemplateText == source.TemplateText ? PreparationDecision.REUSE_EXISTING : PreparationDecision.ADAPT_EXISTING,
                    source.Id, state.Title ?? source.Title, source.Description, state.TemplateText, [], "", "Resultado observado en el navegador real."), [new(source, 10, "Origen verificado")], values)
                with { SessionId = input.SessionId, Semantic = semantic };
            drafts.Put(draft, LiveRuns.Hash(new { input.RunId, purpose = "visible-conversation-context" }));
            var searchContext = (sessions?.Get(input.SessionId)?.Candidates ?? []).Select(candidate => candidate.Id == source.Id ? source : candidate).Take(6).ToArray();
            sessions?.Put(new(input.SessionId, source, draft, state.Values, DateTime.UtcNow, searchContext, semantic, null, "NOT_GRANTED"));
        }
    }
    private static void VerifyVisible(UiVisibleState state, PromptLibrary library, bool committed, string request, PromptRequirements semanticRequirements)
    {
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
            if (!PromptPreparationPipeline.Validation(prepared, requirements).Valid) throw new PreparationToolException("UI_DRAFT_REQUIREMENTS_MISMATCH", "ui_verify_prompt_state", "El borrador visible no conserva todos los requisitos de la petición.");
        }
        if (state.CompiledReady && state.TemplateText is { } template)
        {
            var values = source.Defaults.Concat(state.Values).GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.Last().Value);
            if (TemplateCompiler.Render(template, values) != state.CompiledText) throw new ValidationException("La vista previa no coincide con el compilador determinista.");
        }
    }
}
