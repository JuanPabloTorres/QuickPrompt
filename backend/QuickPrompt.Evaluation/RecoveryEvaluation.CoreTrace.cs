using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;

public static partial class RecoveryEvaluation
{
    // No subscription in production. This fixture-only adapter does not change
    // prompts, model results, retries, permissions or acceptance expectations.
    private sealed class ObservedCoreTransport(IValidatedAgentModelTransport inner, AdaptiveCoreOptions options) : IValidatedAgentModelTransport
    {
        public string CaseId { get; set; } = "";
        public Action<int, string, string?>? Diagnostic { get; set; }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string content, string? error) => inner.Observe(stage, content, error);
        public void Reject(string? answer) => inner.Reject(answer);
        public async Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            var runId = NovaTelemetry.RunId ?? Guid.Empty;
            {
                var value = QwenThinkingSampling.Apply(JsonSerializer.SerializeToElement(packet), options.QwenThinkingSamplingEnabled);
                LabEvent(CaseId, runId, "inference", "RUNNING", new { purpose,
                    model=value.GetProperty("model"),thinking=value.GetProperty("think"),settings=value.GetProperty("options") }, kind: "inference_started");
                if (Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_PUBLIC_INPUTS") == "1")
                    LabEvent(CaseId, runId, "input", "OBSERVED", new { purpose }, kind: "fixture_input",
                        inputs: new { messages = value.GetProperty("messages") });
            }
            var reply = await inner.Send(packet, ct, purpose);
            {
                var content = reply.RootElement.GetProperty("message").GetProperty("content").GetString()!;
                if (purpose == "CoreActionRequest") Diagnostic?.Invoke(reply.RootElement.GetProperty("modelCalls").GetInt32(), content, null);
                LabEvent(CaseId, runId, "inference", "OBSERVED", new { purpose,
                    modelCalls = reply.RootElement.GetProperty("modelCalls").GetInt32(),
                    output = Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_PUBLIC_INPUTS") == "1" ? JsonSerializer.Deserialize<JsonElement?>(content) : null },
                    kind: "inference_finished");
            }
            return reply;
        }
    }
}
