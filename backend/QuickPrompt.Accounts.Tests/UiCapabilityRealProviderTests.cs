using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Opt-in real-provider proposals over synthetic states, without a browser, account or effect executor.</summary>
public sealed class UiCapabilityRealProviderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureSixFrozenUiProposalsWithoutExecutingTheirTools()
    {
        var fixture = await File.ReadAllBytesAsync(Required("QUICKPROMPT_TEST_UI_CASES"));
        var oracleBytes = await File.ReadAllBytesAsync(Required("QUICKPROMPT_TEST_UI_ORACLE"));
        var fixtureHash = Hash(fixture); var oracleHash = Hash(oracleBytes);
        Assert.Equal(Required("QUICKPROMPT_TEST_UI_CASES_SHA256"), fixtureHash);
        Assert.Equal(Required("QUICKPROMPT_TEST_UI_ORACLE_SHA256"), oracleHash);
        var directory = Required("QUICKPROMPT_TEST_UI_EVIDENCE");
        using var plan = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "PLAN.json")));
        Assert.Equal(fixtureHash, plan.RootElement.GetProperty("fixtureSha256").GetString());
        Assert.Equal(oracleHash, plan.RootElement.GetProperty("oracleSha256").GetString());
        Assert.Equal("docker-model-runner", Required("QUICKPROMPT_TEST_PROVIDER"));
        Assert.Equal("false", Required("QUICKPROMPT_TEST_THINKING"));
        Assert.Equal(plan.RootElement.GetProperty("physicalModel").GetString(), Required("QUICKPROMPT_TEST_PHYSICAL_MODEL"));
        foreach (var source in plan.RootElement.GetProperty("sourceHashes").EnumerateObject())
            Assert.Equal(source.Value.GetString(), Hash(await File.ReadAllBytesAsync(Path.Combine(Required("QUICKPROMPT_TEST_UI_ROOT"), source.Name))));
        var cases = JsonSerializer.Deserialize<UiCase[]>(fixture, Json)!;
        var oracles = JsonSerializer.Deserialize<UiOracle[]>(oracleBytes, Json)!.ToDictionary(item => item.Id);
        Assert.Equal(6, cases.Length); Assert.Equal(6, cases.Select(item => item.Id).Distinct().Count());
        Assert.Equal(cases.Select(item => item.Id).Order(), oracles.Keys.Order());
        Assert.All(cases, item => Assert.False(File.Exists(Path.Combine(directory, item.Id + ".json")), "Never overwrite or selectively retry a captured case."));
        // Freeze/check every material and catalog before acquiring the inference lease.
        var prepared = cases.Select(Prepare).ToArray();
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var failures = new List<string>();
        foreach (var item in prepared) {
            var capture = new Capture(AccountHttpTests.RealProvider()); var timer = Stopwatch.StartNew();
            UiDecision? decision = null; Exception? failure = null; var domainValidated = false; var oracleChecked = false;
            try {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                decision = await new UiDecisionModel(capture).Decide(item.Context, deadline.Token);
                UiToolCatalog.Validate(decision, item.Context.Tools, item.Context.State, item.Input, item.Context.Semantic);
                domainValidated = true;
                CheckOracle(oracles[item.Case.Id], decision, item.Case.Request);
                oracleChecked = true;
            } catch (Exception error) { failure = error; failures.Add(item.Case.Id); }
            var record = new { item.Case.Id, fixtureSha256 = fixtureHash, oracleSha256 = oracleHash,
                item.Case.Request, state = item.Context.State, eligibleTools = item.Context.Tools,
                decision, domainValidated, frozenOracleChecksPassed = oracleChecked,
                semanticPassed = (bool?)null, needsIndependentQuestionReview = oracles[item.Case.Id].QuestionSemantics is not null,
                calls = capture.Calls, elapsedMs = timer.ElapsedMilliseconds, decideInvocations = 1, applicationRetries = 0,
                status = failure is null ? "CAPTURED_WITH_FROZEN_CHECKS_REQUIRES_REVIEW" : "FAILED_PRESERVED",
                error = failure?.GetType().Name, message = failure?.Message,
                scope = "Real UiDecisionModel/Core/DMR proposal over a synthetic UiVisibleState and production eligible catalog. No React capture, browser.Execute, accounts, DB, clipboard or effects. Core may use its existing shared two-request schema-repair budget." };
            using var file = new FileStream(Path.Combine(directory, item.Case.Id + ".json"), FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(file, record, Json);
        }
        Assert.Empty(failures);
    }

    private static PreparedCase Prepare(UiCase item)
    {
        var semantic = SemanticPlanCompiler.Compile(item.Meaning);
        var tools = UiToolCatalog.Available(item.State, item.Searched, item.Ambiguous, false, item.UpdateAllowed,
                requestedSourceId: item.RequestedSourceId)
            .Where(tool => SemanticPlanValidator.ToolAllowed(semantic, tool.Name))
            // The controls have just been observed and every frozen snapshot still has a pending task postcondition.
            .Where(tool => tool.Name is not ("ui_read_visible_state" or "ui_verify_prompt_state" or "finish")).ToArray();
        Assert.NotEmpty(tools);
        Assert.DoesNotContain(tools, tool => tool.Name is "ui_execute_local" or "ui_copy_compiled_prompt" or "ui_copy_template" or "ui_request_save_version");
        var context = new UiDecisionContext(item.Request, item.State, item.ExecutedTools, tools, item.RequestedSourceId, null,
            AuthorizedValues: item.AuthorizedValues, RequestedSourceTitle: item.RequestedSourceTitle,
            RequiresUpdate: item.UpdateAllowed, Semantic: semantic);
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), item.Request, SourceId: item.RequestedSourceId,
            SourceRevision: item.State.Revision, Values: item.AuthorizedValues);
        return new(item, context, input);
    }

    private static void CheckOracle(UiOracle oracle, UiDecision decision, string request)
    {
        Assert.Equal(oracle.ExpectedTool, decision.ToolName);
        if (oracle.ExactArguments is { } exact) Assert.True(JsonElement.DeepEquals(exact, decision.Arguments), "Arguments differ from the pre-frozen oracle.");
        if (oracle.ExactArgumentSubset is { } subset)
            foreach (var property in subset.EnumerateObject())
                Assert.True(decision.Arguments.TryGetProperty(property.Name, out var actual) && JsonElement.DeepEquals(property.Value, actual), "Argument differs: " + property.Name);
        if (oracle.EvidenceValue is { } value) {
            var evidence = decision.Arguments.GetProperty("evidence").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(evidence));
            Assert.Contains(evidence, request, StringComparison.Ordinal);
            Assert.Contains(value, evidence, StringComparison.Ordinal);
        }
        if (oracle.QuestionSemantics is not null)
            Assert.False(string.IsNullOrWhiteSpace(decision.Arguments.GetProperty("question").GetString()));
    }

    private static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Missing explicit UI proposal campaign setting: " + key);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed record UiCase(string Id, string Request, UiVisibleState State, SemanticMeaning Meaning,
        bool Searched, bool Ambiguous, bool UpdateAllowed, Guid? RequestedSourceId, string? RequestedSourceTitle,
        Dictionary<string, string>? AuthorizedValues, string[] ExecutedTools);
    private sealed record UiOracle(string Id, string ExpectedTool, JsonElement? ExactArguments,
        JsonElement? ExactArgumentSubset, string? EvidenceValue, string? QuestionSemantics);
    private sealed record PreparedCase(UiCase Case, UiDecisionContext Context, LiveStartInput Input);

    private sealed class Capture(IValidatedAgentModelTransport inner) : IValidatedAgentModelTransport
    {
        internal List<object> Calls { get; } = [];
        public async Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            Assert.Empty(Calls); // One application Decide/Send per case; Core's existing internal repair budget remains unchanged.
            var sent = JsonSerializer.SerializeToElement(packet, Json);
            Assert.Equal("qwen3:8b", sent.GetProperty("model").GetString());
            Assert.False(sent.GetProperty("think").GetBoolean());
            var dataBytes = Encoding.UTF8.GetByteCount(sent.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.InRange(dataBytes, 1, 32000);
            try {
                var response = await inner.Send(packet, ct, purpose);
                var canonical = AdaptiveCoreModelTransport.CreateRequest(sent, response.RootElement.GetProperty("coreRequestId").GetString()!);
                Assert.Equal(2, canonical.GetProperty("limits").GetProperty("requests").GetInt32());
                var modelCalls = response.RootElement.GetProperty("modelCalls").GetInt32();
                Calls.Add(new { purpose, packet = sent, canonicalRequestCodecReconstruction = canonical,
                    response = response.RootElement.Clone(), modelCalls, dataUtf8Bytes = dataBytes });
                Assert.InRange(modelCalls, 1, 2);
                return response;
            } catch { if (Calls.Count == 0) Calls.Add(new { purpose, packet = sent, failed = true, dataUtf8Bytes = dataBytes }); throw; }
        }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string text, string? error) => inner.Observe(stage, text, error);
        public void Reject(string? answer) => inner.Reject(answer);
    }
}
