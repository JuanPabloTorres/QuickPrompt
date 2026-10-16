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
        api.MapPost("/nova/interpret", (NovaRequest input, NovaAssistant assistant, HttpContext context) => assistant.Interpret(input, context.TraceIdentifier, context.RequestAborted)).RequireRateLimiting("ai");
        api.MapPost("/nova/propose", (ProposalInput input, NovaAssistant assistant, CancellationToken ct) => assistant.Propose(input, ct)).RequireRateLimiting("ai");
        api.MapPost("/nova/variables", (NovaRequest input, NovaAssistant assistant, CancellationToken ct) => assistant.Suggest(input, ct)).RequireRateLimiting("ai");
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
}
