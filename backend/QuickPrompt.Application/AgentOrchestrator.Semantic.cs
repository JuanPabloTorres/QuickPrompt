using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed partial class AgentOrchestrator
{
    private async Task<PreparedDraft> PrepareSemantic(PrepareInput original, Func<PreparationState, Task> progress, Action<string, string, double> observe,
        CancellationToken ct, Func<string, Task>? responseProgress, Func<string, CancellationToken, Task>? beforeTool, Action<string>? modelConsulted)
    {
        Validate(original);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(original with { Values = original.Values?.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary() }))));
        if (drafts.Fingerprint(original.OperationId) is { } previous)
        {
            if (previous != fingerprint) throw new ConflictException("Identificador usado para otra petición.");
            return drafts.Get(original.OperationId) ?? throw new ConflictException("Borrador expirado. Inicia otra solicitud.");
        }
        if (runs?.Get(original.OperationId) is not null) throw new ConflictException("Esta operación ya se inició; no se repetirán sus efectos.");
        var sessionId = original.SessionId ?? original.OperationId;
        var conversationBeforePreparation = sessions?.Get(sessionId);
        var run = new AgentCheckpoint(original.OperationId, sessionId, fingerprint, AgentRunState.RECEIVE, [], [], [], [], options.SemanticMaxSteps, "NOT_GRANTED", null, null, original.Model, "nova-semantic-1.0", DateTime.UtcNow);
        if (runs is not null && !runs.TryBegin(run)) throw new ConflictException("La operación ya está en curso.");
        void SaveRun(AgentRunState state) { run = run with { State = state, UpdatedAt = DateTime.UtcNow }; runs?.Put(run); }
        using var total = CancellationTokenSource.CreateLinkedTokenSource(ct); total.CancelAfter(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));
        var token = total.Token;
        try
        {
            await progress(PreparationState.RECEIVED); await progress(PreparationState.UNDERSTANDING); SaveRun(AgentRunState.INTERPRET); modelConsulted?.Invoke("SemanticInterpretation");
            SemanticResolution resolution;
            using (var stage = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                stage.CancelAfter(TimeSpan.FromSeconds(options.StageTimeoutSeconds));
                resolution = await planning.Interpret(original.Message, sessionId, original.SourcePromptId, original.SourceRevision, original.Model, original.Filter, stage.Token);
            }
            var plan = resolution.Interpretation; var understanding = plan.Preparation();
            if (plan.Deletes) throw new ValidationException("Solicita la eliminación desde NOVA en esta pestaña para revisar y confirmar el registro.");
            SemanticPlanValidator.Validate(plan, resolution.Context, original.Message, options.SemanticMaxSteps);
            var input = original with { Interpretation = plan, AuthorizedPendingRequest = plan.ContinuesPendingTask ? resolution.Context.PendingRequest : null, InputScope = plan.InputScope }; PromptProposal? proposal = null; PreparedDraft? draft = null;
            var approvedPreferences = plan.Adapts || plan.Creates ? preferences?.Get() : null;
            if (approvedPreferences is not null && (approvedPreferences.Language is not null || approvedPreferences.Format is not null || approvedPreferences.ProviderId is not null)) input = input with { ApprovedPreferences = approvedPreferences };
            var requirements = plan.Requirements with { Language = plan.Constraints.Language ?? approvedPreferences?.Language, Format = plan.Constraints.Format ?? approvedPreferences?.Format, EditInstruction = input.TaskRequest, ContentTransformation = plan.RequestedTransformation };
            var source = resolution.Source; var candidates = resolution.Candidates;
            var completed = new HashSet<int>(); var didRevise = false; string? inputQuestion = null;
            var pipeline = new PromptPreparationPipeline(generator, resolver, evaluator);
            var toolRegistry = new PreparationTools(options.SemanticMaxSteps, options.StageTimeoutSeconds, (tool, outcome, ms) => {
                run = run with { Tools = [..run.Tools, new(tool, outcome, Math.Round(ms, 2))] }; runs?.Put(run); observe(tool, outcome, ms);
            }, async (tool, cancel) => {
                SemanticPlanValidator.AuthorizeTool(plan, tool);
                if (source is not null) {
                    var current = retriever.Retrieve("", source.Id).Single(c => c.Prompt.Id == source.Id).Prompt;
                    if (current.Revision != source.Revision || current.IsArchived) throw new ConflictException("El origen cambió durante la preparación. Revisa la versión vigente.");
                }
                if (beforeTool is not null) await beforeTool(tool, cancel);
            });
            var empty = ContractSchema.Object(new());
            readTools?.Register(toolRegistry);
            toolRegistry.Register<EmptyArgs, RetrievalCandidate[]>("search_prompts", ToolPermission.Read, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); return Task.FromResult(retriever.Retrieve(plan.Query, null, input.Filter)); });
            toolRegistry.Register<EmptyArgs, SemanticResolution>("resolve_prompt", ToolPermission.Read, empty, async (_, cancel) => {
                modelConsulted?.Invoke("SemanticResolution"); return await planning.Resolve(resolution with { Candidates = candidates }, original.Message, sessionId, input.Model, input.Filter, cancel); });
            toolRegistry.Register<EmptyArgs, PromptDocument>("open_prompt", ToolPermission.Read, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); return Task.FromResult(source ?? throw new ValidationException("Falta un destino validado.")); });
            toolRegistry.Register<EmptyArgs, PreparedDraft>("open_working_draft", ToolPermission.Read, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); return Task.FromResult(input.WorkingDraft ?? throw new ValidationException("No existe ese borrador en esta sesión.")); });
            string CorrectionFeedback(string[] failures, string? rejectedCandidate = null)
            {
                var actualSource = input.WorkingDraft?.TemplateText ?? candidates.SingleOrDefault(candidate => candidate.Prompt.Id == input.SourcePromptId)?.Prompt.TemplateText ?? "";
                return PromptPreparationPipeline.CorrectionFeedback(failures,requirements,actualSource,rejectedCandidate);
            }
            async Task<PromptProposal> Generate(CancellationToken cancel) {
                modelConsulted?.Invoke("Generation");
                try { return await pipeline.DraftGeneration(input, understanding, candidates, cancel); }
                catch (ValidationException failure) when ((plan.Adapts || plan.Creates) && !didRevise) {
                    // The shared editor can reject a malformed/variable-changing body before a
                    // PreparedDraft exists. That rejection consumes the SAME single correction budget.
                    didRevise = true; modelConsulted?.Invoke("Revision");
                    var correction = input with { ValidationFeedback = CorrectionFeedback([failure.Message[..Math.Min(500, failure.Message.Length)]]) };
                    return await pipeline.DraftGeneration(correction, understanding, candidates, cancel);
                }
            }
            toolRegistry.Register<EmptyArgs, PromptProposal>("propose_prompt_update", ToolPermission.Prepare, empty, (_, cancel) => Generate(cancel));
            toolRegistry.Register<EmptyArgs, PromptProposal>("propose_new_prompt", ToolPermission.Prepare, empty, (_, cancel) => Generate(cancel));
            toolRegistry.Register<EmptyArgs, PreparedDraft>("validate_prompt", ToolPermission.Prepare, empty, async (_, cancel) => {
                if (proposal is null) throw new ValidationException("No hay propuesta que validar.");
                var checkedDraft = pipeline.PromptCompilation(input, understanding, proposal, candidates);
                diagnostics?.Put(input.OperationId, checkedDraft, null);
                if (!PromptPreparationPipeline.Validation(checkedDraft, requirements,plan.Goal).Valid && !didRevise) {
                    didRevise = true; modelConsulted?.Invoke("Revision");
                    var revision = input with { ValidationFeedback = CorrectionFeedback(PromptPreparationPipeline.Validation(checkedDraft, requirements,plan.Goal).Errors, checkedDraft.TemplateText) };
                    proposal = await generator.Generate(revision, understanding, candidates, cancel);
                    checkedDraft = pipeline.PromptCompilation(input, understanding, proposal, candidates); diagnostics?.Put(input.OperationId, null, checkedDraft);
                }
                return PromptPreparationPipeline.FinalPreparation(checkedDraft, requirements,plan.Goal);
            });
            toolRegistry.Register<EmptyArgs, PreparedDraft>("compile_prompt", ToolPermission.Prepare, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested();
                if (proposal is null) throw new ValidationException("Falta el prompt validado.");
                return Task.FromResult(PromptPreparationPipeline.FinalPreparation(pipeline.PromptCompilation(input, understanding, proposal, candidates), requirements,plan.Goal)); });
            toolRegistry.Register<EmptyArgs, VariableValue[]>("resolve_variable_values", ToolPermission.Prepare, empty, async (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); proposal ??= Reuse(source);
                var names = TemplateCompiler.Names(proposal.TemplateText);
                var literal = GroundedVariableResolver.LiteralAssignments(input.Message, names, input.InputScope);
                var ambiguous = GroundedVariableResolver.AmbiguousLabels(input.Message, names);
                bool Grounded(GroundedValue value) => !string.IsNullOrWhiteSpace(value.Value) && !string.IsNullOrWhiteSpace(value.Evidence)
                    && value.Evidence.Contains(value.Value, StringComparison.Ordinal) && (input.Message.Contains(value.Evidence, StringComparison.Ordinal)
                        || input.AuthorizedPendingRequest?.Contains(value.Evidence, StringComparison.Ordinal) == true);
                var missing = names.Where(name => !literal.ContainsKey(name) && !ambiguous.Contains(name)
                    && !(input.Values?.TryGetValue(name, out var explicitValue) == true && !string.IsNullOrWhiteSpace(explicitValue))
                    && !proposal.Values.Any(value => value.Name == name && Grounded(value) && input.Message.Contains(value.Evidence, StringComparison.Ordinal))).ToArray();
                if (fieldExtractor is { } extractor && missing.Length > 0) {
                    modelConsulted?.Invoke("InputExtraction");
                    var extracted = await extractor.ExtractInputs(input, proposal.TemplateText, missing, cancel);
                    if (extracted.Values is null || extracted.Values.Length > missing.Length || extracted.Values.Select(value => value.Name).Distinct().Count() != extracted.Values.Length
                        || extracted.Values.Any(value => !missing.Contains(value.Name) || value.Value.Length > 8000 || value.Evidence.Length > 16000 || !Grounded(value))
                        || extracted.Question is null || extracted.Question.Length > 500) throw new PreparationToolException("UNGROUNDED_INPUT_VALUE", "resolve_variable_values", "Los datos deben corresponder a campos reales y citas literales del usuario.");
                    proposal = proposal with { Values = proposal.Values.Where(value => !extracted.Values.Any(fresh => fresh.Name == value.Name)).Concat(extracted.Values).ToArray() };
                    inputQuestion = extracted.Question;
                }
                return resolver.Resolve(input, proposal, source); });
            toolRegistry.Register<EmptyArgs, PreparedDraft>("prepare_local_execution", ToolPermission.Prepare, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); if (draft?.State != PreparationState.READY) throw new ValidationException("Completa los datos antes de preparar la ejecución."); return Task.FromResult(draft); });
            toolRegistry.Register<EmptyArgs, ProviderCapability>("prepare_provider_transfer", ToolPermission.Prepare, empty, (_, cancel) => {
                cancel.ThrowIfCancellationRequested(); return Task.FromResult(ProviderRegistry.Providers.Single(p => p.ProviderId == (plan.ProviderId ?? "ollama"))); });
            toolRegistry.Register<EmptyArgs, string>("execute_local_llm", ToolPermission.LocalExecution, empty, async (_, cancel) => {
                if (!plan.Executes || executor is null || draft?.State != PreparationState.READY || plan.Constraints.ForbiddenOperations.Contains(SemanticOperation.EXECUTE_LOCAL)) throw new ValidationException("No hay ejecución local autorizada y preparada.");
                if (draft.Values.Any(value => value.Value.Contains(plan.ExecutionEvidence, StringComparison.Ordinal))) throw new ValidationException("La evidencia de ejecución coincide con datos del prompt; revisa y usa el control explícito de ejecución.");
                run = run with { EffectStarted = true, Approval = "SEMANTIC_EXPLICIT_LOCAL_REQUEST" }; runs?.Put(run);
                return await executor.Execute(draft, input.Model, responseProgress, cancel); });

            SaveRun(AgentRunState.PLAN);
            foreach (var step in plan.ProposedSteps)
            {
                token.ThrowIfCancellationRequested();
                if (step.DependsOn.Any(id => !completed.Contains(id))) throw new PreparationToolException("UNSATISFIED_DEPENDENCY", "semantic_plan", "Falta validar una dependencia.");
                switch (step.Operation)
                {
                    case SemanticOperation.SEARCH_PROMPT:
                        await progress(PreparationState.SEARCHING); candidates = await toolRegistry.Call<EmptyArgs, RetrievalCandidate[]>("search_prompts", new(), ToolPermission.Read, token); break;
                    case SemanticOperation.RESOLVE_PROMPT:
                        resolution = await toolRegistry.Call<EmptyArgs, SemanticResolution>("resolve_prompt", new(), ToolPermission.Read, token);
                        plan = resolution.Interpretation; source = resolution.Source; candidates = resolution.Candidates;
                        if (plan.DesiredOutcome == SemanticOutcome.CLARIFICATION) return await Finish(Question(plan));
                        input = input with { Interpretation = plan };
                        understanding = plan.Preparation();
                        requirements = plan.Requirements with { Language = plan.Constraints.Language ?? approvedPreferences?.Language,
                            Format = plan.Constraints.Format ?? approvedPreferences?.Format, EditInstruction = input.TaskRequest, ContentTransformation = plan.RequestedTransformation };
                        if (source is null && resolution.ActiveDraft is { SourceId: null } working && plan.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT) {
                            input = input with { SourcePromptId = null, SourceRevision = null, WorkingDraft = working, ContextValues = working.Values.ToDictionary(v => v.Name, v => v.Value) };
                            understanding = understanding with { Intent = PreparationIntent.CREATE_PROMPT };
                            requirements = requirements with { WorkingSourceText = plan.Adapts ? working.TemplateText : null, Constraints = requirements.Constraints.Concat(TemplateConstraints.Required(working.TemplateText)).Distinct().ToArray() };
                            if (!plan.Adapts && !plan.Creates) proposal = new(PreparationDecision.CREATE_NEW, null, working.Title, working.Description, working.TemplateText, [], "", "Borrador activo sin guardar.");
                        }
                        if (source is not null) {
                            input = input with { SourcePromptId = plan.Creates ? null : source.Id, SourceRevision = plan.Creates ? null : source.Revision };
                            requirements = requirements with { Constraints = requirements.Constraints.Concat(TemplateConstraints.Required(source.TemplateText)).Distinct().ToArray() };
                            if (plan.Creates && !string.IsNullOrWhiteSpace(plan.RequestedTransformation)) requirements = requirements with { WorkingSourceText = source.TemplateText };
                            if (plan.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT && resolution.ActiveDraft is { TemplateText.Length: > 0 } active && active.SourceId == source.Id) {
                                candidates = candidates.Select(c => c.Prompt.Id == source.Id ? c with { Prompt = c.Prompt with { TemplateText = active.TemplateText } } : c).ToArray();
                                requirements = requirements with { Constraints = requirements.Constraints.Concat(TemplateConstraints.Required(active.TemplateText)).Distinct().ToArray(),
                                    WorkingSourceText = plan.Creates && !string.IsNullOrWhiteSpace(plan.RequestedTransformation) ? active.TemplateText : requirements.WorkingSourceText };
                                if (!plan.Adapts && !plan.Creates) proposal = new(active.Decision, active.SourceId, active.Title, active.Description, active.TemplateText, [], "", "Borrador activo de la sesión; original conservado.");
                            }
                            var session = sessions?.Get(sessionId);
                            var priorValues = plan.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT && resolution.ActiveDraft?.SourceId == source.Id
                                ? resolution.ActiveDraft.Values.ToDictionary(v => v.Name, v => v.Value) : session?.Values ?? [];
                            input = input with { ContextValues = plan.Creates && !plan.Has(SemanticOperation.FILL_VARIABLES) ? [] : priorValues };
                        }
                        run = run with { RetrievedPromptIds = candidates.Select(c => c.Prompt.Id).ToArray(), SourceRevisions = candidates.ToDictionary(c => c.Prompt.Id, c => c.Prompt.Revision) }; break;
                    case SemanticOperation.OPEN_PROMPT:
                        if (input.WorkingDraft is not null) await toolRegistry.Call<EmptyArgs, PreparedDraft>("open_working_draft", new(), ToolPermission.Read, token);
                        else { source = await toolRegistry.Call<EmptyArgs, PromptDocument>("open_prompt", new(), ToolPermission.Read, token); proposal ??= Reuse(source); }
                        break;
                    case SemanticOperation.PROPOSE_ADAPTATION:
                    case SemanticOperation.CREATE_DRAFT:
                        await progress(PreparationState.SELECTING_OR_GENERATING);
                        proposal = await toolRegistry.Call<EmptyArgs, PromptProposal>(plan.Creates ? "propose_new_prompt" : "propose_prompt_update", new(), ToolPermission.Prepare, token); break;
                    case SemanticOperation.VALIDATE_DRAFT:
                        await progress(PreparationState.VALIDATING); draft = await toolRegistry.Call<EmptyArgs, PreparedDraft>("validate_prompt", new(), ToolPermission.Prepare, token); break;
                    case SemanticOperation.FILL_VARIABLES:
                        proposal ??= Reuse(source);
                        if (TemplateCompiler.Names(proposal.TemplateText).Length == 0) { plan = TaskClarification.Apply(plan, "Este prompt no tiene campos para completar. ¿Quieres cambiar su instrucción o usarlo tal como está?", "NO_TEMPLATE_INPUTS"); return await Finish(Question(plan)); }
                        await toolRegistry.Call<EmptyArgs, VariableValue[]>("resolve_variable_values", new(), ToolPermission.Prepare, token);
                        if (!string.IsNullOrWhiteSpace(inputQuestion)) { plan = TaskClarification.Apply(plan, inputQuestion, "AMBIGUOUS_INPUT_VALUES"); return await Finish(Question(plan)); }
                        break;
                    case SemanticOperation.COMPILE_PROMPT:
                    case SemanticOperation.COPY_PROMPT:
                        await progress(PreparationState.FILLING);
                        proposal ??= Reuse(source); draft = await toolRegistry.Call<EmptyArgs, PreparedDraft>("compile_prompt", new(), ToolPermission.Prepare, token); break;
                    case SemanticOperation.PREPARE_LOCAL_EXECUTION:
                        if (draft?.State != PreparationState.READY) return await Finish(draft ?? Question(plan));
                        await toolRegistry.Call<EmptyArgs, PreparedDraft>("prepare_local_execution", new(), ToolPermission.Prepare, token); break;
                    case SemanticOperation.PREPARE_PROVIDER:
                        await toolRegistry.Call<EmptyArgs, ProviderCapability>("prepare_provider_transfer", new(), ToolPermission.Prepare, token); break;
                    case SemanticOperation.EXECUTE_LOCAL:
                        if (draft?.State != PreparationState.READY) return await Finish(draft ?? Question(plan));
                        draft = draft with { LocalResponse = await toolRegistry.Call<EmptyArgs, string>("execute_local_llm", new(), ToolPermission.LocalExecution, token) }; break;
                    case SemanticOperation.COPY_TO_CLIPBOARD:
                        throw new PreparationToolException("VISIBLE_CLIPBOARD_REQUIRED", "ui_copy_compiled_prompt", "La copia real requiere el agente en la pestaña visible. El texto preparado se conserva.");
                    case SemanticOperation.CANCEL:
                        var cancelled = sessions?.Get(sessionId)?.Draft;
                        if (cancelled is not null) { drafts.Forget(cancelled.Id); diagnostics?.Forget(cancelled.Id); }
                        sessions?.Forget(sessionId); return await Finish(Question(plan) with { State = PreparationState.CANCELLED, Reason = "Preparación cancelada. Los registros guardados se conservan." });
                    case SemanticOperation.EXPLAIN:
                        if (plan.Goal is { } adviceGoal && manualAdvice is { } advisor) {
                            var answer = await advisor.Explain(original.Message, source, resolution.ActiveDraft, input.Model, token);
                            if (!GoalCompletionValidator.ValidateAdvice(adviceGoal, plan, answer, source, source is null ? null : retriever.ReadSource(source.Id)).Complete)
                                throw new PreparationToolException("GOAL_INCOMPLETE", "explain_advice", "No se pudo verificar el origen de la explicación.");
                            plan = plan with { Explanation = answer };
                        }
                        return await Finish(Question(plan));
                    case SemanticOperation.ASK_CLARIFICATION:
                        return await Finish(Question(plan));
                    case SemanticOperation.REQUEST_SAVE_VERSION:
                    case SemanticOperation.REQUEST_SAVE_COPY:
                        // A natural request opens review; only the existing explicit save API may write.
                        break;
                    case SemanticOperation.PAUSE:
                    case SemanticOperation.RESUME:
                        return await Finish(Question(TaskClarification.Apply(plan, "Usa los controles de la ejecución activa para pausarla o reanudarla.")));
                }
                completed.Add(step.Id);
            }
            if (draft is null) { proposal ??= Reuse(source); draft = pipeline.PromptCompilation(input, understanding, proposal, candidates); }
            return await Finish(draft);

            async Task<PreparedDraft> Finish(PreparedDraft result)
            {
                token.ThrowIfCancellationRequested();
                if (plan.Goal is { } goal && result.State == PreparationState.READY) {
                    var applied = plan.ProposedSteps.Where(step => completed.Contains(step.Id)).Select(step => step.Operation).ToHashSet();
                    var receipts = new List<string>();
                    if (applied.Contains(SemanticOperation.PROPOSE_ADAPTATION)) receipts.Add("ui_prepare_update");
                    if (applied.Contains(SemanticOperation.CREATE_DRAFT)) receipts.Add("ui_prepare_creation");
                    if (applied.Contains(SemanticOperation.EXECUTE_LOCAL)) receipts.Add("ui_execute_local");
                    var visibleResult = new UiVisibleState(plan.Adapts || plan.Creates ? "create" : "focus", [], result.SourceId, result.SourceRevision,
                        result.Title, result.TemplateText, result.Values.ToDictionary(v => v.Name, v => v.Value), result.Variables.Select(v => v.Name).ToArray(),
                        plan.Creates || plan.Adapts && result.TemplateText != source?.TemplateText, result.CompiledText is not null, result.CompiledText,
                        result.LocalResponse, result.LocalResponse is not null, plan.Has(SemanticOperation.PREPARE_PROVIDER), ProviderId: result.ProviderId ?? plan.ProviderId);
                    var completion = GoalCompletionValidator.Validate(goal, plan, visibleResult, source, source is null ? null : retriever.ReadSource(source.Id), receipts,
                        result.Values.ToDictionary(v => v.Name, v => v.Value), PromptPreparationPipeline.Validation(result, requirements).Valid, false);
                    if (!completion.Complete) throw new PreparationToolException("GOAL_INCOMPLETE", "finish", "Faltan resultados obligatorios del contrato de la tarea.");
                }
                if (result.State == PreparationState.NEEDS_INFORMATION && plan.DesiredOutcome == SemanticOutcome.READ
                    && (plan.Has(SemanticOperation.OPEN_PROMPT) || plan.Has(SemanticOperation.SEARCH_PROMPT)) && !plan.Has(SemanticOperation.COPY_PROMPT) && !plan.Has(SemanticOperation.FILL_VARIABLES))
                    result = result with { State = PreparationState.USER_ACTION, Question = "", Reason = "Origen recuperado sin modificar. Completa sus campos solo cuando quieras utilizarlo." };
                var repositoryCandidates = resolution.Candidates.Length > 0 ? resolution.Candidates : candidates;
                result = result with { SessionId = sessionId, Semantic = plan, ProviderId = plan.ProviderId, Candidates = repositoryCandidates };
                if (result.State != PreparationState.CANCELLED) drafts.Put(result, fingerprint);
                var scope = sessions?.Get(sessionId);
                var selectedSource = source ?? scope?.Source;
                var selectedValues = result.SourceId == selectedSource?.Id && result.SourceRevision == selectedSource?.Revision && result.SourceId is not null
                    ? result.Values.ToDictionary(value => value.Name, value => value.Value)
                    : scope?.Source?.Id == selectedSource?.Id && scope?.Source?.Revision == selectedSource?.Revision ? scope?.Values ?? [] : [];
                var observation = new AgentSession(sessionId, selectedSource,
                    result.TemplateText.Length > 0 ? result : resolution.ActiveDraft, selectedValues, DateTime.UtcNow,
                    scope?.Candidates ?? [], scope?.LastProposal, plan.DesiredOutcome == SemanticOutcome.CLARIFICATION ? SemanticOperation.ASK_CLARIFICATION : null,
                    "NOT_GRANTED", source is null && scope?.SourceStale == true, result.State == PreparationState.NEEDS_INFORMATION || plan.DesiredOutcome == SemanticOutcome.CLARIFICATION ? plan : null,
                    result.State == PreparationState.NEEDS_INFORMATION || plan.DesiredOutcome == SemanticOutcome.CLARIFICATION ? input.AuthorizedPendingRequest ?? original.Message : null);
                if (result.State != PreparationState.CANCELLED && sessions is not null) {
                    if (observation.Draft is null) sessions.Put(observation);
                    else sessions.ObserveDraft(observation, result.TemplateText.Length == 0 ? DraftObservationKind.Snapshot
                        : plan.Creates ? DraftObservationKind.NewArtifact : plan.Adapts ? DraftObservationKind.Authored : DraftObservationKind.Snapshot);
                }
                run = run with { PreparedId = result.Id, UserIntent = AgentRouting.Intent(result.Intent), Plan = plan.Preparation().Operations!.Select(AgentRouting.Intent).Distinct().ToArray(), Approval = "SAVE_REQUIRES_REVIEW" };
                SaveRun(result.State == PreparationState.CANCELLED ? AgentRunState.CANCELLED : result.State == PreparationState.NEEDS_INFORMATION ? AgentRunState.NEEDS_CLARIFICATION : AgentRunState.READY);
                await progress(result.State); return result;
            }
            PreparedDraft Question(SemanticInterpretation interpretation) => new(original.OperationId,
                interpretation.DesiredOutcome == SemanticOutcome.ADVICE ? PreparationState.USER_ACTION : PreparationState.NEEDS_INFORMATION,
                PreparationIntent.UNKNOWN, PreparationDecision.ASK_CLARIFICATION, interpretation.DesiredOutcome == SemanticOutcome.ADVICE ? "Orientación de NOVA" : "Una cosa antes de continuar",
                "", "", null, [], [], [], string.Join(" ", interpretation.MissingInformation), interpretation.Explanation, candidates, null, null, 0, original.RememberDraft, DateTime.UtcNow);
        }
        catch (Exception error) {
            // Source lookup is read-only, but it may switch the selected source before a
            // generation fails. Preserve the prior working artifact unless a newer editor
            // snapshot replaced it. Keep the current source and its separately scoped values.
            if (run.PreparedId is null && sessions?.Get(sessionId) is { } current
                && (current.Draft is null || current.Draft.Id == conversationBeforePreparation?.Draft?.Id))
                sessions.Put(current with { Draft = conversationBeforePreparation?.Draft, LastProposal = conversationBeforePreparation?.LastProposal,
                    PendingPlan = conversationBeforePreparation?.PendingPlan, PendingRequest = conversationBeforePreparation?.PendingRequest,
                    PendingAction = conversationBeforePreparation?.PendingAction, Approval = "NOT_GRANTED", UpdatedAt = DateTime.UtcNow });
            run = run with { ErrorCode = error is PreparationToolException tool ? tool.Code : error is OperationCanceledException ? "CANCELLED_OR_TIMEOUT" : error.GetType().Name };
            SaveRun(error is OperationCanceledException && ct.IsCancellationRequested ? AgentRunState.CANCELLED : AgentRunState.FAILED); throw;
        }
    }
    private static PromptProposal Reuse(PromptDocument? source) => source is null ? throw new ValidationException("No existe un origen validado para esta operación.")
        : new(PreparationDecision.REUSE_EXISTING, source.Id, source.Title, source.Description, source.TemplateText, [], "", "Origen real conservado; sin guardar cambios.");
}
