using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed partial class ConfirmedTranslationControllerTests
{
    [Fact]
    public async Task SourceDictionaryQueryIsNonthinkingAndFinalTranslationUsesOnlyConfiguredThinking()
    {
        var transport = new SourceQueryTransport();
        var result = await new AuthoringRevisionEditor(transport, sourceViews: new SourceReader()).Review(
            "Traduce toda la plantilla.", [], Source(), "qwen3:8b", default, English);
        Assert.Equal(2, transport.Calls);
        Assert.Equal(Source().Task, result.Suggested!.Task);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("foreign")]
    [InlineData("missing-slot")]
    [InlineData("extra-slot")]
    public async Task SourceDictionaryQueryCannotChangeSourceHashOrReadUnknownWord(string attack)
    {
        var transport = new SourceQueryTransport(attack);
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(transport, sourceViews: new SourceReader()).Review(
            "Traduce toda la plantilla.", [], Source(), "qwen3:8b", default, English));
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task ConfirmedTranslationDoesNotExposeOpaqueProvenanceOrProviderPreferences()
    {
        var provider = new Provider(packet => {
            var material = Material(packet).GetRawText();
            Assert.DoesNotContain("PRIVATE_PROVIDER", material);
            Assert.DoesNotContain("PRIVATE_PROVENANCE", material);
            return Reply(packet, Fields(packet));
        });
        await new AuthoringRevisionEditor(provider, sourceViews: new Views()).Review("Traduce.", [], Source(), "qwen3:8b", default,
            new("en", "Plain", "PRIVATE_PROVIDER"), provenance: new { marker = "PRIVATE_PROVENANCE" });
    }

    private sealed class SourceReader : IAuthoringTranslationSourceLexicon
    {
        internal int LexicalReads { get; private set; }
        public async Task<JsonElement> Read(IReadOnlyDictionary<string, string> fields, CancellationToken cancellation)
        {
            var node = JsonNode.Parse((await new Views().Read(fields, cancellation)).GetRawText())!;
            node["lexicalCatalog"] = JsonSerializer.SerializeToNode(new[] { new { id = "w0", term = "resumir" } });
            return JsonSerializer.SerializeToElement(node);
        }
        public JsonElement ReadLexical(JsonElement observed, string[] ids, CancellationToken cancellation)
        {
            LexicalReads++;
            Assert.Equal(new[] { "w0" }, ids);
            return JsonSerializer.SerializeToElement(new { selectedIds = ids, evidence = new { completeSyntheticDefinitions = true } });
        }
    }

    private sealed class SourceQueryTransport(string? attack = null) : IValidatedAgentModelTransport
    {
        internal int Calls { get; private set; }
        public Task<JsonDocument> Send(object body, CancellationToken cancellation, string purpose)
        {
            cancellation.ThrowIfCancellationRequested();
            var packet = JsonSerializer.SerializeToElement(body);
            object answer;
            if (++Calls == 1) {
                Assert.Equal("SelectSourceTranslationEvidence", purpose);
                Assert.False(packet.GetProperty("think").GetBoolean());
                Assert.Equal(1000, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
                Assert.NotEmpty(Fields(packet));
                var slotSchema = packet.GetProperty("format").GetProperty("properties").GetProperty("slots");
                Assert.Equal(6, slotSchema.GetProperty("required").GetArrayLength());
                Assert.False(packet.GetProperty("format").GetProperty("properties").TryGetProperty("ids", out _));
                var slots = Enumerable.Range(0, 6).ToDictionary(index => "s" + index,
                    index => index == 0 ? attack == "foreign" ? "w99" : "w0" : (string?)null);
                if (attack == "missing-slot") slots.Remove("s5");
                if (attack == "extra-slot") slots.Add("s6", "w0");
                answer = new { basisHash = attack == "hash" ? "foreign" : Material(packet).GetProperty("basisHash").GetString(), slots };
            } else {
                Assert.Equal(2, Calls);
                Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
                Assert.True(packet.GetProperty("think").GetBoolean());
                Assert.Equal(3500, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
                Assert.True(Material(packet).GetProperty("sourceLexicalEvidence").GetProperty("evidence").GetProperty("completeSyntheticDefinitions").GetBoolean());
                Assert.DoesNotContain("machineDraft", Material(packet).GetRawText());
                answer = Reply(packet, Fields(packet));
            }
            return Task.FromResult(JsonSerializer.SerializeToDocument(new { message = new { content = JsonSerializer.Serialize(answer) } }));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string text, string? error) { }
        public void Reject(string? answer) { }
    }
}
