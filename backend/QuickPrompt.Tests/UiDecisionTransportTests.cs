using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class UiDecisionTransportTests
{
    [Theory]
    [InlineData("batch", false)]
    [InlineData("batch", true)]
    [InlineData("unknown", false)]
    [InlineData("unknown", true)]
    [InlineData("invalidArguments", false)]
    [InlineData("invalidArguments", true)]
    public async Task InvalidProposalsFailClosedWithoutADotNetInferenceRepairLoop(string invalid, bool independent)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var transport = new OllamaAgentModelTransport($"http://127.0.0.1:{port}");
        using var legacy = independent ? null : new PreparationInference($"http://127.0.0.1:{port}", modelTransport: transport);
        IUiDecisionModel inference = (IUiDecisionModel?)legacy ?? new UiDecisionModel(transport);
        var events = new List<string>();
        var tools = UiToolCatalog.All.Where(tool => tool.Name is "finish" or "ui_read_visible_state").ToArray();
        var state = new UiVisibleState("focus", [], Guid.NewGuid(), 1, "Ejemplo", "Instrucción", [], [], false, true, "Instrucción", null, false, false);
        var pending = inference.Decide(new("Revisa el prompt.", state, [], tools, state.PromptId, null, ModelEvent: events.Add), cancellation.Token);
        async Task Reply(HttpListenerContext request, object message) {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message }));
            request.Response.ContentType = "application/json"; request.Response.ContentLength64 = bytes.Length;
            await request.Response.OutputStream.WriteAsync(bytes); request.Response.Close();
        }
        try {
            var first = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var function = new { function = new { name = invalid == "unknown" ? "UNAVAILABLE_TOOL" : "finish", arguments = invalid == "invalidArguments" ? (object)new { invented = true } : new { } } };
            await Reply(first, new { tool_calls = invalid == "batch" ? new[] { function, function } : new[] { function } });
            await Assert.ThrowsAsync<ValidationException>(() => pending);
            Assert.Empty(events); // Structural repair belongs to Core, not the domain selector.
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
}
