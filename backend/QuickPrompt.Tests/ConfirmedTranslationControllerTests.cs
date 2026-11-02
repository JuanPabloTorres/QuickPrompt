using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed partial class ConfirmedTranslationControllerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly PreparationPreferences English = new("en", "Plain", "synthetic");
    private static AuthoringBriefContent Source(string? task = null) => new("Plantilla", "Instrucción reutilizable",
        task ?? "Resume <texto>.", [], ["No inventes datos."], ["Sólo el resumen."], []);

    [Fact]
    public async Task DirectTranslationUsesOnlyTheConfiguredTransportThinkingCapability()
    {
        var provider = new Provider(packet => Reply(packet, Fields(packet)), thinking: true);
        await Review(provider, Source());
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ConfirmedLanguageUsesOneTranslationWithAllFieldsAndNoDiscoveryOrDictionaryAuthority()
    {
        var source = Source(); var before = LiveRuns.Hash(source);
        var provider = new Provider(packet => {
            var material = Material(packet);
            Assert.Equal("en", material.GetProperty("translationTarget").GetString());
            Assert.False(material.TryGetProperty("translationPreparation", out _));
            Assert.False(material.TryGetProperty("lexicalEvidence", out _));
            var fields = Fields(packet);
            Assert.Equal(new[] { "task", "constraint_1", "output_1", "metadata_title", "metadata_description" }, fields.Keys);
            fields["task"] = "Summarize ⟦0⟧."; fields["metadata_title"] = "Template";
            fields["metadata_description"] = "Reusable instruction";
            return Reply(packet, fields);
        });
        var result = await Review(provider, source);
        Assert.Equal(1, provider.Calls); Assert.True(result.MetadataProposed);
        Assert.Equal("Summarize <texto>.", result.Suggested!.Task);
        Assert.Equal("Template", result.Suggested.Title); Assert.Equal("Reusable instruction", result.Suggested.Description);
        Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Theory]
    [InlineData("«Firmar»", "«Firmar»")]
    [InlineData("\"Publish\"", "\"Publish\"")]
    [InlineData("“Cerrar”", "“Cerrar”")]
    [InlineData("«<texto>»", "«⟦0⟧»")]
    public async Task SourceQuotedValuesRemainExactAndNeverNeedToBeTranslatedByTheModel(string quoted, string boundSource)
    {
        var source = Source("Abre " + quoted + " y resume <texto>.");
        var before = LiveRuns.Hash(source);
        var provider = new Provider(packet => {
            var material = Material(packet);
            var binding = Assert.Single(material.GetProperty("sourceBindings").EnumerateArray());
            Assert.Equal(boundSource, binding.GetProperty("sourceText").GetString());
            Assert.Equal("task", binding.GetProperty("field").GetString());
            Assert.Contains("⟪literal:", Fields(packet)["task"]);
            Assert.DoesNotContain(quoted, Fields(packet)["task"]);
            return Reply(packet, Fields(packet));
        });
        var result = await Review(provider, source);
        Assert.Equal(source.Task, result.Suggested!.Task); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    [InlineData("moved")]
    public async Task LiteralBindingsCannotBeLostCopiedForgedOrMoved(string attack)
    {
        var source = Source("Abre «Firmar» y resume <texto>.");
        var provider = new Provider(packet => {
            var fields = Fields(packet);
            var token = Material(packet).GetProperty("sourceBindings")[0].GetProperty("token").GetString()!;
            fields["task"] = fields["task"].Replace(token, attack switch {
                "missing" or "moved" => "", "duplicate" => token + token,
                "foreign" => "⟪literal:99:99⟫", _ => "⟪literal:bad⟫"
            }, StringComparison.Ordinal);
            if (attack == "moved") fields["constraint_1"] += token;
            return Reply(packet, fields);
        });
        await Assert.ThrowsAsync<ValidationException>(() => Review(provider, source));
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("⟦0⟧ ⟦0⟧")]
    [InlineData("⟦99⟧")]
    [InlineData("<inventado>")]
    public async Task InputBindingsRemainOwnedAndUnfilled(string replacement)
    {
        var provider = new Provider(packet => {
            var fields = Fields(packet); fields["task"] = fields["task"].Replace("⟦0⟧", replacement, StringComparison.Ordinal);
            return Reply(packet, fields);
        });
        await Assert.ThrowsAsync<ValidationException>(() => Review(provider, Source()));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("protocol")]
    [InlineData("missing-field")]
    [InlineData("extra-field")]
    [InlineData("duplicate-property")]
    [InlineData("old-wire")]
    public async Task FullSourceTransactionRejectsForgedIncompleteAndLegacyReplies(string attack)
    {
        var provider = new Provider(packet => {
            var node = JsonSerializer.SerializeToNode(Reply(packet, Fields(packet)), Json)!;
            switch (attack) {
                case "hash": node["basisHash"] = new string('0', 64); break;
                case "protocol": node["protocol"] = "unknown"; break;
                case "missing-field": node["fields"]!.AsObject().Remove("metadata_description"); break;
                case "extra-field": node["fields"]!["permission"] = "SAVE"; break;
                case "duplicate-property": return new Raw(node.ToJsonString().Replace("\"fields\":", "\"fields\":{},\"fields\":", StringComparison.Ordinal));
                case "old-wire": return new { basisHash = Material(packet).GetProperty("basisHash").GetString(), bindings = new { }, rewrites = Fields(packet) };
            }
            return node;
        });
        await Assert.ThrowsAsync<ValidationException>(() => Review(provider, Source()));
    }

    [Fact]
    public async Task ANewInputRequiresTheExistingHumanSelectionAndCannotBecomeMetadata()
    {
        var provider = new Provider(packet => {
            Assert.Equal(2, Material(packet).GetProperty("inputs").GetArrayLength());
            var fields = Fields(packet); fields["task"] += " ⟦1⟧.";
            return Reply(packet, fields);
        });
        var result = await new AuthoringRevisionEditor(provider).Review("Traduce e incorpora la entrada fecha.", [], Source(),
            "qwen3:8b", default, English, allowedInputs: ["texto", "fecha"]);
        Assert.Contains("<fecha>", result.Suggested!.Task); Assert.Contains("<texto>", result.Suggested.Task);

        var invalid = new Provider(packet => {
            var fields = Fields(packet); fields["metadata_title"] += " ⟦1⟧";
            return Reply(packet, fields);
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(invalid).Review(
            "Traduce e incorpora la entrada fecha.", [], Source(), "qwen3:8b", default, English, allowedInputs: ["texto", "fecha"]));
    }

    [Fact]
    public async Task ReservedSourceLiteralNamespaceFailsBeforeAnyModelCall()
    {
        var provider = new Provider(packet => Reply(packet, Fields(packet)));
        await Assert.ThrowsAsync<ValidationException>(() => Review(provider, Source("Lee ⟪literal:0:1⟫ y <texto>.")));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task CancellationAfterReplyCannotExposeASuggestion()
    {
        using var stop = new CancellationTokenSource();
        var provider = new Provider(packet => { var reply = Reply(packet, Fields(packet)); stop.Cancel(); return reply; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Review(provider, Source(), stop.Token));
        Assert.Equal(1, provider.Calls);
    }

    private static Task<AuthoringRevisionReview> Review(Provider provider, AuthoringBriefContent source, CancellationToken cancellation = default) =>
        new AuthoringRevisionEditor(provider).Review("Traduce todos los campos al inglés.", [], source, "qwen3:8b", cancellation, English);
    private static JsonElement Material(JsonElement packet) => JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
    private static Dictionary<string, string> Fields(JsonElement packet) => Material(packet).GetProperty("fields").EnumerateArray()
        .ToDictionary(field => field.GetProperty("id").GetString()!, field => field.GetProperty("text").GetString()!);
    private static object Reply(JsonElement packet, Dictionary<string, string> fields) => new {
        protocol = "quickprompt-confirmed-translation/1", basisHash = Material(packet).GetProperty("basisHash").GetString(), fields
    };
    private sealed record Raw(string Content);
    private sealed class Provider(Func<JsonElement, object> answer, bool thinking = false) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public Task<JsonDocument> Send(object body, CancellationToken cancellation, string purpose)
        {
            cancellation.ThrowIfCancellationRequested(); Assert.Equal(1, ++Calls);
            Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
            var packet = JsonSerializer.SerializeToElement(body, Json);
            Assert.Equal(thinking, packet.GetProperty("think").GetBoolean());
            Assert.Equal(1, packet.GetProperty("options").GetProperty("max_requests").GetInt32());
            Assert.Equal(3500, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
            var value = answer(packet); var content = value is Raw raw ? raw.Content : JsonSerializer.Serialize(value, Json);
            return Task.FromResult(JsonSerializer.SerializeToDocument(new { message = new { content } }));
        }
        public bool UsesThinking(string model) => thinking;
        public void Observe(int stage, string text, string? error) { }
        public void Reject(string? answer) { }
    }
}
