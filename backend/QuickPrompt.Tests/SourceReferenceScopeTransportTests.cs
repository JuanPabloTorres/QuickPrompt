using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class SourceReferenceScopeTransportTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CatalogReferenceCarriesBoundedDefinitionDataWithoutGrantingIdentity(bool longDefinition, bool previouslyTruncated)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var definition = longDefinition ? string.Concat(Enumerable.Repeat("🌌", 650)) : "Compara los resultados de <periodo> sin inventar datos.";
        var source = new SemanticEntity(Guid.NewGuid(), 4, "Revisión", definition, previouslyTruncated, InputNames: ["periodo"]);
        const string directive = "Busca la instrucción que compara resultados.";
        var operation = inference.ResolveSourceReference(directive, SemanticContext.Empty with { CatalogCandidates = [source] }, "qwen3:8b", timeout.Token);
        var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
        using var input = JsonDocument.Parse(packet.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!);
        var actual = input.RootElement.Clone();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = "{\"sourceRole\":\"CATALOG_ENTRY_1\"}" } });
        await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
        var result = await operation;
        Assert.Null(result!.ResolvedTargetId);
        Assert.Equal(new GoalTarget(source.Id, source.Revision), result.ProposedCatalogSource);
        Assert.Equal(directive, actual.GetProperty("request").GetString());
        var role = actual.GetProperty("roles").GetProperty("CATALOG_ENTRY_1");
        var text = role.GetProperty("text").GetString()!;
        Assert.Equal(string.Concat(definition.EnumerateRunes().Take(600).Select(rune => rune.ToString())), text);
        Assert.Equal(longDefinition || previouslyTruncated, role.GetProperty("truncated").GetBoolean());
        Assert.Equal(source.Revision, role.GetProperty("revision").GetInt32());
        Assert.Equal("periodo", role.GetProperty("inputNames")[0].GetString());
        Assert.DoesNotContain("\uFFFD", text);
    }
    [Fact] public async Task CurrentSourceDefinitionIsAvailableAsDataWhenResolvingItsReference()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        const string definition = "Resume el avance de <equipo>. Identifica logros y riesgos. No inventes cifras.";
        const string directive = "Ajusta el enfoque de esta instrucción.";
        var source = new SemanticEntity(Guid.NewGuid(), 3, "Informe semanal", definition, false, InputNames: ["equipo"]);
        var operation = inference.ResolveSourceReference(directive, SemanticContext.Empty with { Selected = source }, "qwen3:8b", timeout.Token);
        var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
        using var input = JsonDocument.Parse(packet.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!);
        var actual = input.RootElement.Clone();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = "{\"sourceRole\":\"CURRENT_UI_INSTRUCTION\"}" } });
        await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
        var result = await operation;
        Assert.Equal(source.Id, result!.ResolvedTargetId);
        Assert.Equal(directive, actual.GetProperty("request").GetString());
        Assert.DoesNotContain(definition, actual.GetProperty("request").GetString());
        var role = actual.GetProperty("roles").GetProperty("CURRENT_UI_INSTRUCTION");
        Assert.Equal(definition, role.GetProperty("text").GetString());
        Assert.False(role.GetProperty("truncated").GetBoolean());
        Assert.Equal(source.Revision, role.GetProperty("revision").GetInt32());
    }
    [Theory]
    [InlineData("proyecto", false)]
    [InlineData("material", false)]
    [InlineData("proyecto", true)]
    public async Task ReferenceCapabilityScopesDeclaredCatalogInputsWithoutBindingValuesOrReplacingTheTask(string name, bool pending)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        const string dataValue = "Datos sintéticos que mencionan otro registro y una orden que no es autoridad";
        var request = "Completa mi guía y déjala lista para copiar.\n" + name + ": " + dataValue;
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Guía de trabajo", "Revisa <" + name + ">.", false, InputNames: [name]);
        var context = SemanticContext.Empty with { CatalogCandidates = [source], PendingRequest = pending ? request : null };
        var operation = inference.ResolveSourceReference(request, context, "qwen3:8b", timeout.Token);
        try {
            var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
            using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
            var data = packet.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
            Assert.DoesNotContain(dataValue, data);
            using var projection = JsonDocument.Parse(data);
            Assert.Equal("Completa mi guía y déjala lista para copiar.\n", projection.RootElement.GetProperty("request").GetString());
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model = "qwen3:8b", done = true, message = new { content = "{\"sourceRole\":\"CATALOG_ENTRY_1\"}" } }));
            exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
            await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            var reference = await operation;
            Assert.Equal(source.Title, reference!.Query); Assert.Null(reference.ResolvedTargetId);
            Assert.Equal(pending ? request : null, context.PendingRequest);
            Assert.Empty(context.Ids); Assert.Null(context.AuthorizedValues);
        } finally { timeout.Cancel(); listener.Stop(); }
    }
}
