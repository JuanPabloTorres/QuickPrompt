using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CapabilityKnowledgeTransportTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task KnowledgeReachesModelAsReferenceWithoutGrantingDestinationAuthority(bool enabled)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var knowledge = new SyntheticKnowledge(); using var inference = new PreparationInference($"http://127.0.0.1:{port}", capabilityKnowledge: enabled ? knowledge : null);
        var id = Guid.NewGuid(); var source = new SemanticEntity(id, 1, "Fuente sintética", "Explica <tema>.", false, InputNames: ["tema"]);
        var context = SemanticContext.Empty with { Selected = source, ExplicitTargetId = id };
        var pending = inference.Interpret("Completa los campos del formulario.", context, "qwen3:8b", cancellation.Token);
        try
        {
            for (var attempt = 0; attempt < 1; attempt++)
            {
                var received = await listener.GetContextAsync().WaitAsync(cancellation.Token);
                using var body = await JsonDocument.ParseAsync(received.Request.InputStream, cancellationToken: cancellation.Token);
                using var packet = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal("Completa los campos del formulario.", packet.RootElement.GetProperty("currentUserDirective").GetString());
                Assert.Equal(enabled, packet.RootElement.TryGetProperty("systemCapabilities", out var reference));
                if (enabled) { Assert.Contains("neither a user request nor permission", reference.GetProperty("authority").GetString()); Assert.Equal("operation:EXECUTE_LOCAL", reference.GetProperty("nodes")[0].GetProperty("id").GetString()); }
                // A retrieved execution descriptor cannot legalize a destination
                // without its actual provider or authorize a user execution request.
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new { content = "{\"action\":\"EXECUTE_NOW\",\"providerId\":null,\"executionEvidence\":\"\"}" } }));
                received.Response.ContentType = "application/json"; await received.Response.OutputStream.WriteAsync(bytes, cancellation.Token); received.Response.Close();
                // Scope is now reviewed before converting the destination to a
                // capability. Even a positive semantic review cannot supply the
                // missing provider or turn retrieved knowledge into authority.
                var review = await listener.GetContextAsync().WaitAsync(cancellation.Token);
                using var reviewBody = await JsonDocument.ParseAsync(review.Request.InputStream, cancellationToken: cancellation.Token);
                Assert.True(reviewBody.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("targetRole", out _));
                using var reviewData = JsonDocument.Parse(reviewBody.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(0, reviewData.RootElement.GetProperty("destination").GetArrayLength());
                Assert.False(reviewData.RootElement.TryGetProperty("systemCapabilities", out _));
                var reviewBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new {
                    content = "{\"stance\":\"REQUESTED\",\"supportingPassage\":0,\"targetRole\":\"AI_DESTINATION\"}" } }));
                review.Response.ContentType = "application/json"; await review.Response.OutputStream.WriteAsync(reviewBytes, cancellation.Token); review.Response.Close();
                var repair = await listener.GetContextAsync().WaitAsync(cancellation.Token);
                using var repairBody = await JsonDocument.ParseAsync(repair.Request.InputStream, cancellationToken: cancellation.Token);
                Assert.True(repairBody.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("targets", out _));
                using var repairData = JsonDocument.Parse(repairBody.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.False(repairData.RootElement.TryGetProperty("systemCapabilities", out _));
                var repairBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new {
                    content = "{\"targets\":[{\"id\":\"UNRESOLVED_AI_RECIPIENT\",\"quote\":\"Completa los campos del formulario.\"}]}" } }));
                repair.Response.ContentType = "application/json"; await repair.Response.OutputStream.WriteAsync(repairBytes, cancellation.Token); repair.Response.Close();
            }
            Assert.Equal("UNRESOLVED_DESTINATION", (await Assert.ThrowsAsync<PreparationToolException>(() => pending)).Code);
            Assert.Equal(enabled ? 1 : 0, knowledge.Calls);
        }
        finally { cancellation.Cancel(); listener.Stop(); }
    }
    private sealed class SyntheticKnowledge : ICapabilityKnowledge
    {
        public int Calls { get; private set; }
        public Task<CapabilityKnowledgeContext> Retrieve(string directive, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new CapabilityKnowledgeContext("READY", "synthetic", "synthetic-digest", [new("operation:EXECUTE_LOCAL", "OPERATION", "Execute with explicit user authority only.")], []));
        }
    }
}
