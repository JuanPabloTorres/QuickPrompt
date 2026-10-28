using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptContentInterpreterTests
{
    [Fact] public async Task ContentPortKeepsLiteralConstraintsAndCannotGrantPersistenceOrWorkflow()
    {
        var transport = new Transport(new([], false, null, null, [], [], true, "", ""));
        var result = await new PromptContentInterpreter(transport).ExtractContent("Acorta la instrucción.", SemanticContext.Empty, "qwen3:8b", default);
        Assert.True(result.Shortening); Assert.True(result.PreserveOriginal);
        Assert.Equal(1, transport.Calls);
        Assert.False(transport.Packet.GetProperty("format").GetProperty("properties").TryGetProperty("effects", out _));
    }

    [Fact] public async Task InventedSemanticEvidenceFailsWithoutASecondDomainInferenceLoop()
    {
        var transport = new Transport(new([], true, "fr", null, [], [], false, "invented quote", ""));
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => new PromptContentInterpreter(transport)
            .ExtractContent("Acorta la instrucción.", SemanticContext.Empty, "qwen3:8b", default));
        Assert.Equal("UNGROUNDED_OUTPUT_CONSTRAINT", error.Code); Assert.Equal(1, transport.Calls);
    }

    [Fact] public async Task CancellationPreventsAnyModelRequest()
    {
        var transport = new Transport(new([], true, null, null, [], [], false, "", ""));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PromptContentInterpreter(transport)
            .ExtractContent("Acorta la instrucción.", SemanticContext.Empty, "qwen3:8b", cancelled.Token));
        Assert.Equal(0, transport.Calls);
    }

    private sealed class Transport(SemanticConstraintInput answer) : IValidatedAgentModelTransport
    {
        public int Calls; public JsonElement Packet;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Packet = JsonSerializer.SerializeToElement(packet);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = JsonSerializer.Serialize(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } })));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) => throw new InvalidOperationException("The content port owns no model cache.");
    }
}
