using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class TransportEditingCompositionTests
{
    [Fact]
    public async Task ExplicitTransportExecutesTheReviewedContentAndDoesNotOwnItsLifetime()
    {
        var transport = new Borrowed(); using var client = OllamaClient.ForTransport(transport);
        var compiled = new CompiledPrompt(Guid.NewGuid(), Guid.NewGuid(), 1, "Prueba", "Contenido revisado", new Dictionary<string, string>(), DateTime.UtcNow);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => client.Available(default));
        Assert.Equal(0, transport.Calls);
        var seen = new List<string>();
        var result = await client.Execute(compiled, "qwen3:8b", true, text => { seen.Add(text); return Task.CompletedTask; }, default);
        Assert.Equal("Resultado completo", result.Text); Assert.Equal(new[] { result.Text }, seen);
        Assert.False(transport.Last!.Value.GetProperty("stream").GetBoolean());
        Assert.Contains("Contenido revisado", transport.Last!.Value.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString());
        client.Dispose(); Assert.False(transport.Disposed);
    }

    [Fact]
    public async Task TransportFailureAndCancellationDoNotFallBackOrExecuteUnconfirmedContent()
    {
        var transport = new Borrowed { Fail = true }; using var client = OllamaClient.ForTransport(transport);
        var compiled = new CompiledPrompt(Guid.NewGuid(), Guid.NewGuid(), 1, "Prueba", "Contenido", new Dictionary<string, string>(), DateTime.UtcNow);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => client.Execute(compiled, "qwen3:8b", true, null, default));
        Assert.Equal(1, transport.Calls);
        await Assert.ThrowsAsync<ValidationException>(() => client.Execute(compiled, "qwen3:8b", false, null, default));
        Assert.Equal(1, transport.Calls);
        using var stop = new CancellationTokenSource(); stop.Cancel(); transport.Fail = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Execute(compiled, "qwen3:8b", true, null, stop.Token));
        Assert.Equal(1, transport.Calls);
    }

    private sealed class Borrowed : IAgentModelTransport, IDisposable
    {
        public bool Fail; public int Calls; public bool Disposed; public JsonElement? Last;
        public bool UsesThinking(string model) => false;
        public void Observe(int status, string message, string? detail = null) { }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Last = JsonSerializer.SerializeToElement(packet);
            LocalProposalProtocol.ValidateRequest(Last.Value);
            if (Fail) throw new LlmUnavailableException("Synthetic provider unavailable; no fallback.");
            return Task.FromResult(JsonDocument.Parse("{\"message\":{\"content\":\"Resultado completo\"}}"));
        }
        public void Dispose() => Disposed = true;
    }
}
