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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BatchedCallsRequireOneBoundedModelReselectionWithoutGrantingUnavailableTools(bool unavailable, bool invalidRetry)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
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
            await Reply(first, new { tool_calls = new[] {
                new { function = new { name = "finish", arguments = new { } } },
                new { function = new { name = unavailable ? "UNAVAILABLE_TOOL" : "ui_read_visible_state", arguments = new { } } }
            } });
            if (unavailable) {
                await Assert.ThrowsAsync<ValidationException>(() => pending);
                Assert.Empty(events); return;
            }
            var retry = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var body = await JsonDocument.ParseAsync(retry.Request.InputStream);
            var packet = body.RootElement;
            Assert.False(packet.TryGetProperty("tools", out _));
            Assert.True(packet.GetProperty("format").TryGetProperty("anyOf", out _));
            Assert.Contains("NONE was executed", packet.GetProperty("messages")[0].GetProperty("content").GetString());
            await Reply(retry, new { content = JsonSerializer.Serialize(new { toolName = invalidRetry ? "UNAVAILABLE_TOOL" : "ui_read_visible_state", arguments = new { } }) });
            if (invalidRetry) await Assert.ThrowsAsync<ValidationException>(() => pending);
            else {
                var result = await pending;
                Assert.Equal("ui_read_visible_state", result.ToolName); // The batch's first call was not chosen implicitly.
                Assert.Equal("json_schema_retry", result.SelectionFormat);
            }
            Assert.Equal(new[] { "MULTIPLE_ACTIONS" }, events);
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
}
