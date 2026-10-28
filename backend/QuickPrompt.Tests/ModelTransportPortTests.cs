using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ModelTransportPortTests
{
    [Fact] public async Task InterpreterUsesBorrowedPortWithoutCallingItsEndpointAndPreservesCancellation()
    {
        using var transport = new ControlledTransport();
        using (var interpreter = new PreparationInference("http://127.0.0.1:1", modelTransport: transport)) {
            var context = new UiDecisionContext("Inspección sintética.",
                new("focus", [], Guid.NewGuid(), 1, "Ficha", "Explica un tema.", [], [], false, true, "Explica un tema.", null, false, false),
                [], UiToolCatalog.All.Where(tool => tool.Name == "finish").ToArray(), null, null);
            using var cancellation = new CancellationTokenSource();
            Assert.Equal("finish", (await interpreter.Decide(context, cancellation.Token)).ToolName);
            Assert.Equal(cancellation.Token, transport.Cancellation);
            Assert.Contains("Inspección sintética.", transport.Packet.GetProperty("messages")[1].GetProperty("content").GetString());
            Assert.Equal("qwen3:8b", transport.Packet.GetProperty("model").GetString());
            Assert.Equal(PreparationInference.InferenceContextLength, transport.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        }
        Assert.False(transport.Disposed);
        Assert.Equal(1, transport.Calls);
    }

    private sealed class ControlledTransport : IValidatedAgentModelTransport, IDisposable
    {
        public bool Disposed; public int Calls; public JsonElement Packet; public CancellationToken Cancellation;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Cancellation = ct;
            Packet = JsonSerializer.SerializeToElement(packet);
            return Task.FromResult(JsonDocument.Parse("""{"done":true,"message":{"tool_calls":[{"function":{"name":"finish","arguments":{}}}]}}"""));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
        public void Dispose() => Disposed = true;
    }
}
