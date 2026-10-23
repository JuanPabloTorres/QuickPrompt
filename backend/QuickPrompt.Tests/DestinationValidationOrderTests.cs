using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class DestinationValidationOrderTests
{
    [Theory]
    [InlineData("PREPARE", null, false, false)]
    [InlineData("EXECUTE_NOW", "claude", false, false)]
    [InlineData("PREPARE", null, true, false)]
    [InlineData("EXECUTE_NOW", "claude", true, false)]
    [InlineData("PREPARE", "claude", true, true)]
    [InlineData("EXECUTE_NOW", "ollama", true, true)]
    public async Task ReviewRequestedUseBeforeResolvingItsProviderCapability(string action, string? provider, bool requested, bool supported)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var id = Guid.NewGuid();
        var message = requested ? "Usa la instrucción con la IA que he solicitado." : "Consulta la instrucción sin ejecutar ni preparar ninguna IA.";
        var context = SemanticContext.Empty with { Selected = new(id, 1, "Ficha", "Tarea sintética.", false, InputNames: []), ExplicitTargetId = id };
        var pending = inference.Interpret(message, context, "qwen3:8b", timeout.Token);
        var reviews = 0; var destinations = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        try {
            var unresolved = requested && provider is null;
            var exchanges = requested ? supported ? 6 : 3 : 5;
            for (var index = 0; index < exchanges; index++) {
                var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format").GetRawText(); object reply;
                if (packet.RootElement.GetProperty("format").TryGetProperty("properties", out var properties) && properties.TryGetProperty("action", out _)) {
                    destinations++;
                    Assert.Equal(reviews + 1, destinations);
                    Assert.Equal(0, index % 2);
                    reply = new { action, providerId = provider, executionEvidence = action == "EXECUTE_NOW" ? message : "" };
                } else if (schema.Contains("\"targetRole\"")) {
                    reviews++;
                    using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.Contains(message, data.RootElement.GetProperty("userPassages").GetRawText());
                    Assert.Equal(provider is null ? 0 : 1, data.RootElement.GetProperty("destination").GetArrayLength());
                    reply = new { stance = requested ? "REQUESTED" : "NOT_REQUESTED", supportingPassage = requested ? 0 : -1,
                        targetRole = requested ? "AI_DESTINATION" : "UNSPECIFIED" };
                } else if (schema.Contains("\"targets\"")) {
                    Assert.True(requested); Assert.Equal(1, reviews);
                    reply = new { targets = new[] { new { id = unresolved ? "UNRESOLVED_AI_RECIPIENT" : provider!, quote = message } } };
                } else if (schema.Contains("\"requestType\"")) reply = new { requestType = "NO_CONTENT_REQUEST", authoringRequestEvidence = "", instructionTask = "",
                    question = "", explanation = "Usar la ficha existente.", continuesPendingTask = false };
                else if (schema.Contains("\"targetObject\"")) reply = new { targetObject = "INPUT_VALUES", stance = "NOT_REQUESTED", supportingPassage = -1 };
                else if (schema.Contains("\"access\"")) reply = new { access = "VIEW_UNCHANGED", control = "NONE", save = "NONE", explanation = "Usar la ficha." };
                else throw new InvalidOperationException("Unexpected model contract: " + schema);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new { content = JsonSerializer.Serialize(reply, json) } }));
                exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            Assert.Equal(1, reviews);
            Assert.Equal(reviews, destinations);
            if (unresolved) Assert.Equal("UNRESOLVED_DESTINATION", (await Assert.ThrowsAsync<PreparationToolException>(() => pending)).Code);
            else if (requested && !supported) await Assert.ThrowsAsync<ValidationException>(() => pending);
            else {
                var plan = await pending;
                Assert.True(plan.Has(SemanticOperation.OPEN_PROMPT));
                Assert.Equal(requested && action == "EXECUTE_NOW", plan.Executes);
                Assert.Equal(requested ? provider : null, plan.ProviderId);
                Assert.Equal(requested && action == "PREPARE", plan.Has(SemanticOperation.PREPARE_PROVIDER));
                Assert.False(plan.Adapts); Assert.False(plan.Creates);
                Assert.False(plan.Has(SemanticOperation.REQUEST_SAVE_VERSION)); Assert.False(plan.Has(SemanticOperation.REQUEST_SAVE_COPY));
            }
        } finally { timeout.Cancel(); listener.Stop(); try { await pending; } catch (Exception) { } }
    }
}
