using System.Text.Json;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class NovaTelemetryTests
{
    [Fact] public void DisabledDebugPointsDoNotEvaluateValues()
    {
        Assert.False(NovaTelemetry.Activities.HasListeners());
        var evaluated = false;
        NovaTelemetry.DebugPoint("test.disabled", false, () => { evaluated = true; return new(); });
        Assert.False(evaluated);
    }
    [Fact] public async Task DebugPointsKeepConcurrentRunIdentityWithoutChangingOperationMetrics()
    {
        using var capture = NovaTelemetry.CaptureDebugPoints();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        async Task Run(Guid id, bool rejected) {
            using var run = NovaTelemetry.Begin(id);
            await Task.Yield();
            NovaTelemetry.DebugPoint("test.validation", rejected, () => new() { ["selectedIndices"] = new[] { 2, 4 } });
        }
        await Task.WhenAll(Run(first, true), Run(second, false));
        var points = capture.Snapshot();
        Assert.Equal(2, points.Length);
        Assert.True(points.Single(point => point.RunId == first).Rejected);
        Assert.False(points.Single(point => point.RunId == second).Rejected);
        Assert.All(points, point => {
            Assert.Equal(new[] { 2, 4 }, Assert.IsType<int[]>(point.Values["selectedIndices"]));
            Assert.Equal("NovaTelemetryTests.cs", point.Values["code.file"]);
            Assert.True(Assert.IsType<int>(point.Values["code.line"]) > 0);
        });
        Assert.Contains("test.validation", JsonSerializer.Serialize(points));
        Assert.Equal(0, NovaTelemetry.Snapshot(first)!.OperationCount);
        Assert.Equal(0, NovaTelemetry.Snapshot(second)!.OperationCount);
    }
    [Fact] public void DebugCaptureIsBoundedDisposableAndObserverErrorsDoNotEscape()
    {
        var capture = NovaTelemetry.CaptureDebugPoints(2, _ => throw new InvalidOperationException("Observer failure"));
        for (var index = 0; index < 4; index++) NovaTelemetry.DebugPoint("test.bounded", false, () => new());
        Assert.Equal(2, capture.Snapshot().Length); Assert.Equal(2, capture.Dropped);
        Assert.Equal(2, capture.ObserverErrors);
        capture.Dispose();
        var evaluated = false;
        NovaTelemetry.DebugPoint("test.disposed", false, () => { evaluated = true; return new(); });
        Assert.False(evaluated); Assert.Equal(2, capture.Snapshot().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => NovaTelemetry.CaptureDebugPoints(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NovaTelemetry.CaptureDebugPoints(4097));
    }
    [Theory]
    [InlineData(System.Diagnostics.ActivityIdFormat.Hierarchical)]
    [InlineData(System.Diagnostics.ActivityIdFormat.W3C)]
    public void DebugCaptureSupportsBothHostActivityIdFormats(System.Diagnostics.ActivityIdFormat format)
    {
        using var parent = new System.Diagnostics.Activity("external.host").SetIdFormat(format).Start();
        using var capture = NovaTelemetry.CaptureDebugPoints();
        NovaTelemetry.DebugPoint("test.host", false, () => new());
        Assert.Equal("test.host", Assert.Single(capture.Snapshot()).Boundary);
    }
    [Fact] public async Task ConcurrentRunsAndLinkedRequestsKeepMetricsSeparateWithoutContent()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        async Task Run(Guid id, int tokens) {
            using var run = NovaTelemetry.Begin(id);
            await Task.Yield();
            using (var call = NovaTelemetry.Llm(JsonSerializer.SerializeToElement(new { model = "qwen3:8b", think = false, options = new { num_ctx = 12288, num_predict = 500 }, messages = new[] { new { content = "PRIVATE_PROMPT_SENTINEL" } } }), "test"))
                call.Complete(JsonSerializer.SerializeToElement(new { message = new { content = "PRIVATE_RESPONSE_SENTINEL" }, prompt_eval_count = tokens, eval_count = 8, load_duration = 12000000, eval_duration = 40000000 }));
            NovaTelemetry.VisibleAction(id);
        }
        await Task.WhenAll(Run(first, 20), Run(second, 40));
        using (NovaTelemetry.Attach(first)) { using var operation = NovaTelemetry.Measure("ui.test"); }
        var a = NovaTelemetry.Snapshot(first)!; var b = NovaTelemetry.Snapshot(second)!;
        Assert.Equal(20, a.Operations.Single(o => o.Operation == "llm.test").PromptTokens);
        Assert.Equal(40, b.Operations.Single(o => o.Operation == "llm.test").PromptTokens);
        Assert.Equal(1, a.LlmCalls); Assert.Equal(1, a.ToolCalls); Assert.Equal(0, b.ToolCalls);
        Assert.NotNull(a.FirstVisibleActionMs); Assert.Null(a.FirstModelTokenMs);
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(a)); Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(b));
    }
    [Fact] public void StorageIsBoundedAndCacheHitsAreNotReportedAsModelCalls()
    {
        var id = Guid.NewGuid();
        using (NovaTelemetry.Begin(id)) {
            using (var cached = NovaTelemetry.Llm(JsonSerializer.SerializeToElement(new { model = "qwen3:8b" }), "test")) cached.CacheHit();
            for (var index = 0; index < 300; index++) { using var operation = NovaTelemetry.Measure("verification.test"); }
        }
        var snapshot = NovaTelemetry.Snapshot(id)!;
        Assert.Equal(256, snapshot.Operations.Length); Assert.Equal(301, snapshot.OperationCount); Assert.True(snapshot.Truncated); Assert.Equal(0, snapshot.LlmCalls);
    }
    [Fact] public void CancelledMetadataLookupIsNotReportedAsAnAttemptedModelRequest()
    {
        var id = Guid.NewGuid();
        using (NovaTelemetry.Begin(id)) { using var measurement = NovaTelemetry.Llm(JsonSerializer.SerializeToElement(new { model = "qwen3:8b" }), "metadata"); }
        var snapshot = NovaTelemetry.Snapshot(id)!;
        Assert.Equal(0, snapshot.LlmCalls); Assert.Equal("NOT_REQUESTED", snapshot.Operations.Single().Outcome);
    }
}
