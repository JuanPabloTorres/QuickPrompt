using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class SourceReferenceTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyNamedLookupRepairsOnlyTheReferenceOnceWithoutDefaultingToTheCurrentItem(bool resolves)
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with { Selected = new(id, 4, "Guía vigente", "Explica <tema>.", false) };
        const string request = "Refina esta instrucción para principiantes sin guardarla.";
        var pending = inference.ResolveSourceReference(request, context, "qwen3:8b", cancellation.Token);
        var replies = new List<object> {
            new { sourceRole = "NAMED_LIBRARY_SOURCE" }, new { searchText = "", userPassageIndex = -1 },
            new { sourceRole = resolves ? "CURRENT_UI_INSTRUCTION" : "NAMED_LIBRARY_SOURCE" }
        };
        if (!resolves) replies.Add(new { searchText = "", userPassageIndex = -1 });
        for (var step = 0; step < replies.Count; step++) {
            var exchange = await listener.GetContextAsync().WaitAsync(cancellation.Token);
            using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: cancellation.Token);
            if (step == 2) {
                var actual = packet.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
                Assert.Contains("REFERENCE_CONTRACT_CONFLICT", actual);
                using var repairData = JsonDocument.Parse(actual);
                Assert.Equal(request, repairData.RootElement.GetProperty("request").GetString());
                Assert.Contains("CURRENT_UI_INSTRUCTION", packet.RootElement.GetProperty("format").GetRawText());
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(replies[step]) } });
            await exchange.Response.OutputStream.WriteAsync(bytes, cancellation.Token); exchange.Response.Close();
        }
        var result = await pending.WaitAsync(cancellation.Token);
        Assert.Equal(!resolves, result!.NeedsClarification);
        Assert.Equal(resolves ? id : (Guid?)null, result.ResolvedTargetId);
        Assert.Equal(resolves ? TargetReference.SELECTED : TargetReference.NONE, result.TargetReference);
    }

    [Theory]
    [InlineData("NO_IDENTIFIABLE_REFERENCE", false)]
    [InlineData("DESCRIBED_SOURCE", false)]
    [InlineData("DESCRIBED_SOURCE", true)]
    [InlineData("CURRENT_SOURCE", false)]
    [InlineData("WORKING_DRAFT", false)]
    public async Task SourceIdentityRequiresAnActualMentionOrAnActualContextBinding(string kind, bool inventedQuote)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var server = new HttpListener(); server.Prefixes.Add($"http://127.0.0.1:{port}/"); server.Start();
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", taskGoals: true, taskResultContract: true);
        var source = new PromptDocument(Guid.NewGuid(), "Quarterly overview", "", "Summarize milestones.", 0, false, false, 1, DateTime.UnixEpoch, DateTime.UnixEpoch, 0, null, [], new Dictionary<string, string>());
        var context = SemanticContext.Empty with {
            Selected = kind == "CURRENT_SOURCE" ? SemanticEntity.From(source) : null,
            ActiveDraft = kind == "WORKING_DRAFT" ? "Borrador sin guardar." : null
        };
        const string directive = "Busca mi reporte trimestral.";
        var pending = inference.SelectSource(directive, [new(source, 0, "Sin coincidencia literal")], context, "qwen3:8b", default);
        var exchange = await server.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream);
        var payload = kind == "DESCRIBED_SOURCE" ? (object)new { kind, quote = inventedQuote ? "Mención inventada" : "reporte trimestral.", promptIds = new[] { source.Id }, reason = "Referencia semántica controlada." }
            : new { kind, reason = "Vínculo tipado controlado; no mide comprensión del modelo." };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(payload) } });
        exchange.Response.ContentType = "application/json"; await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
        if (inventedQuote) { await Assert.ThrowsAsync<ValidationException>(() => pending); return; }
        var resolved = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(kind == "CURRENT_SOURCE" ? TargetReference.SELECTED : kind == "WORKING_DRAFT" ? TargetReference.ACTIVE_DRAFT : TargetReference.NAMED, resolved.Reference);
        Assert.Equal(kind is "DESCRIBED_SOURCE" or "CURRENT_SOURCE" ? new[] { source.Id } : [], resolved.Choice.PromptIds);
    }
}
