using System.Diagnostics;
using System.Text.Json;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Tests;

/// <summary>Opt-in hardware test; never contacts a provider during ordinary CI.</summary>
public sealed class DockerModelRunnerFactAttribute : FactAttribute
{
    public DockerModelRunnerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_REAL_DMR") != "1")
            Skip = "Requires explicit Docker Model Runner hardware configuration.";
    }
}

public sealed class DockerModelRunnerRealProviderTests
{
    [DockerModelRunnerFact, Trait("Category", "RealDockerModelRunner")]
    public async Task CancellationReturnsControlAndTheFollowingProviderCallCompletesWithinItsBudget()
    {
        string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing explicit campaign configuration: " + key);
        var evidence = Path.Combine(Required("QUICKPROMPT_TEST_REAL_EVIDENCE"), "dmr-cancellation");
        Directory.CreateDirectory(evidence);
        using var observation = new DevelopmentLabObserver(true, evidence, Required("QUICKPROMPT_TEST_SOURCE_ROOT"));
        var options = new AdaptiveCoreOptions(Required("QUICKPROMPT_TEST_CORE_PYTHON"), Required("QUICKPROMPT_TEST_CORE_WORKER"),
            Required("QUICKPROMPT_TEST_CORE_SDK"), Required("QUICKPROMPT_TEST_MODEL_ENDPOINT"), Thinking: false,
            Provider: "docker-model-runner", PhysicalModel: Required("QUICKPROMPT_TEST_PHYSICAL_MODEL"));
        var transport = new AdaptiveCoreModelTransport(options, diagnostics: observation);
        // model_started precedes provider I/O. Establish readiness first so this
        // test cancels generation rather than an unboundedly assumed cold load.
        using var readyDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ready = Stopwatch.StartNew();
        using var warmup = await transport.Send(Packet("Responde únicamente LISTO.", 32), readyDeadline.Token, "dmr.readiness.synthetic");
        ready.Stop();
        Assert.Equal("stop", warmup.RootElement.GetProperty("done_reason").GetString());
        int Starts() => File.Exists(observation.CorePath)
            ? File.ReadLines(observation.CorePath).Count(line => line.Contains("\"kind\": \"model_started\"", StringComparison.Ordinal)) : 0;
        var previousStarts = Starts();
        using var cancellation = new CancellationTokenSource();
        var pending = transport.Send(Packet("Enumera los enteros del 1 al 2000, cada uno en su propia línea. Continúa sin resumir.", 2048),
            cancellation.Token, "dmr.cancellation.synthetic");
        var started = Stopwatch.StartNew();
        while (Starts() <= previousStarts)
        {
            if (pending.IsCompleted) await pending;
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(30), "Core never reported inference startup.");
            await Task.Delay(50);
        }
        await Task.Delay(500);
        Assert.False(pending.IsCompleted, "Inference ended before cancellation could be exercised.");
        var cancelled = Stopwatch.StartNew();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        cancelled.Stop();
        Assert.True(cancelled.Elapsed < TimeSpan.FromSeconds(5));

        var next = Stopwatch.StartNew();
        // Use the product's request deadline. Process startup/import and model
        // service time are distinct: measure the latter by the reply's actual ID.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var reply = await transport.Send(Packet("Responde únicamente LISTO.", 32), deadline.Token, "dmr.after-cancellation.synthetic");
        next.Stop();
        Assert.Equal("stop", reply.RootElement.GetProperty("done_reason").GetString());
        Assert.Contains("LISTO", reply.RootElement.GetProperty("message").GetProperty("content").GetString());
        var requestId = reply.RootElement.GetProperty("coreRequestId").GetString();
        var events = File.ReadLines(observation.CorePath).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .Where(item => item.GetProperty("requestId").GetString() == requestId).ToArray();
        DateTimeOffset Time(string kind) => events.Single(item => item.GetProperty("kind").GetString() == kind)
            .GetProperty("observedAt").GetDateTimeOffset();
        var providerDuration = Time("model_finished") - Time("model_started");
        Assert.InRange(providerDuration.TotalSeconds, 0, 12);
        File.WriteAllText(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new {
            passed = true, readyMilliseconds = ready.ElapsedMilliseconds,
            cancelMilliseconds = cancelled.ElapsedMilliseconds, nextMilliseconds = next.ElapsedMilliseconds,
            followingProviderMilliseconds = providerDuration.TotalMilliseconds,
            nextModelCalls = reply.RootElement.GetProperty("modelCalls").GetInt32(),
            scope = "Real .NET worker cancellation and following correlated provider interval; startup is separate. Native first-token/termination contrasts prove engine cancellation separately. No OAuth worker or domain effects."
        }));
    }

    private static object Packet(string text, int outputTokens) => new {
        model = "qwen3:8b", think = false, messages = new[] { new { role = "user", content = text } },
        options = new { num_ctx = 12288, num_predict = outputTokens, temperature = 0 }
    };
}
