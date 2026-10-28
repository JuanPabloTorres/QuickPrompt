using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class InputScopeTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task FieldContractRepairIsLocalBoundedAndNeverSilentlyDiscardsAQuestion(bool resolved, bool independent = false)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var transport = new OllamaAgentModelTransport($"http://127.0.0.1:{port}");
        using var legacy = independent ? null : new PreparationInference($"http://127.0.0.1:{port}", modelTransport: transport);
        IInputScopeInterpreter inference = (IInputScopeInterpreter?)legacy ?? new PromptInputScopeInterpreter(new PromptInputValueExtractor(transport));
        const string request = "Completa. material: un jardín. Copia.";
        var context = SemanticContext.Empty with { Selected = new(Guid.NewGuid(), 1, "Ficha", "Revisa <material>.", false, InputNames: ["material"]) };
        var pending = inference.ResolveInputScope(request, context, "qwen3:8b", timeout.Token);
        try {
            for (var index = 0; index < 2; index++) {
                var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var messages = packet.RootElement.GetProperty("messages");
                using var data = JsonDocument.Parse(messages[messages.GetArrayLength() - 1].GetProperty("content").GetString()!);
                Assert.Equal(request, data.RootElement.GetProperty("currentUserRequest").GetString());
                Assert.Equal("TEXT", data.RootElement.GetProperty("fieldContracts")[0].GetProperty("valueType").GetString());
                Assert.Equal(index == 1, data.RootElement.GetProperty("validationFeedback").ValueKind == JsonValueKind.String);
                var content = JsonSerializer.Serialize(new { values = new[] { new { name = "material", value = "un jardín", evidence = "un jardín." } }, question = index == 1 && resolved ? "" : "¿Falta algún dato?" });
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new { content } }));
                exchange.Response.ContentType = "application/json"; await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            if (resolved) Assert.Equal("un jardín.", (await pending)!.Values(request, ["material"])["material"]);
            else Assert.Equal("¿Falta algún dato?", (await Assert.ThrowsAsync<ValidationException>(() => pending)).Message);
        } finally { timeout.Cancel(); listener.Stop(); try { await pending; } catch (Exception) { } }
    }
}
