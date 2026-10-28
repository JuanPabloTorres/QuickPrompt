using System.Diagnostics;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Cross-process adapter only. Python owns the provider and schema-repair policy;
// the application still validates every proposed domain action before any effect.
public sealed class AdaptiveCoreModelTransport(AdaptiveCoreOptions options, LocalModelPolicy? policy = null, DevelopmentLabObserver? diagnostics = null) : IValidatedAgentModelTransport
{
    public const string Protocol = "adaptive-inference/1";
    private readonly LocalModelPolicy modelPolicy = policy ?? LocalModelPolicy.Production;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public bool UsesThinking(string model) => options.Thinking && modelPolicy.Require(model).Thinking;
    public void Observe(int stage, string content, string? error) { }
    public void Reject(string? answer) { } // No inference cache exists across these isolated turns.

    // Compatibility codec for the existing internal model port. The wire protocol
    // itself contains no Ollama response types and no QuickPrompt domain entities.
    public static JsonElement CreateRequest(JsonElement packet, string requestId)
    {
        var mode = "text"; JsonElement? schema = null;
        if (packet.TryGetProperty("tools", out var tools)) {
            var choices = tools.EnumerateArray().Select(tool => {
                var function = tool.GetProperty("function");
                var shape = ContractSchema.Object(new() {
                    ["toolName"] = ContractSchema.Text(100, [function.GetProperty("name").GetString()!]),
                    ["arguments"] = function.GetProperty("parameters")
                }, "toolName", "arguments");
                var described = System.Text.Json.Nodes.JsonNode.Parse(shape.GetRawText())!;
                if (function.TryGetProperty("description", out var description)) described["description"] = description.GetString();
                return JsonSerializer.SerializeToElement(described);
            }).ToArray();
            if (choices.Length == 0) throw new ValidationException("El catálogo de capacidades está vacío.");
            schema = JsonSerializer.SerializeToElement(new { type = "object", anyOf = choices }); mode = "tools";
        } else if (packet.TryGetProperty("format", out var format) && format.ValueKind != JsonValueKind.Null) {
            schema = format.ValueKind == JsonValueKind.Object ? format : JsonSerializer.SerializeToElement(new { type = "object" });
            mode = "structured";
        }
        var config = packet.TryGetProperty("options", out var settings) ? settings : default;
        int Int(string key, int fallback) => config.ValueKind == JsonValueKind.Object && config.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;
        double temperature = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("temperature", out var temp) ? temp.GetDouble() : 0;
        double? Number(string key) => config.ValueKind == JsonValueKind.Object && config.TryGetProperty(key, out var value) ? value.GetDouble() : null;
        var request = JsonSerializer.SerializeToElement(new {
            version = Protocol, requestId, model = packet.GetProperty("model").GetString(), mode,
            messages = packet.GetProperty("messages"), outputSchema = schema,
            limits = new { requests = 2, timeoutSeconds = 90, contextTokens = Int("num_ctx", 12288),
                outputTokens = Int("num_predict", 2600), temperature,
                thinking = packet.TryGetProperty("think", out var thinking) && thinking.GetBoolean(),
                topP = Number("top_p"), topK = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("top_k", out var topK) ? (int?)topK.GetInt32() : null, minP = Number("min_p") }
        }, Json);
        if (packet.TryGetProperty("decoding", out var decoding)) {
            if (schema is null || decoding.ValueKind != JsonValueKind.String || decoding.GetString() is not ("native" or "prompted"))
                throw new ValidationException("Modo de salida estructurada no compatible.");
            var data = System.Text.Json.Nodes.JsonNode.Parse(request.GetRawText())!;
            data["decoding"] = decoding.GetString();
            request = JsonSerializer.SerializeToElement(data, Json);
        }
        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("seed",out var seed)) {
            var data=System.Text.Json.Nodes.JsonNode.Parse(request.GetRawText())!;
            data["limits"]!["seed"]=seed.GetInt32();
            return JsonSerializer.SerializeToElement(data,Json);
        }
        return request;
    }

    public async Task<JsonDocument> Send(object body, CancellationToken ct, string purpose)
    {
        ct.ThrowIfCancellationRequested();
        try { options.Validate(); }
        catch (InvalidOperationException) { throw new PreparationToolException("CORE_NOT_CONFIGURED", purpose, "Configura Python, worker y SDK de Adaptive Core. La biblioteca manual sigue disponible."); }
        var packet = QwenThinkingSampling.Apply(JsonSerializer.SerializeToElement(body, Json), options.QwenThinkingSamplingEnabled);
        modelPolicy.Require(packet.GetProperty("model").GetString()!);
        var requestId = Guid.NewGuid().ToString("N");
        var request = CreateRequest(packet, requestId);
        var serialized = request.GetRawText();
        if (Encoding.UTF8.GetByteCount(serialized) > 128000) throw new ValidationException("La petición excede el contrato de Core; no se truncará.");
        diagnostics?.Inference(NovaTelemetry.RunId,purpose,request);
        var start = new ProcessStartInfo(options.Python) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var argument in new[] { "-u", "-B", options.Worker, "--sdk-root", options.SdkRoot, "--endpoint", options.Endpoint }) start.ArgumentList.Add(argument);
        start.Environment["PYDANTIC_AI_NO_BANNER"] = "1";
        diagnostics?.ConfigureWorker(start,NovaTelemetry.RunId,requestId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(190));
        using var measurement = NovaTelemetry.Llm(packet, purpose);
        using var worker = Process.Start(start) ?? throw new LlmUnavailableException("No se pudo iniciar Adaptive Core.");
        try {
            var output = ReadBounded(worker.StandardOutput, deadline.Token);
            var errors = ReadBounded(worker.StandardError, deadline.Token);
            await worker.StandardInput.WriteAsync(serialized.AsMemory(), deadline.Token); worker.StandardInput.Close();
            await worker.WaitForExitAsync(deadline.Token);
            var content = await output; await errors;
            using var reply = JsonDocument.Parse(content);
            if (reply.RootElement.TryGetProperty("usage", out var usage) && usage.TryGetProperty("requests", out var count))
                for (var i = 0; i < count.GetInt32() && i < 2; i++) measurement.Requested();
            var result = Decode(reply.RootElement, request, worker.ExitCode);
            measurement.Complete(result.RootElement);
            return result;
        } catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) {
            throw new PreparationToolException("CORE_INVALID_PROTOCOL", purpose, "Core no entregó una respuesta correlacionada válida; no se ejecutaron efectos.");
        } finally {
            if (!worker.HasExited) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(CancellationToken.None); }
        }
    }

    public static JsonDocument Decode(JsonElement reply, JsonElement request, int exitCode)
    {
        if (reply.GetProperty("version").GetString() != Protocol
            || reply.GetProperty("requestId").GetString() != request.GetProperty("requestId").GetString())
            throw new PreparationToolException("CORE_INVALID_PROTOCOL", "", "La respuesta de Core no corresponde a esta petición.");
        if (exitCode != 0 || reply.GetProperty("status").GetString() != "proposed") {
            var error = reply.TryGetProperty("error", out var detail) ? detail.GetString() : null;
            if (error == "MODEL_UNAVAILABLE") throw new LlmUnavailableException("Core no pudo conectar con el modelo local. La biblioteca sigue disponible.");
            if (error == "DEPENDENCY_MISSING") throw new PreparationToolException("CORE_DEPENDENCY_MISSING", "", "Configura el Python de Adaptive Core con el extra inference instalado.");
            throw new PreparationToolException("CORE_INTERPRETATION_FAILED", "", "Core no pudo obtener una propuesta válida; no se ejecutaron efectos.");
        }
        var usage = reply.GetProperty("usage"); var calls = usage.GetProperty("requests").GetInt32();
        if (calls is < 1 or > 2) throw new PreparationToolException("CORE_INVALID_PROTOCOL", "", "Core excedió el presupuesto de inferencia.");
        var value = reply.GetProperty("output");
        object message = request.GetProperty("mode").GetString() == "tools"
            ? new { content = "", tool_calls = new[] { new { function = new { name = value.GetProperty("toolName").GetString(), arguments = value.GetProperty("arguments") } } } }
            : new { content = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText() };
        return JsonDocument.Parse(JsonSerializer.Serialize(new { done = true, done_reason = "stop", message,
            model = request.GetProperty("model").GetString(), coreRequestId = request.GetProperty("requestId").GetString(), modelCalls = calls,
            prompt_eval_count = usage.GetProperty("inputTokens").GetInt32(), eval_count = usage.GetProperty("outputTokens").GetInt32() }, Json));
    }

    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken ct)
    {
        var value = new StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) != 0) {
            if (value.Length + read > 512000) throw new ValidationException("La respuesta de Core excede el límite permitido.");
            value.Append(buffer, 0, read);
        }
        return value.ToString();
    }
}
