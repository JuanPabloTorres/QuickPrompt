using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class GroundedConstraintTransportTests
{
    [Theory]
    [InlineData(true,true)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(false,false)]
    public async Task ContentExtractionCannotReclassifySavePolicyOrConfuseSlotsWithCaseValues(bool reviewVersion,bool appliesValues)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}"); var id = Guid.NewGuid();
        var request = "Cambia el estilo y "+(appliesValues?"úsalo en un caso concreto":"conserva <tema> como entrada")+" y "+(reviewVersion?"pide revisión para guardar una versión":"solo prepara el borrador sin guardarlo");
        var context = SemanticContext.Empty with { Selected = new(id, 1, "Fuente", "Explica <tema>.", false, InputNames: ["tema"]), ExplicitTargetId = id };
        var pending = inference.Interpret(request, context, "qwen3:8b", timeout.Token);
        var constraintSeen = false;var independentInputCalls=0;
        try {
            for (var step = 0; step < 9 && !constraintSeen; step++) {
                var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format"); var contract = schema.GetRawText(); object reply;
                if (contract.Contains("\"userProhibitions\"")) {
                    Assert.Equal(!reviewVersion, schema.GetProperty("properties").GetProperty("preserveOriginal").GetProperty("enum")[0].GetBoolean());
                    using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.Equal(!reviewVersion, data.RootElement.GetProperty("resolvedSavePreservesOriginal").GetBoolean());
                    reply = new SemanticConstraintInput([], !reviewVersion, null, null, [], [], false, "", ""); constraintSeen = true;
                } else if (contract.Contains("\"requestType\"")) reply = new { requestType="REVISION_REQUEST", authoringRequestEvidence=request,inputApplication=new {action=appliesValues?"FILL_CASE_FIELDS":"NOT_REQUESTED",evidence=appliesValues?request:""}, instructionTask="Cambia el estilo", question="", explanation="Revisar el estilo.", continuesPendingTask=false };
                else if (contract.Contains("\"requestedOperations\"")) reply = new { requestedOperations = new[] {new {operation="PROPOSE_ADAPTATION",supportingPassage=0}} };
                else if (contract.Contains("\"additionalArtifactRequested\"")) reply = new { additionalArtifactRequested=false };
                else if (contract.Contains("\"access\"")) reply = new { access="NONE", control="NONE", save=reviewVersion?"UPDATE_CURRENT_RECORD":"NONE", explanation="Revisar." };
                else if (contract.Contains("\"stance\"")) {
                    independentInputCalls++;
                    using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.True(data.RootElement.GetProperty("authoringAlreadyRequested").GetBoolean());
                    Assert.Equal(request,data.RootElement.GetProperty("currentUserDirective").GetString());
                    Assert.False(data.RootElement.TryGetProperty("templateData",out _));
                    Assert.False(data.RootElement.TryGetProperty("confirmedInstructionChange",out _));
                    reply = new { targetObject="INPUT_VALUES", stance=appliesValues?"REQUESTED":"NOT_REQUESTED", supportingPassage=appliesValues?0:-1 };
                }
                else reply = new { action="NONE", providerId=(string?)null, executionEvidence="" };
                var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model="qwen3:8b", done=true, message=new { content=JsonSerializer.Serialize(reply,json) } },json));
                exchange.Response.ContentType="application/json"; exchange.Response.ContentLength64=bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes,timeout.Token); exchange.Response.Close();
            }
            Assert.True(constraintSeen); var result = await pending;
            Assert.Equal(!reviewVersion, result.Constraints.PreserveOriginal);
            Assert.Equal(reviewVersion, result.Has(SemanticOperation.REQUEST_SAVE_VERSION));
            Assert.Equal(appliesValues,result.Has(SemanticOperation.FILL_VARIABLES));
            Assert.Equal(0,independentInputCalls);
        } finally { timeout.Cancel(); listener.Stop(); }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GroundingFeedbackReturnsToTheConstraintStageWithOneBoundedCorrection(bool repaired)
    {
        var portReservation = new TcpListener(IPAddress.Loopback, 0); portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port; portReservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        const string request = "Conserva el resto del contenido.";
        var pending = inference.ExtractContent(request, SemanticContext.Empty, "qwen3:8b", timeout.Token);
        try {
            for (var attempt = 0; attempt < 2; attempt++) {
                var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request, data.RootElement.GetProperty("request").GetString());
                Assert.False(packet.RootElement.TryGetProperty("tools", out _));
                if (attempt == 0) Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("validationFeedback").ValueKind);
                else {
                    Assert.Equal("UNGROUNDED_PROHIBITION", data.RootElement.GetProperty("validationFeedback").GetProperty("code").GetString());
                    Assert.DoesNotContain("Prohibición inventada.", data.RootElement.GetRawText());
                    Assert.False(data.RootElement.GetProperty("validationFeedback").TryGetProperty("rejectedConstraints", out _));
                }
                var constraints = new SemanticConstraintInput(attempt == 1 && repaired ? [request] : ["Prohibición inventada."], true, null, null, [], [], false, "", "");
                var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model = "qwen3:8b", done = true,
                    message = new { content = JsonSerializer.Serialize(constraints, json) } }, json));
                exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            if (repaired) {
                var scope = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet = await JsonDocument.ParseAsync(scope.Request.InputStream, cancellationToken: timeout.Token);
                using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request, data.RootElement.GetProperty("candidateClauses")[0].GetProperty("text").GetString());
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model = "qwen3:8b", done = true, message = new { content = "{\"clauses\":[{\"index\":0,\"scope\":\"EDITOR\"}]}" } }));
                await scope.Response.OutputStream.WriteAsync(bytes, timeout.Token); scope.Response.Close();
                var result = await pending;
                Assert.Equal(new[] { request }, result.UserProhibitions);
                Assert.Empty(result.LiteralClauses);
            }
            else {
                var failure = await Assert.ThrowsAsync<PreparationToolException>(() => pending);
                Assert.Equal("UNGROUNDED_PROHIBITION", failure.Code);
                Assert.Equal(true, failure.Data["NovaStageRepairExhausted"]);
            }
        } finally { timeout.Cancel(); listener.Stop(); }
    }
}
