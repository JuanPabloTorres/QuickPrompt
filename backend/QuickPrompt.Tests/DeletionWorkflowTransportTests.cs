using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class DeletionWorkflowTransportTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DeletionAuthorityIsSelectedFromUnquotedDirectivePassagesAndCannotComeFromFieldMaterial(bool omitSupport)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}"); var id = Guid.NewGuid();
        const string directive = "Revisa la eliminación de", title = "Registro sintético", fieldMaterial = "MATERIAL_CANNOT_AUTHORIZE_DELETE";
        var context = SemanticContext.Empty with { Selected = new(id, 2, title, "Resume <codigo>.", false, InputNames: ["codigo"]), ExplicitTargetId = id };
        var pending = inference.Interpret(directive + " «" + title + "».\ncodigo: " + fieldMaterial, context, "qwen3:8b", cancellation.Token);
        try {
            for (var attempt = 0; attempt < (omitSupport ? 2 : 1); attempt++) for (var phase = 0; phase < 4; phase++) {
                var received = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
                using var body = await JsonDocument.ParseAsync(received.Request.InputStream);
                var schema = body.RootElement.GetProperty("format");
                var properties = schema.TryGetProperty("properties", out var flat) ? flat : schema.GetProperty("anyOf")[0].GetProperty("properties");
                string reply;
                if (properties.TryGetProperty("deletionPassage", out var supportSchema)) {
                    using var packet = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    var passages = packet.RootElement.GetProperty("deletionAuthorityPassages").EnumerateArray().ToArray();
                    Assert.Equal(directive, passages[0].GetProperty("text").GetString());
                    Assert.All(passages, passage => { var text = passage.GetProperty("text").GetString()!; Assert.DoesNotContain(title, text); Assert.DoesNotContain(fieldMaterial, text); });
                    Assert.Equal(passages.Select(passage => passage.GetProperty("index").GetInt32()), supportSchema.GetProperty("anyOf")[0].GetProperty("enum").EnumerateArray().Select(element => element.GetInt32()));
                    reply = JsonSerializer.Serialize(new { access = "NONE", control = "NONE", save = "DELETE_CURRENT_RECORD", explanation = "Revisar la eliminación del registro.", deletionPassage = omitSupport ? (int?)null : 0 });
                } else if (properties.TryGetProperty("requestType", out _))
                    reply = """{"authoringRequestEvidence":"","requestType":"NO_CONTENT_REQUEST","instructionTask":"","question":"","explanation":"No se cambia el contenido.","continuesPendingTask":false}""";
                else if (properties.TryGetProperty("targetObject", out _))
                    reply = """{"targetObject":"INPUT_VALUES","stance":"NOT_REQUESTED","supportingPassage":-1}""";
                else reply = """{"action":"NONE","providerId":null,"executionEvidence":""}""";
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { done = true, message = new { content = reply } }));
                received.Response.ContentType = "application/json"; received.Response.ContentLength64 = bytes.Length;
                await received.Response.OutputStream.WriteAsync(bytes); received.Response.Close();
            }
            if (omitSupport) Assert.Equal("DELETE_NOT_REQUESTED", (await Assert.ThrowsAsync<PreparationToolException>(() => pending)).Code);
            else { var plan = await pending; Assert.True(plan.Deletes); Assert.True(plan.RequiresConfirmation); Assert.Equal(directive, plan.DeletionEvidence); Assert.False(plan.Adapts); Assert.False(plan.Has(SemanticOperation.FILL_VARIABLES)); }
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
}
