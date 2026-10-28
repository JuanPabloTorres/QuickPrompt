using System.Reflection;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class InferenceFailurePropagationTests
{
    private const string Raw = "SYNTHETIC_UNTRUSTED_PROVIDER_BODY_TOKEN";
    public static IEnumerable<object[]> Codes() => new[] { "review", "advice", "ui", "legacy" }
        .SelectMany(owner => new[] { "MODEL_UNAVAILABLE", "MODEL_TIMEOUT", "MODEL_FAILED", "QUEUE_EXPIRED", "CONNECTION_REVOKED",
            "SESSION_REVOKED", "LEASE_EXPIRED", "HOST_STOPPED", "UNTRUSTED_UNKNOWN_CODE" }.Select(code => new object[] { owner, code }));

    [Theory, MemberData(nameof(Codes))]
    public async Task EachAdapterPreservesTypedCodeAndStageWithoutRawDiagnosticsOrAnotherRequest(string owner, string code)
    {
        var cause = new LlmUnavailableException(Raw, code); var transport = new FailureTransport(cause);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => Invoke(owner, transport, default));
        Assert.Equal(cause.Code, failure.Code); Assert.Equal(1, transport.Calls);
        Assert.Equal(owner switch { "review" => "ReadRequestedTitle", "advice" => "ExplainTaskGoal", "ui" => "Decide", _ => "SyntheticLegacyStage" }, failure.Tool);
        Assert.DoesNotContain(Raw, failure.ToString()); Assert.Null(failure.InnerException);
        Assert.Equal(0, transport.Rejections);
    }

    [Theory]
    [InlineData("review", false)] [InlineData("review", true)]
    [InlineData("advice", false)] [InlineData("advice", true)]
    [InlineData("ui", false)] [InlineData("ui", true)]
    [InlineData("legacy", false)] [InlineData("legacy", true)]
    public async Task CancellationBeforeOrDuringTransportStaysCancellationWithNoRetry(string owner, bool inFlight)
    {
        using var stop = new CancellationTokenSource();
        var transport = new FailureTransport(null, inFlight ? stop.Cancel : null);
        if (!inFlight) stop.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Invoke(owner, transport, stop.Token));
        Assert.Equal(stop.Token, failure.CancellationToken);
        Assert.Equal(inFlight ? 1 : 0, transport.Calls); Assert.Equal(0, transport.Rejections);
    }

    private static async Task Invoke(string owner, FailureTransport transport, CancellationToken ct)
    {
        switch (owner) {
            case "review": await new DraftFidelityReviewer(transport).ReadRequestedTitle("Crea una instrucción.", [], "qwen3:8b", ct); break;
            case "advice": await new PromptAdviceResponder(transport).Explain("Explica esta instrucción.", null, null, "qwen3:8b", ct); break;
            case "ui":
                var state = new UiVisibleState("focus", [], null, null, null, "Explica un tema.", [], [], false, false, null, null, false, false);
                var context = new UiDecisionContext("Inspección sintética.", state, [], UiToolCatalog.All.Where(tool => tool.Name == "finish").ToArray(), null, null);
                await new UiDecisionModel(transport).Decide(context, ct);
                Assert.Null(state.PromptId); Assert.False(state.DraftChanged); break;
            default:
                var interpreter = new SemanticIntentInterpreter(transport, new ForbiddenSelector());
                var method = typeof(SemanticIntentInterpreter).GetMethod("ChatForUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
                using (await (Task<JsonDocument>)method.Invoke(interpreter, [new { model = "qwen3:8b" }, ct, "SyntheticLegacyStage"])!) { }
                break;
        }
    }

    private sealed class FailureTransport(Exception? failure, Action? cancel = null) : IValidatedAgentModelTransport
    {
        public int Calls; public int Rejections;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++; cancel?.Invoke(); ct.ThrowIfCancellationRequested();
            return Task.FromException<JsonDocument>(failure ?? new InvalidOperationException("Synthetic transport should have been cancelled."));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? code) { }
        public void Reject(string? answer) => Rejections++;
    }
    private sealed class ForbiddenSelector : ICandidateSelector
    {
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
            => throw new InvalidOperationException("Error propagation must not select a source.");
    }
}
