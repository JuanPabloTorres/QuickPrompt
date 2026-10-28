using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled HTTP transport, not model comprehension or end-to-end success.
public sealed class InferenceTransportRecoveryTests
{
    [Theory]
    [InlineData(503, false, 2)]
    [InlineData(503, true, 2)]
    [InlineData(429, false, 2)]
    [InlineData(400, true, 1)]
    public async Task PureInferenceRetriesOnlyTransientFailuresOnce(int status, bool persistent, int expectedCalls)
    {
        await using var server = new Server(status, persistent);
        using var inference = new PreparationInference(server.Origin);
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Guid.NewGuid(); using var trace = NovaTelemetry.Begin(run);
        if (persistent) {
            var error = await Assert.ThrowsAsync<PreparationToolException>(() => inference.Decide(Context(), cancel.Token));
            Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        } else Assert.Equal("finish", (await inference.Decide(Context(), cancel.Token)).ToolName);
        Assert.Equal(expectedCalls, server.Packets.Count);
        Assert.All(server.Packets, packet => Assert.Equal(server.Packets[0], packet));
        Assert.Equal(expectedCalls, NovaTelemetry.Snapshot(run)!.LlmCalls);
    }
    [Fact] public async Task CancellationPreventsTheSecondRequest()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new Server(503, true, cancel.Cancel);
        using var inference = new PreparationInference(server.Origin);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inference.Decide(Context(), cancel.Token));
        Assert.Single(server.Packets);
    }
    private static UiDecisionContext Context() => new("Solicitud sintética.",
        new("focus", [], Guid.NewGuid(), 1, "Ficha", "Explica un tema.", [], [], false, true, "Explica un tema.", null, false, false),
        [], UiToolCatalog.All.Where(tool => tool.Name == "finish").ToArray(), null, null);
    private sealed class Server : IAsyncDisposable
    {
        private readonly HttpListener listener = new(); private readonly Task serving;
        public string Origin { get; }
        public List<string> Packets { get; } = [];
        public Server(int status, bool persistent, Action? first = null)
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            Origin = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Origin); listener.Start();
            serving = Serve();
            async Task Serve() {
                try {
                    while (listener.IsListening) {
                        var exchange = await listener.GetContextAsync();
                        using var reader = new StreamReader(exchange.Request.InputStream);
                        Packets.Add(await reader.ReadToEndAsync());
                        exchange.Response.StatusCode = persistent || Packets.Count == 1 ? status : 200;
                        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { tool_calls = new[] { new { function = new { name = "finish", arguments = new { } } } } } });
                        await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
                        if (Packets.Count == 1) first?.Invoke();
                    }
                } catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or IOException) { }
            }
        }
        public async ValueTask DisposeAsync() { listener.Stop(); await serving; listener.Close(); }
    }
}
