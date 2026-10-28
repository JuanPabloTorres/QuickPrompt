using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Pure local inference: no semantic interpretation, database access or UI effects.
public sealed partial class OllamaAgentModelTransport : IValidatedAgentModelTransport, IDisposable
{
    private readonly HttpClient http;
    private readonly Action<InferenceObservation>? observe;
    private readonly bool semanticThinking;
    private readonly bool qwenThinkingSampling;
    private readonly LocalModelPolicy modelPolicy;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public Action<int, string, string?>? SemanticDiagnostic { get; set; }

    public OllamaAgentModelTransport(string endpoint, Action<InferenceObservation>? observe = null,
        bool reuseValidatedInference = false, bool semanticThinking = true, bool qwenThinkingSampling = false,
        LocalModelPolicy? modelPolicy = null)
    {
        this.observe = observe;
        this.semanticThinking = semanticThinking;
        this.qwenThinkingSampling = qwenThinkingSampling;
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        reuseInference = reuseValidatedInference;
        http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = OllamaClient.ValidateEndpoint(endpoint), Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 512000 };
    }

    public bool UsesThinking(string model) => semanticThinking && modelPolicy.Require(model).Thinking;
    public void Observe(int stage, string content, string? error) => SemanticDiagnostic?.Invoke(stage, content, error);
    public async Task<JsonDocument> Send(object body, CancellationToken ct, string purpose)
    {
        ct.ThrowIfCancellationRequested();
        var packet = QwenThinkingSampling.Apply(JsonSerializer.SerializeToElement(body, Json), qwenThinkingSampling);
        modelPolicy.Require(packet.GetProperty("model").GetString()!);
        var identity = await ReuseIdentity(packet, ct);
        ct.ThrowIfCancellationRequested();
        if (identity is { } reuse && Recall(reuse) is { } cached) {
            using var hit = NovaTelemetry.Llm(packet, purpose); hit.CacheHit(); return cached;
        }
        // This adapter performs pure inference only. Domain/UI effects are outside
        // this bounded transport retry and must never be replayed here.
        for (var attempt = 0; attempt < 2; attempt++) {
            ct.ThrowIfCancellationRequested();
            using var measurement = NovaTelemetry.Llm(packet, purpose);
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat") { Content = JsonContent.Create(packet) };
                if (NovaTelemetry.RunId is { } runId) request.Headers.Add("X-Nova-Run-Id", runId.ToString());
                measurement.Requested();
                using var response = await http.SendAsync(request, ct);
                var retryable = (int)response.StatusCode is 408 or 429 or 500 or 502 or 503 or 504;
                NovaTelemetry.DebugPoint("model.http.response", !response.IsSuccessStatusCode, () => new() {
                    ["purpose"] = purpose, ["statusCode"] = (int)response.StatusCode, ["success"] = response.IsSuccessStatusCode,
                    ["attempt"] = attempt + 1, ["retryable"] = retryable
                });
                if (!response.IsSuccessStatusCode) {
                    if (retryable && attempt == 0) { await Task.Delay(250, ct); continue; }
                    throw new LlmUnavailableException("NOVA no pudo usar el modelo local. La biblioteca y el compilador siguen disponibles.");
                }
                var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = result.RootElement;
                measurement.Complete(root);
                observe?.Invoke(new("CHAT_USAGE", root.TryGetProperty("model", out var model) ? model.GetString() : null,
                    root.TryGetProperty("prompt_eval_count", out var promptTokens) ? promptTokens.GetInt32() : null,
                    root.TryGetProperty("eval_count", out var generatedTokens) ? generatedTokens.GetInt32() : null));
                if (!root.TryGetProperty("done", out var done) || !done.GetBoolean()
                    || root.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length")
                { result.Dispose(); throw new ValidationException("El modelo devolvió una propuesta incompleta; no se guardó."); }
                ct.ThrowIfCancellationRequested();
                // Model identity and semantic validators retain the existing cache rules.
                if (identity is not null && await ReuseIdentity(packet, ct) == identity) Remember(identity, packet, result);
                return result;
            }
            catch (HttpRequestException) when (attempt == 0 && !ct.IsCancellationRequested) { await Task.Delay(250, ct); }
            catch (HttpRequestException) { ct.ThrowIfCancellationRequested(); throw new LlmUnavailableException("Ollama no está disponible. Puedes seguir usando tu biblioteca."); }
            catch (JsonException) { throw new ValidationException("El modelo devolvió un contrato inválido."); }
        }
        throw new LlmUnavailableException("Ollama no está disponible. Puedes seguir usando tu biblioteca.");
    }
    public void Dispose() => http.Dispose();
}
