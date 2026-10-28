using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class GroundedCatalogReferenceTransportTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    public async Task CatalogChoiceRequestsFreshLookupAndNeverGrantsAnUnverifiedIdentity(bool stale, bool bound, bool literalTitle)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var catalog = new SemanticEntity(Guid.NewGuid(), 4, literalTitle ? "Registro real del catálogo" : "Claude", "Texto.", false, stale, []);
        var message = literalTitle ? "Abre Registro real del catálogo." : "Abre la ficha de catalogación y prepárala para Claude.";
        var context = SemanticContext.Empty with { Selected = new(Guid.NewGuid(), 1, "Otro registro abierto", "Otro texto.", false, InputNames: []), CatalogCandidates = [catalog] };
        if (bound) {
            context = context with { ResolvedSource = catalog, Candidates = [catalog] };
            context = context with { ResolvedReference = ResolvedTaskReference.Bind(message, new(TargetReference.NAMED, catalog.Id, literalTitle ? catalog.Title : "catalogación", false, ""), context) };
        }
        var pending = inference.Interpret(message, context, "qwen3:8b", timeout.Token);
        string[] replies = stale ? ["{\"sourceRole\":\"CATALOG_ENTRY_1\"}", "{\"sourceRole\":\"CATALOG_ENTRY_1\"}"] : [
            "{\"sourceRole\":\"CATALOG_ENTRY_1\"}",
            "{\"action\":\"NONE\",\"providerId\":null,\"executionEvidence\":\"\"}",
            "{\"requestType\":\"NO_CONTENT_REQUEST\",\"authoringRequestEvidence\":\"\",\"instructionTask\":\"\",\"question\":\"\",\"explanation\":\"Abrir.\",\"continuesPendingTask\":false}",
            "{\"targetObject\":\"INPUT_VALUES\",\"stance\":\"NOT_REQUESTED\",\"supportingPassage\":-1}",
            "{\"access\":\"VIEW_UNCHANGED\",\"control\":\"NONE\",\"save\":\"NONE\",\"explanation\":\"Abrir.\"}"
        ];
        if (bound) replies = [replies[1], "{\"requestedOperations\":[{\"operation\":\"OPEN_PROMPT\",\"supportingPassage\":0}]}", ..replies.Skip(2)];
        try {
            for(var step=0;step<replies.Length;step++) {
                var exchange=await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var packet=await JsonDocument.ParseAsync(exchange.Request.InputStream,cancellationToken:timeout.Token);
                if(!bound && (step==0 || stale)) {
                    var roles=packet.RootElement.GetProperty("format").GetProperty("properties").GetProperty("sourceRole").GetProperty("enum").EnumerateArray().Select(item=>item.GetString()).ToArray();
                    Assert.Equal(!stale,roles.Contains("CATALOG_ENTRY_1"));
                }
                if (bound && step == 0) {
                    var properties = packet.RootElement.GetProperty("format").GetProperty("properties");
                    Assert.False(properties.TryGetProperty("sourceRole", out _));
                    Assert.True(properties.TryGetProperty("providerId", out _));
                    using var data = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.Equal(literalTitle ? "Abre [saved instruction]." : message, data.RootElement.GetProperty("currentUserDirective").GetString());
                    Assert.Equal(catalog.Title, data.RootElement.GetProperty("sourceProposal").GetProperty("source").GetProperty("title").GetString());
                }
                var bytes=System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {model="qwen3:8b",done=true,message=new {content=replies[step]}}));
                exchange.Response.ContentType="application/json";exchange.Response.ContentLength64=bytes.Length;
                await exchange.Response.OutputStream.WriteAsync(bytes,timeout.Token);exchange.Response.Close();
            }
            if(stale) await Assert.ThrowsAsync<ValidationException>(()=>pending);
            else {
                var result=await pending;
                Assert.Equal(TargetReference.NAMED,result.TargetReference);Assert.Equal(literalTitle ? catalog.Title : "catalogación",result.Query);
                Assert.Equal(bound ? catalog.Id : (Guid?)null, result.ResolvedTargetId);
                Assert.Equal(bound, context.Ids.Contains(catalog.Id));
                Assert.False(result.Adapts);Assert.False(result.Executes);
            }
        } finally {timeout.Cancel();listener.Stop();}
    }
}
