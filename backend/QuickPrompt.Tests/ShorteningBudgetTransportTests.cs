using System.Net;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class ShorteningBudgetTransportTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GenerationReservesTheProtectedContractBeforeApplyingTheFinalLengthLimit(bool protectedClause, bool fields)
    {
        var material = fields ? "<material>" : "el material";
        var source = new Template { Title = "Guía", Template = $"Analiza detenidamente {material} y presenta una explicación minuciosa para el público indicado."
            + (protectedClause ? " No inventes cifras ni identidades." : "") };
        var sourceText = source.Template;
        var core = $"Resume {material} para principiantes.";
        var clauses = TemplateConstraints.Required(sourceText);
        var reservation = clauses.Length == 0 ? 0 : 2 + string.Join("\n", clauses).Length;
        var finalLimit = sourceText.Length - 1;
        var calls = 0;
        using var http = new HttpClient(new Handler(async request => {
            calls++;
            using var packet = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = packet.RootElement;
            Assert.True(root.GetProperty("think").GetBoolean()); Assert.False(root.TryGetProperty("tools", out _));
            Assert.Equal(finalLimit - reservation, root.GetProperty("format").GetProperty("properties").GetProperty("text").GetProperty("maxLength").GetInt32());
            Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
            using var input = JsonDocument.Parse(root.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(sourceText, input.RootElement.GetProperty("source").GetString());
            var budget = input.RootElement.GetProperty("generationBudget");
            Assert.Equal(protectedClause, budget.GetProperty("editableCoreOnly").GetBoolean());
            Assert.Equal(finalLimit, budget.GetProperty("maximumFinalCharacters").GetInt32());
            Assert.Equal(finalLimit - reservation, budget.GetProperty("maximumGeneratedCharacters").GetInt32());
            Assert.Equal(reservation, budget.GetProperty("reservedConstraintCharacters").GetInt32());
            Assert.Equal(fields ? new[] { "material" } : [], budget.GetProperty("requiredCoreInputNames").EnumerateArray().Select(value => value.GetString()!).ToArray());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { done = true, message = new { content = JsonSerializer.Serialize(new {
                text = core, reasons = new[] { "Claridad" }, changes = new[] { "Público y extensión" }, warnings = Array.Empty<string>(),
                variablesPreserved = fields ? new[] { "material" } : []
            }) } })) };
        }));
        var client = new OllamaClient(http, contextLength: 12288);
        var result = await client.Propose("improve", "Hazlo más breve para principiantes.", source, "qwen3:8b", default, true, true,
            new TemplateEditInterpretation("Versión breve para principiantes", TargetTerms: ["principiantes"]));
        Assert.Equal(1, calls);
        Assert.Equal(core + (protectedClause ? "\n\n" + string.Join("\n", clauses) : ""), result.Text);
        Assert.True(result.Text.Length <= finalLimit);
        Assert.Empty(TemplateConstraints.Missing(sourceText, result.Text));
        Assert.Equal(TemplateCompiler.Names(sourceText), TemplateCompiler.Names(result.Text));
        Assert.Equal(sourceText, source.Template);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
