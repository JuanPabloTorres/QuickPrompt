using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled transport verifies stage ordering, not language understanding.
public sealed class SourceConstraintStageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentConstraintsWaitForTheirActualSourceWithoutDroppingTheSaveGoal(bool bodyLoaded)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        const string request = "Adapta esta instrucción para principiantes. Conserva «No inventes datos». Solicita revisión para guardar la versión.";
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with { Selected = new(id, 2, "Guía", bodyLoaded ? "Explica <tema>. No inventes datos." : "", false), ExplicitTargetId = id };
        var pending = inference.Interpret(request, context, "qwen3:8b", timeout.Token);
        var constraintCalls = 0; var scopeCalls = 0;
        try {
            for (var step = 0; step < 12 && !pending.IsCompleted; step++) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format");
                var properties = schema.TryGetProperty("properties", out var fields) ? fields : schema.GetProperty("anyOf").EnumerateArray().Last().GetProperty("properties");
                object reply;
                if (properties.TryGetProperty("userProhibitions", out _)) {
                    constraintCalls++;
                    reply = new SemanticConstraintInput([], false, null, null, ["No inventes datos"], [], false, "", "");
                } else if (properties.TryGetProperty("clauses", out _)) {
                    scopeCalls++;
                    using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.Equal(context.Selected.Text, data.RootElement.GetProperty("templateBeforeEdit").GetString());
                    reply = new { clauses = new[] { new { index = 0, scope = "FUTURE_TASK_INVARIANT" } } };
                } else if (properties.TryGetProperty("requestType", out _))
                    reply = new { requestType = "REVISION_REQUEST", authoringRequestEvidence = properties.GetProperty("authoringRequestEvidence").GetProperty("enum")[0].GetString(), instructionTask = "Adaptar para principiantes.", question = "", explanation = "Adaptar.", continuesPendingTask = false, inputApplication = new { action = "NOT_REQUESTED", evidence = "" } };
                else if (schema.GetRawText().Contains("\"requestedOperations\"")) reply = new { requestedOperations = new[] {new {operation="PROPOSE_ADAPTATION",supportingPassage=0}} };
                else if (properties.TryGetProperty("additionalArtifactRequested", out _)) reply = new { additionalArtifactRequested = false };
                else if (properties.TryGetProperty("access", out _)) reply = new { access = "NONE", control = "NONE", save = "UPDATE_CURRENT_RECORD", explanation = "Revisión de versión." };
                else if (properties.TryGetProperty("action", out _)) reply = new { action = "NONE", providerId = (string?)null, executionEvidence = "" };
                else throw new InvalidOperationException("Unexpected transport schema: " + properties.GetRawText());
                var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new { content = JsonSerializer.Serialize(reply, json) } }, json));
                exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            var plan = await pending;
            Assert.True(plan.Adapts); Assert.True(plan.Has(SemanticOperation.REQUEST_SAVE_VERSION)); Assert.False(plan.Constraints.PreserveOriginal);
            Assert.Equal(bodyLoaded ? 1 : 0, constraintCalls);
            Assert.Equal(bodyLoaded ? 1 : 0, scopeCalls);
            Assert.Equal(!bodyLoaded, plan.ValidationNotes?.Contains("CONTENT_CONSTRAINTS_REQUIRE_SOURCE") == true);
            if (bodyLoaded) Assert.Equal(new[] { "No inventes datos" }, plan.Constraints.LiteralClauses);
        } finally { timeout.Cancel(); listener.Stop(); }
    }
}
