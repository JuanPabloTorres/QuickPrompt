using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Api;

public static class NovaEndpoints
{
    public static void MapNova(this RouteGroupBuilder api)
    {
        api.MapGet("/ai/local/models", async (ILocalLlmClient client, CancellationToken ct) => new { models = await client.Available(ct) });
        api.MapPost("/nova/interpret", (NovaRequest input, NovaAssistant assistant, HttpContext context, LiveRuns live) => Observed(context, live, "Interpretation", ct => assistant.Interpret(input, context.TraceIdentifier, ct))).RequireRateLimiting("ai");
        api.MapPost("/nova/propose", (ProposalInput input, NovaAssistant assistant, HttpContext context, LiveRuns live) => {
            if (Guid.TryParse(context.Request.Headers["X-Nova-Live-Run"], out var parent) && live.Get(parent).Mode is "BROWSER" or "SAME_TAB") {
                var authority = live.Active(parent);
                if (authority.Semantic?.Adapts != true || input.Context != (authority.VisibleTaskRequest ?? authority.AuthorizedTaskRequest ?? authority.Input.Message) || input.PromptId != authority.Observation?.PromptId || input.Revision != authority.Observation?.Revision)
                    throw new ValidationException("La edición no corresponde a la solicitud y al origen del run visible.");
                input = input with { Context = authority.AuthorizedTaskRequest ?? authority.Input.Message, InterpretedShortening = authority.Semantic.Constraints.Shortening, InterpretedContentChange = authority.Semantic.RequestedTransformation, InterpretedTargetTerms = authority.Semantic.Constraints.TargetTerms };
            }
            if (Guid.TryParse(context.Request.Headers["X-Nova-Live-Run"], out var id) && live.Get(id).Mode is "BROWSER" or "SAME_TAB" && live.Active(id).WorkingDraft is { } working) {
                var control = live.Active(id);
                if (control.Semantic?.Adapts != true || input.Context != (control.AuthorizedTaskRequest ?? control.Input.Message) || input.PromptId != working.SourceId || input.Revision != working.SourceRevision
                    || control.Observation?.TemplateText != working.TemplateText) throw new ValidationException("La edición no corresponde al borrador activo y a la petición autorizada.");
                input = input with { AuthorizedWorkingSource = new Template { Id = working.SourceId!.Value, Revision = working.SourceRevision!.Value, Title = working.Title, Description = working.Description, Template = working.TemplateText } };
            }
            return Observed(context, live, "Generation", ct => assistant.Propose(input, ct));
        }).RequireRateLimiting("ai");
        api.MapPost("/nova/variables", (NovaRequest input, NovaAssistant assistant, HttpContext context, LiveRuns live) => Observed(context, live, "Variables", ct => assistant.Suggest(input, ct))).RequireRateLimiting("ai");
        api.MapPost("/ai/local/execute", async (ExecuteInput input, IPromptReader reader, ILocalLlmClient client, HttpContext context) =>
        {
            if (!input.Confirmed || input.Values is null || input.Values.Count > 60 || input.Values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000))
                throw new ValidationException("Confirma el prompt y revisa sus valores antes de ejecutar.");
            var compiled = reader.Preview(input.PromptId, input.Revision, input.Values);
            if (compiled.Text != input.ExpectedText) throw new ConflictException("El texto final cambió. Revisa el prompt antes de ejecutarlo.");
            var ct = context.RequestAborted;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            async Task Send(string name, object data)
            {
                await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web))}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
            try
            {
                var output = await client.Execute(compiled, input.Model, true, text => Send("progress", new { text }), ct);
                await Send("complete", new { text = output.Text, elapsedMs = output.ElapsedMs, revision = compiled.Revision });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* disconnected client cancels Ollama */ }
            catch (Exception error) when (error is ValidationException or LlmUnavailableException)
            { await Send("error", new { message = error.Message, correlationId = context.TraceIdentifier }); }
        }).RequireRateLimiting("ai");
    }
    private static async Task<T> Observed<T>(HttpContext context, LiveRuns live, string stage, Func<CancellationToken, Task<T>> operation)
    {
        if (!Guid.TryParse(context.Request.Headers["X-Nova-Live-Run"], out var id)) return await operation(context.RequestAborted);
        var run = live.Get(id); var control = live.Active(id);
        if (LiveRuns.Terminal(run.Status)) throw new ConflictException("La ejecución ya terminó.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, control.Cancellation.Token);
        await live.Boundary(id, linked.Token); var timer = System.Diagnostics.Stopwatch.StartNew();
        var previousNotice = OllamaClient.OperationNotice.Value;
        OllamaClient.OperationNotice.Value = notice => {
            if (notice == "SOURCE_INVARIANTS_RETAINED") live.Emit(id, LiveStatus.VERIFYING, LiveActor.RULE_SELECTED, "QuickPrompt conservó literalmente las restricciones protegidas del origen en la propuesta.", "SourceInvariants");
            else if (notice == "SOURCE_INPUTS_BOUND") live.Emit(id, LiveStatus.VERIFYING, LiveActor.RULE_SELECTED, "QuickPrompt incorporó los campos reales del origen en una sección de entradas, sin inventar valores.", "SourceInputs");
            else live.Emit(id, LiveStatus.INTERPRETING, LiveActor.RULE_SELECTED, "La comprobación detectó una restricción o longitud incumplida; solicitando una única corrección al modelo.", "ModelRevision");
        };
        live.Emit(id, LiveStatus.INTERPRETING, run.Mode is "BROWSER" or "SAME_TAB" ? LiveActor.RULE_SELECTED : LiveActor.USER_SELECTED, "Consultando Qwen para la operación solicitada en el editor.", stage);
        try
        {
            var result = await operation(linked.Token); linked.Token.ThrowIfCancellationRequested();
            live.Emit(id, run.Mode == "LIVE" ? LiveStatus.COMPLETED : LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Respuesta del modelo recibida y contrato validado; no se guardó el original.", stage, duration: timer.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (OperationCanceledException) { if (run.Mode == "LIVE") live.Emit(id, LiveStatus.CANCELLED, LiveActor.SYSTEM_VALIDATION, "Operación del modelo detenida.", stage, error: "CANCELLED"); throw; }
        catch (Exception error) { if (run.Mode == "LIVE") live.Emit(id, LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION, "El modelo no pudo completar la operación.", stage, error: error is LlmUnavailableException ? "MODEL_UNAVAILABLE" : "MODEL_RESULT_REJECTED"); throw; }
        finally { OllamaClient.OperationNotice.Value = previousNotice; }
    }
}
