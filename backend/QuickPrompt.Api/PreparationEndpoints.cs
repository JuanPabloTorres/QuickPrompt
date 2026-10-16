using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Api;

public record CompletePreparedInput(Guid OperationId, Dictionary<string, string> Values);
public record ExecutePreparedInput(bool Confirmed, string ExpectedText, string Model = "qwen3:8b");
public static class PreparationEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void MapPreparation(this RouteGroupBuilder api)
    {
        api.MapPost("/nova/prepare", async (PrepareInput input, AgentOrchestrator orchestrator, HttpContext context, ILogger<AgentOrchestrator> logger) =>
        {
            AgentOrchestrator.Validate(input);
            context.Response.ContentType = "text/event-stream";
            async Task Send(string name, object data)
            {
                if (context.RequestAborted.IsCancellationRequested) return;
                await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
            try
            {
                var result = await orchestrator.Prepare(input, state => Send("stage", new PreparationProgress(state)),
                    (tool, outcome, milliseconds) => logger.LogInformation("Preparation {OperationId} tool {Tool} {Outcome} {ElapsedMs}ms", input.OperationId, tool, outcome, Math.Round(milliseconds)), context.RequestAborted);
                await Send("complete", result);
            }
            catch (OperationCanceledException) { await Send("error", new { code = "CANCELLED_OR_TIMEOUT", message = "Preparación detenida o tiempo agotado. Tu trabajo anterior se conserva." }); }
            catch (Exception error) when (error is ValidationException or ConflictException or LlmUnavailableException or PreparationToolException or MissingPromptException)
            { await Send("stage", new PreparationProgress(PreparationState.FAILED)); await Send("error", new { code = error is PreparationToolException tool ? tool.Code : error is LlmUnavailableException ? "MODEL_UNAVAILABLE" : "PREPARATION_REJECTED", message = error.Message }); }
            catch (Exception error)
            {
                logger.LogError("Preparation {OperationId} failed with {ErrorType}", input.OperationId, error.GetType().Name);
                await Send("stage", new PreparationProgress(PreparationState.FAILED)); await Send("error", new { code = "INTERNAL_ERROR", message = "No se pudo completar la preparación. Tu trabajo anterior se conserva." });
            }
        }).RequireRateLimiting("ai");
        api.MapGet("/nova/drafts", (IPreparedDraftStore drafts) => drafts.Recent());
        api.MapGet("/nova/drafts/{id:guid}", (Guid id, IPreparedDraftStore drafts) => drafts.Get(id) ?? throw new MissingPromptException());
        api.MapDelete("/nova/drafts/{id:guid}", (Guid id, IPreparedDraftStore drafts) => { drafts.Forget(id); return Results.NoContent(); });
        api.MapPost("/nova/drafts/{id:guid}/save", (Guid id, SavePreparedInput input, IPreparedDraftStore drafts) =>
        {
            if (!input.Confirmed) throw new ValidationException("Guardar en la biblioteca requiere una acción explícita.");
            return drafts.Save(id);
        });
        api.MapPost("/nova/drafts/{id:guid}/complete", (Guid id, CompletePreparedInput input, IPreparedDraftStore drafts) =>
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
            drafts.Put(result, fingerprint); return result;
        });
        // Uses the existing local executor. Preparation alone never executes or saves a final prompt.
        api.MapPost("/nova/drafts/{id:guid}/execute", async (Guid id, ExecutePreparedInput input, IPreparedDraftStore drafts, ILocalLlmClient client, HttpContext context) =>
        {
            var draft = drafts.Get(id) ?? throw new MissingPromptException();
            if (!input.Confirmed || draft.State != PreparationState.READY || draft.CompiledText != input.ExpectedText || !OllamaClientModels(input.Model)) throw new ValidationException("Revisa y confirma el prompt final antes de ejecutar.");
            context.Response.ContentType = "text/event-stream";
            async Task Send(string name, object data) { await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted); }
            try
            {
                var compiled = new CompiledPrompt(Guid.NewGuid(), draft.Id, 1, draft.Title, draft.CompiledText!, new Dictionary<string, string>(), DateTime.UtcNow);
                var output = await client.Execute(compiled, input.Model, true, text => Send("progress", new { text }), context.RequestAborted);
                await Send("complete", new { text = output.Text, revision = 1 });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception error) when (error is ValidationException or LlmUnavailableException) { await Send("error", new { message = error.Message }); }
        }).RequireRateLimiting("ai");
    }
    private static bool OllamaClientModels(string model) => model is "qwen3:8b" or "qwen2.5:7b-instruct";
}
