using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Source preservation and terminal compression contracts, not model quality.</summary>
public sealed class CompressionSelectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static AuthoringBriefContent Source() => new("Documento", "Metadatos humanos",
        "Resume <documento>. Esta tarea resume ese documento. El objetivo es resumir el documento.",
        [], ["No inventes datos."], ["Entrega sólo el resumen."], []);

    [Fact]
    public async Task IntentDoesNotChooseAnArtifactAndTerminalSelectionCanRemoveAllRepetitions()
    {
        var source = Source() with { Steps = ["Describe los hechos. Describe los hechos."] };
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Plan();
            Assert.Equal("SelectCompleteCompressionArtifact", purpose);
            var material = Material(packet);
            Assert.Equal("Quita todas las repeticiones.", material.GetProperty("originalRequest").GetString());
            Assert.Equal(source.Title, material.GetProperty("sourceMetadata").GetProperty("title").GetString());
            Assert.Contains("El objetivo", material.GetProperty("establishedTask")[0].GetProperty("text").GetString());
            Assert.False(material.TryGetProperty("firstProposal", out _));
            Assert.False(material.TryGetProperty("previewChoices", out _));
            Assert.False(material.TryGetProperty("translationResources", out _));
            Assert.InRange(Encoding.UTF8.GetByteCount(packet.GetProperty("messages")[1].GetProperty("content").GetString()!), 1, 26000);
            return Selection(packet, "COMPRESSED", new() { ["task"] = "task/3", ["step_1"] = "step_1/1" });
        });
        var result = await new AuthoringRevisionEditor(provider).Review("Quita todas las repeticiones.", [], source, "qwen3:8b", default);
        Assert.Equal("Resume <documento>. ", result.Suggested!.Task);
        Assert.Equal(new[] { "Describe los hechos. " }, result.Suggested.Steps);
        Assert.Equal(source.Constraints, result.Suggested.Constraints);
        Assert.Equal(source.OutputRequirements, result.Suggested.OutputRequirements);
        Assert.Equal(source.Title, result.Suggested.Title);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task UniqueContinuationCanRemainExactWithoutBeingDeclaredCompressed()
    {
        var source = Source() with { Task = "Pregunta por la fecha antes de fechar <documento>. Continúa con los otros temas de <documento>." };
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision"
            ? Plan() : Selection(packet, "UNCHANGED", Originals(packet)));
        var result = await new AuthoringRevisionEditor(provider).Review("Quita sólo repeticiones; conserva lo demás exacto.", [], source, "qwen3:8b", default);
        Assert.Equal(JsonSerializer.Serialize(source, Json), JsonSerializer.Serialize(result.Suggested, Json));
        Assert.Equal(2, provider.Calls);
    }

    [Theory]
    [InlineData("COMPRESSED", "task/0")]
    [InlineData("UNCHANGED", "task/1")]
    public async Task ContradictoryChangeLabelsAreRejectedWithoutAThirdCall(string outcome, string choice)
    {
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision"
            ? Plan() : Selection(packet, outcome, new() { ["task"] = choice }));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review(
            "Quita todas las repeticiones.", [], Source(), "qwen3:8b", default));
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task AForeignArtifactCannotBeAppliedToTheCurrentSource()
    {
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision" ? Plan()
            : new { basisHash = new string('A', 64), outcome = "COMPRESSED", choices = new { task = "task/3" } });
        await Assert.ThrowsAsync<ConflictException>(() => new AuthoringRevisionEditor(provider).Review(
            "Quita todas las repeticiones.", [], Source(), "qwen3:8b", default));
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task CancellationAfterIntentNeverStartsTerminalSelection()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new Provider((_, _) => { cancellation.Cancel(); return Plan(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AuthoringRevisionEditor(provider).Review(
            "Quita todas las repeticiones.", [], Source(), "qwen3:8b", cancellation.Token));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task PermittedReasoningIsReservedForTheTerminalSelectionWithinTheExistingBudget()
    {
        var provider = new Provider((packet, purpose) => {
            Assert.False(packet.GetProperty("stream").GetBoolean());
            Assert.Equal(1, packet.GetProperty("options").GetProperty("max_requests").GetInt32());
            if (purpose == "DiscoverCurrentAuthorDecision") {
                Assert.False(packet.GetProperty("think").GetBoolean());
                Assert.Equal(1400, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
                return Plan();
            }
            Assert.Equal("SelectCompleteCompressionArtifact", purpose);
            Assert.True(packet.GetProperty("think").GetBoolean());
            Assert.Equal(3500, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
            return Selection(packet, "COMPRESSED", new() { ["task"] = "task/3" });
        }) { AllowsThinking = true };
        var result = await new AuthoringRevisionEditor(provider).Review("Quita todas las repeticiones.", [], Source(), "qwen3:8b", default);
        Assert.Equal("Resume <documento>. ", result.Suggested!.Task);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task AQuantityWithNoRemovableUnitDoesNotOfferTheCompressionTool()
    {
        var source = Source() with { Task = "Organiza <documento>. Entrega exactamente siete apartados." };
        var provider = new Provider((packet, purpose) => {
            Assert.Equal("DiscoverCurrentAuthorDecision", purpose);
            Assert.DoesNotContain("\"COMPRESS\"", packet.GetProperty("format").GetRawText());
            return new { outcome = "READY", decisionText = (string?)null, revisionKind = "KEEP",
                previewChoices = new Dictionary<string, string>(), translationPreparation = (object?)null };
        });
        var result = await new AuthoringRevisionEditor(provider).Review("Quita repeticiones si las hay; conserva cantidades.", [], source, "qwen3:8b", default);
        Assert.Equal(source.Task, result.Suggested!.Task);
        Assert.Equal(1, provider.Calls);
    }

    private static object Plan() => new { outcome = "READY", decisionText = (string?)null,
        revisionKind = "COMPRESS", previewChoices = (object?)null, translationPreparation = (object?)null };
    private static JsonElement Material(JsonElement packet) => JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!).RootElement.Clone();
    private static Dictionary<string, string> Originals(JsonElement packet) => Material(packet).GetProperty("sourcePreviews")
        .GetProperty("fields").EnumerateObject().ToDictionary(field => field.Name, field => field.Value[0].GetProperty("id").GetString()!);
    private static object Selection(JsonElement packet, string outcome, Dictionary<string, string> choices) => new {
        basisHash = Material(packet).GetProperty("basisHash").GetString(), outcome, choices
    };
    private sealed class Provider(Func<JsonElement, string, object> reply) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public bool AllowsThinking { get; init; }
        public Task<JsonDocument> Send(object body, CancellationToken cancellation, string purpose)
        {
            cancellation.ThrowIfCancellationRequested();
            Calls++;
            Assert.InRange(Calls, 1, 2);
            var packet = JsonSerializer.SerializeToElement(body, Json);
            var content = JsonSerializer.Serialize(reply(packet, purpose), Json);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content } }, Json)));
        }
        public bool UsesThinking(string model) => AllowsThinking;
        public void Observe(int stage, string text, string? error) { }
        public void Reject(string? answer) { }
    }
}
