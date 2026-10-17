using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Api;

public record CompletePreparedInput(Guid OperationId, Dictionary<string, string> Values);
public record ExecutePreparedInput(bool Confirmed, string ExpectedText, string Model = "qwen3:8b", Guid? OperationId = null);
public record SessionContextInput(Guid PromptId, int Revision, Dictionary<string, string>? Values = null, bool AllowStaleContext = false);
public record DraftContextInput(Guid DraftId);
public static class PreparationEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void MapPreparation(this RouteGroupBuilder api)
    {
        api.MapGet("/nova/preferences", (IPreparationPreferences preferences) => preferences.Get());
        api.MapPut("/nova/preferences", (PreferenceApproval input, IPreparationPreferences preferences) =>
        { if (!input.Confirmed || input.Preferences is null) throw new ValidationException("Confirma las preferencias que quieres conservar."); preferences.Save(input.Preferences); return preferences.Get(); });
        api.MapDelete("/nova/preferences", (IPreparationPreferences preferences) => { preferences.Forget(); return Results.NoContent(); });
        api.MapGet("/nova/runs/{id:guid}", (Guid id, IAgentRunStore runs) => runs.Get(id) ?? throw new MissingPromptException());
        api.MapDelete("/nova/runs/{id:guid}", (Guid id, IAgentRunStore runs) => { runs.Forget(id); return Results.NoContent(); });
        api.MapPost("/nova/sessions/{id:guid}/context", (Guid id, SessionContextInput input, AgentSessions sessions, PromptLibrary library) =>
        {
            if (id == Guid.Empty || input.Values?.Count > 60 || input.Values?.Any(v => v.Value is null || v.Value.Length > 8000) == true) throw new ValidationException("Contexto inválido.");
            var prompt = library.Get(input.PromptId);
            if (input.Revision < 1 || prompt.IsArchived || prompt.Revision != input.Revision && !input.AllowStaleContext) throw new ConflictException("El origen cambió.");
            if (input.Values?.Any(v => !prompt.VariableNames.Contains(v.Key)) == true) throw new ValidationException("Variable desconocida.");
            var previous = sessions.Get(id);
            var same = previous?.Source?.Id == prompt.Id && previous.Source.Revision == prompt.Revision;
            var stale = prompt.Revision != input.Revision;
            if (stale) prompt = previous?.Source?.Id == prompt.Id && previous.Source.Revision == input.Revision ? previous.Source : prompt with { Revision = input.Revision, TemplateText = "", Defaults = new Dictionary<string, string>(), VariableNames = [] };
            sessions.Put(new(id, prompt, previous?.Draft, same ? previous!.Values.Concat(input.Values ?? []).GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.Last().Value) : input.Values ?? [], DateTime.UtcNow,
                previous?.Candidates, previous?.LastProposal, previous?.PendingAction, "NOT_GRANTED", stale, previous?.PendingPlan, previous?.PendingRequest)); return Results.NoContent();
        });
        api.MapDelete("/nova/sessions/{id:guid}", (Guid id, AgentSessions sessions) => { sessions.Forget(id); return Results.NoContent(); });
        api.MapPost("/nova/sessions/{id:guid}/draft-context", (Guid id, DraftContextInput input, IPreparedDraftStore drafts, AgentSessions sessions) =>
        {
            if (id == Guid.Empty) throw new ValidationException("Sesión inválida.");
            var draft = drafts.Get(input.DraftId) ?? throw new MissingPromptException();
            var previous = sessions.Get(id);
            sessions.Put(new(id, previous?.Source, draft, previous?.Values ?? [], DateTime.UtcNow,
                draft.Candidates.Select(c => c.Prompt).Take(6).ToArray(), draft.Semantic, null, "NOT_GRANTED", previous?.SourceStale ?? false,
                draft.State == PreparationState.NEEDS_INFORMATION ? draft.Semantic : null, previous?.Draft?.Id == draft.Id ? previous.PendingRequest : null));
            return Results.NoContent();
        });
        api.MapPost("/nova/prepare", async (PrepareInput input, AgentOrchestrator orchestrator, HttpContext context, ILogger<AgentOrchestrator> logger, LiveRuns live) =>
        {
            AgentOrchestrator.Validate(input);
            live.Create(new(input.OperationId, input.SessionId ?? input.OperationId, input.Message, "LIVE", input.SourcePromptId, input.SourceRevision, input.Values, input.Model));
            using var liveCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, live.Active(input.OperationId).Cancellation.Token);
            context.Response.ContentType = "text/event-stream";
            async Task Send(string name, object data)
            {
                if (context.RequestAborted.IsCancellationRequested) return;
                await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
            try
            {
                var result = await orchestrator.Prepare(input, async state => {
                        if (state is PreparationState.RECEIVED or PreparationState.UNDERSTANDING or PreparationState.SEARCHING or PreparationState.SELECTING_OR_GENERATING or PreparationState.FILLING or PreparationState.VALIDATING)
                            await live.Boundary(input.OperationId, liveCancellation.Token);
                        await Send("stage", new PreparationProgress(state));
                    },
                    (tool, outcome, milliseconds) => {
                        logger.LogInformation("Preparation {OperationId} tool {Tool} {Outcome} {ElapsedMs}ms", input.OperationId, tool, outcome, Math.Round(milliseconds));
                        live.Emit(input.OperationId, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, outcome == "OK" ? "Herramienta finalizada; resultado registrado." : "La herramienta no pudo completar su operación.", "ToolResult", tool, duration: milliseconds, error: outcome == "OK" ? null : outcome);
                    }, liveCancellation.Token,
                    text => Send("response", new { text }),
                    async (tool, ct) => {
                        live.Emit(input.OperationId, LiveStatus.TOOL_PROPOSED, LiveActor.RULE_SELECTED, "El flujo de preparación seleccionó una herramienta.", "Decision", tool);
                        await live.Boundary(input.OperationId, ct);
                        live.Emit(input.OperationId, LiveStatus.TOOL_APPROVED, LiveActor.SYSTEM_VALIDATION, "Contrato y permisos comprobados.", "Validation", tool);
                        live.Emit(input.OperationId, LiveStatus.TOOL_RUNNING, LiveActor.RULE_SELECTED, "Ejecutando la herramienta de preparación.", "Execution", tool);
                    },
                    stage => live.Emit(input.OperationId, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Consultando Qwen con el contexto autorizado; el plan y sus herramientas se validan antes de actuar.", stage));
                live.Active(input.OperationId).Semantic = result.Semantic;
                if (result.Semantic is not null) live.Emit(input.OperationId, LiveStatus.VERIFYING, result.Semantic.ValidationNotes is { Length: > 0 } ? LiveActor.SYSTEM_VALIDATION : LiveActor.MODEL_SELECTED,
                    "Objetivos interpretados por Qwen; dependencias y resultados comprobados por el dominio.", "SemanticPlan", target: result.SourceId);
                await Send("complete", result);
                var finalStatus = result.State switch { PreparationState.CANCELLED => LiveStatus.CANCELLED, PreparationState.FAILED => LiveStatus.FAILED, PreparationState.BLOCKED or PreparationState.NEEDS_INFORMATION => LiveStatus.BLOCKED, _ => LiveStatus.COMPLETED };
                live.Emit(input.OperationId, finalStatus, LiveActor.SYSTEM_VALIDATION, result.State == PreparationState.CANCELLED ? "Modificación cancelada. No se guardó ningún cambio." : result.State == PreparationState.NEEDS_INFORMATION ? "Falta información del usuario para completar la preparación." : "Preparación validada y enviada al lienzo.", "Completion", target: result.SourceId, error: result.State == PreparationState.NEEDS_INFORMATION ? "NEEDS_CLARIFICATION" : null);
            }
            catch (OperationCanceledException) { live.Emit(input.OperationId, LiveStatus.CANCELLED, LiveActor.SYSTEM_VALIDATION, "Preparación detenida o tiempo agotado.", error: "CANCELLED_OR_TIMEOUT"); await Send("error", new { code = "CANCELLED_OR_TIMEOUT", message = "Preparación detenida o tiempo agotado. Tu trabajo anterior se conserva." }); }
            catch (Exception error) when (error is ValidationException or ConflictException or LlmUnavailableException or PreparationToolException or MissingPromptException)
            { live.Emit(input.OperationId, LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, "No se pudo completar la preparación.", error: error is PreparationToolException toolError ? toolError.Code : error is LlmUnavailableException ? "MODEL_UNAVAILABLE" : "PREPARATION_REJECTED"); await Send("stage", new PreparationProgress(PreparationState.FAILED)); await Send("error", new { code = error is PreparationToolException tool ? tool.Code : error is LlmUnavailableException ? "MODEL_UNAVAILABLE" : "PREPARATION_REJECTED", message = error.Message }); }
            catch (Exception error)
            {
                logger.LogError("Preparation {OperationId} failed with {ErrorType}", input.OperationId, error.GetType().Name);
                live.Emit(input.OperationId, LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, "La preparación encontró un error.", error: "INTERNAL_ERROR");
                await Send("stage", new PreparationProgress(PreparationState.FAILED)); await Send("error", new { code = "INTERNAL_ERROR", message = "No se pudo completar la preparación. Tu trabajo anterior se conserva." });
            }
        }).RequireRateLimiting("ai");
        api.MapGet("/nova/drafts", (IPreparedDraftStore drafts) => drafts.Recent());
        api.MapGet("/nova/drafts/{id:guid}", (Guid id, IPreparedDraftStore drafts) => drafts.Get(id) ?? throw new MissingPromptException());
        api.MapDelete("/nova/drafts/{id:guid}", (Guid id, IPreparedDraftStore drafts) => { drafts.Forget(id); return Results.NoContent(); });
        api.MapPost("/nova/drafts/{id:guid}/save", (Guid id, SavePreparedInput input, IPreparedDraftStore drafts, IAgentRunStore runs) =>
        {
            if (!input.Confirmed) throw new ValidationException("Guardar en la biblioteca requiere una acción explícita.");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var saved = drafts.Save(id, input.UpdateSource);
            if (runs.Get(id) is { } run)
            {
                var durations = new Dictionary<string, double>(run.StageDurations ?? []) { ["Approval"] = timer.Elapsed.TotalMilliseconds };
                runs.Put(run with { Approval = input.UpdateSource ? "APPROVED_SOURCE_VERSION" : "APPROVED_COPY_OR_REUSE", StageDurations = durations, UpdatedAt = DateTime.UtcNow });
            }
            return saved;
        });
        api.MapPost("/nova/drafts/{id:guid}/complete", (Guid id, CompletePreparedInput input, IPreparedDraftStore drafts, IAgentRunStore runs, AgentSessions sessions, LiveRuns live) =>
        {
            var source = drafts.Get(id) ?? throw new MissingPromptException();
            if (input.OperationId == Guid.Empty || input.OperationId == id || input.Values is null || input.Values.Count > 60
                || input.Values.Any(v => !source.Variables.Any(d => d.Name == v.Key) || string.IsNullOrWhiteSpace(v.Value) || v.Value.Length > 8000)) throw new ValidationException("Revisa los valores del borrador.");
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, values = input.Values.OrderBy(v => v.Key, StringComparer.Ordinal) }))));
            if (drafts.Fingerprint(input.OperationId) is { } previous)
            { if (previous != fingerprint) throw new ConflictException("Identificador ya utilizado."); return drafts.Get(input.OperationId)!; }
            var values = source.Values.Where(v => !input.Values.ContainsKey(v.Name)).Concat(input.Values.Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED"))).ToArray();
            var missing = source.Variables.Where(v => !values.Any(value => value.Name == v.Name)).Select(v => v.Name).ToArray();
            var result = source with { Id = input.OperationId, Values = values, MissingVariables = missing,
                CompiledText = missing.Length == 0 ? TemplateCompiler.Render(source.TemplateText, values.ToDictionary(v => v.Name, v => v.Value)) : null,
                State = missing.Length == 0 ? PreparationState.READY : PreparationState.NEEDS_INFORMATION, Question = missing.Length == 0 ? "" : $"¿Puedes aportar {string.Join(", ", missing)}?", CreatedAt = DateTime.UtcNow };
            var run = new AgentCheckpoint(result.Id, source.SessionId ?? source.Id, fingerprint,
                result.State == PreparationState.READY ? AgentRunState.READY : AgentRunState.NEEDS_CLARIFICATION, [AgentIntent.FILL, AgentIntent.COMPILE],
                result.Candidates.Select(c => c.Prompt.Id).ToArray(), result.Candidates.ToDictionary(c => c.Prompt.Id, c => c.Prompt.Revision),
                [new("compile_prompt", "OK", 0), new("validate_prompt", "OK", 0)], 5, "SAVE_REQUIRES_REVIEW", null, result.Id,
                "deterministic", AgentOrchestrator.ContractVersion, DateTime.UtcNow, AgentIntent.FILL);
            if (!runs.TryBegin(run)) throw new ConflictException("Esta operación ya se inició. No se repetirá.");
            drafts.Put(result, fingerprint);
            var sessionId = result.SessionId ?? source.Id;
            var session = sessions.Get(sessionId);
            sessions.Put(new(sessionId, result.Candidates.SingleOrDefault(c => c.Prompt.Id == result.SourceId)?.Prompt ?? session?.Source,
                result, result.Values.ToDictionary(v => v.Name, v => v.Value), DateTime.UtcNow,
                result.Candidates.Select(c => c.Prompt).Take(6).ToArray(), result.Semantic ?? session?.LastProposal, null, "NOT_GRANTED", session?.SourceStale ?? false,
                result.State == PreparationState.NEEDS_INFORMATION ? result.Semantic : null, result.State == PreparationState.NEEDS_INFORMATION ? session?.PendingRequest : null));
            live.Create(new(result.Id, result.SessionId ?? source.Id, "Completar el borrador con los valores aportados.", "LIVE", result.SourceId, result.SourceRevision));
            live.Emit(result.Id, LiveStatus.VERIFYING, LiveActor.RULE_SELECTED, "Compilación determinista completada con los valores aportados; no se consultó al modelo.", "Compilation", "compile_prompt", result.SourceId);
            live.Emit(result.Id, result.State == PreparationState.READY ? LiveStatus.COMPLETED : LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION, result.State == PreparationState.READY ? "Resultado compilado y listo para revisar." : "Todavía faltan datos.", "Completion", target: result.SourceId);
            return result;
        });
        // Uses the existing local executor. Preparation alone never executes or saves a final prompt.
        api.MapPost("/nova/drafts/{id:guid}/execute", async (Guid id, ExecutePreparedInput input, IPreparedDraftStore drafts, ILocalLlmClient client, IAgentRunStore runs, HttpContext context, LiveRuns live, PromptLibrary library) =>
        {
            var draft = drafts.Get(id) ?? throw new MissingPromptException();
            if (!input.Confirmed || input.OperationId == Guid.Empty || draft.State != PreparationState.READY || string.IsNullOrWhiteSpace(draft.CompiledText) || draft.CompiledText != input.ExpectedText || !OllamaClientModels(input.Model)) throw new ValidationException("Revisa y confirma el prompt final antes de ejecutar.");
            if (draft.SourceId is { } guardedSourceId) {
                var currentSource = library.Get(guardedSourceId);
                if (currentSource.IsArchived || currentSource.Revision != draft.SourceRevision) throw new ConflictException("El origen cambió. Prepara una nueva ejecución con la versión vigente.");
            }
            Guid? parentRun = Guid.TryParse(context.Request.Headers["X-Nova-Live-Run"], out var linked) ? linked : null;
            if (parentRun is { } parent)
            {
                var authority = live.Active(parent); var parentState = live.Get(parent);
                if (LiveRuns.Terminal(parentState.Status) || parentState.Mode is not ("BROWSER" or "SAME_TAB") || authority.Semantic?.Executes != true || input.Model != "qwen3:8b"
                    || draft.SourceId != parentState.TargetId || draft.SourceRevision != parentState.Revision || draft.CompiledText != authority.Observation?.CompiledText)
                    throw new ValidationException("La ejecución no corresponde al prompt y a los permisos del run visible.");
                if (draft.Values.Any(value => value.Value.Contains(authority.Semantic.ExecutionEvidence, StringComparison.Ordinal))) throw new ValidationException("Los valores del prompt no autorizan una ejecución.");
            }
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, input.Model, input.ExpectedText })));
            var executionId = input.OperationId ?? new Guid(hash.AsSpan(0, 16));
            var run = new AgentCheckpoint(executionId, draft.SessionId ?? draft.Id, Convert.ToHexString(hash), AgentRunState.EXECUTE_TOOLS,
                [AgentIntent.EXECUTE], draft.SourceId is { } sourceId ? [sourceId] : [], draft.SourceId is { } source ? new() { [source] = draft.SourceRevision!.Value } : [], [], 1,
                "EXPLICIT_REVIEWED_EXECUTION", null, draft.Id, input.Model, AgentOrchestrator.ContractVersion, DateTime.UtcNow, AgentIntent.EXECUTE, true);
            if (!runs.TryBegin(run)) throw new ConflictException("Esta ejecución ya se inició. No se repetirá al reintentar.");
            context.Response.ContentType = "text/event-stream";
            async Task Send(string name, object data) { await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted); }
            try
            {
                if (parentRun is { } visibleRun) live.Emit(visibleRun, LiveStatus.INTERPRETING, LiveActor.RULE_SELECTED, "Consultando Qwen para ejecutar el texto compilado que muestra React.", "LocalExecution", "execute_local_llm", draft.SourceId);
                var compiled = new CompiledPrompt(Guid.NewGuid(), draft.Id, 1, draft.Title, draft.CompiledText!, new Dictionary<string, string>(), DateTime.UtcNow);
                var output = await client.Execute(compiled, input.Model, true, text => Send("progress", new { text }), context.RequestAborted);
                runs.Put(run with { State = AgentRunState.READY, Tools = [new("execute_local_llm", "OK", output.ElapsedMs)], UpdatedAt = DateTime.UtcNow });
                if (parentRun is { } completedRun) live.Emit(completedRun, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Qwen completó una respuesta real; se está presentando en React.", "LocalExecution", "execute_local_llm", draft.SourceId, output.ElapsedMs);
                await Send("complete", new { text = output.Text, revision = 1 });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { runs.Put(run with { State = AgentRunState.CANCELLED, ErrorCode = "CANCELLED", UpdatedAt = DateTime.UtcNow }); }
            catch (Exception error) when (error is ValidationException or LlmUnavailableException) { runs.Put(run with { State = AgentRunState.FAILED, ErrorCode = error.GetType().Name, UpdatedAt = DateTime.UtcNow }); await Send("error", new { message = error.Message }); }
        }).RequireRateLimiting("ai");
    }
    private static bool OllamaClientModels(string model) => model is "qwen3:8b" or "qwen2.5:7b-instruct";
}
