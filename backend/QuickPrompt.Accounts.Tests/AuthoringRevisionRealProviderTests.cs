using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>One capture per case of the isolated revision candidate; does not access accounts or persistence.</summary>
public sealed class AuthoringRevisionRealProviderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureFrozenRevisionCasesWithoutRetriesOrEffects()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing revision contrast setting: " + key);
        var fixture = await File.ReadAllBytesAsync(Required("QUICKPROMPT_TEST_REVISION_CASES"));
        var hash = Convert.ToHexString(SHA256.HashData(fixture));
        Assert.Equal(Required("QUICKPROMPT_TEST_REVISION_CASES_SHA256"), hash);
        var cases = JsonSerializer.Deserialize<RevisionCase[]>(fixture, Json)!;
        var directory = Required("QUICKPROMPT_TEST_REVISION_EVIDENCE");
        Directory.CreateDirectory(directory);
        Assert.Equal(cases.Length, cases.Select(item => item.Id).Distinct().Count());
        Assert.All(cases, item => Assert.False(File.Exists(Path.Combine(directory, item.Id + ".json"))));
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await using var profile = await CapturedThinkingProfile.Start(directory);
        using var diagnostics = new DevelopmentLabObserver(true, Path.Combine(directory, "lab"), Required("QUICKPROMPT_TEST_SOURCE_ROOT"));
        var failures = new List<string>();
        foreach (var item in cases) {
            var capture = new Capture(AccountHttpTests.RealProvider(diagnostics));
            var timer = Stopwatch.StartNew();
            AuthoringBriefContent? proposed = null;
            Exception? failure = null;
            try {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var review = await new AuthoringRevisionEditor(capture, sourceViews: ApertiumTranslationSourceViews.Docker(
                    "sha256:916315150c8a7a047a9bcb9950ede51bf2fc1ea0c0a8cfe3a1b9dc1d2545a880")).Review(item.Request, item.Clarifications ?? [], item.Established,
                    "qwen3:8b", deadline.Token, item.Preferences, item.Constraints);
                proposed = review.Suggested ?? throw new InvalidOperationException("Unexpected clarification: " + review.Question);
            } catch (Exception error) { failure = error; failures.Add(item.Id); }
            var record = new { item.Id, fixtureSha256 = hash, item.Request, original = item.Established, proposed,
                calls = capture.Calls, elapsedMs = timer.ElapsedMilliseconds, sampledOnce = true, semanticPassed = (bool?)null,
                status = failure is null ? "CAPTURED_REQUIRES_SEMANTIC_REVIEW" : "TECHNICAL_FAILURE",
                error = failure?.GetType().Name, message = failure?.Message,
                scope = "Isolated production revision candidate and real configured provider. No accounts, database, UI, installed Companion or effects." };
            using var file = new FileStream(Path.Combine(directory, item.Id + ".json"), FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(file, record, Json);
        }
        Assert.Empty(failures);
    }

    private sealed record RevisionCase(string Id, string Request, AuthoringBriefContent Established,
        PreparationPreferences? Preferences, SemanticConstraints? Constraints, AuthoringClarification[]? Clarifications = null);

    private sealed class Capture(IValidatedAgentModelTransport inner) : IValidatedAgentModelTransport
    {
        internal List<object> Calls { get; } = [];
        public async Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        {
            Assert.True(Calls.Count < 2, "A revision has two dependent stages and no retries.");
            var sent = JsonSerializer.SerializeToElement(packet, Json);
            try {
                var result = await inner.Send(packet, cancellation, purpose);
                Calls.Add(new { purpose, packet = sent, response = result.RootElement.Clone() });
                return result;
            } catch { Calls.Add(new { purpose, packet = sent, failed = true }); throw; }
        }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string text, string? error) => inner.Observe(stage, text, error);
        public void Reject(string? answer) => inner.Reject(answer);
    }
}
