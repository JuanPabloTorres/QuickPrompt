using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed partial class ConfirmedTranslationControllerTests
{
    [Fact]
    public async Task ADiscoveredTargetUsesDiscoveryAndTranslationWithoutAThirdDictionaryQuery()
    {
        var transport = new DiscoveredTargetTransport();
        var reader = new SourceReader();
        var result = await new AuthoringRevisionEditor(transport, sourceViews: reader).Review(
            "Traduce al inglés.", [], Source(), "qwen3:8b", default);
        Assert.Equal(2, transport.Calls);
        Assert.Equal(0, reader.LexicalReads);
        Assert.NotNull(result.Suggested);
        Assert.True(result.MetadataProposed);
    }

    private sealed class DiscoveredTargetTransport : IValidatedAgentModelTransport
    {
        internal int Calls { get; private set; }
        public Task<JsonDocument> Send(object body, CancellationToken cancellation, string purpose)
        {
            var packet = JsonSerializer.SerializeToElement(body);
            object answer;
            if (++Calls == 1) {
                Assert.Equal("DiscoverCurrentAuthorDecision", purpose);
                Assert.False(packet.GetProperty("think").GetBoolean());
                var choices = Material(packet).GetProperty("sourcePreviews").GetProperty("fields").EnumerateObject()
                    .ToDictionary(field => field.Name, field => field.Value[0].GetProperty("id").GetString()!);
                answer = new { outcome = "READY", decisionText = (string?)null, revisionKind = "TRANSLATE",
                    previewChoices = choices, translationPreparation = new { targetLanguage = "en" } };
            } else {
                Assert.Equal(2, Calls);
                Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
                Assert.True(packet.GetProperty("think").GetBoolean());
                Assert.Equal("en", Material(packet).GetProperty("translationTarget").GetString());
                Assert.False(Material(packet).TryGetProperty("sourceLexicalEvidence", out _));
                answer = Reply(packet, Fields(packet));
            }
            return Task.FromResult(JsonSerializer.SerializeToDocument(new { message = new { content = JsonSerializer.Serialize(answer) } }));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string text, string? error) { }
        public void Reject(string? answer) { }
    }
}
