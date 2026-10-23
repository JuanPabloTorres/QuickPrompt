using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ReferenceInferencePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferenceUsesConfiguredThinkingWithoutBindingIdentityOrGrantingEffects(bool thinking)
    {
        var transport = new Capture();
        var interpreter = new SemanticIntentInterpreter(transport, new SemanticCandidateSelector(transport), semanticThinking: thinking);
        var source = new SemanticEntity(Guid.NewGuid(), 3, "Fuente real", "Resume <material>.", false, InputNames: ["material"]);
        var result = await interpreter.ResolveSourceReference("Usa Fuente real sin modificarla.", SemanticContext.Empty with { CatalogCandidates = [source] }, "qwen3:8b", default);
        Assert.Equal(thinking, transport.Packet.GetProperty("think").GetBoolean());
        Assert.Null(result!.ResolvedTargetId);
        Assert.Equal(new GoalTarget(source.Id, source.Revision), result.ProposedCatalogSource);
        Assert.False(result.NeedsClarification);
        Assert.Equal(TargetReference.NAMED, result.TargetReference);
    }

    private sealed class Capture : IValidatedAgentModelTransport
    {
        public JsonElement Packet { get; private set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Packet = JsonSerializer.SerializeToElement(packet);
            return Task.FromResult(JsonDocument.Parse("{\"message\":{\"content\":\"{\\\"sourceRole\\\":\\\"CATALOG_ENTRY_1\\\"}\"}}"));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }
}
