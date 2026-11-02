using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using QuickPrompt.Companion;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Companion.Tests;

/// <summary>Configuration and synthetic HTTP only: no model, worker, Docker or account connection.</summary>
public sealed class ReasoningProfileTests
{
    [Fact]
    public void ExistingThinkingOptionDoesNotSilentlyEnableSampling()
    {
        using var files = new LocalFiles();
        var existing = CompanionOptions.Parse([.. files.Arguments, "--thinking", "true"]);
        Assert.True(existing.Inference.Thinking); Assert.False(existing.Inference.QwenThinkingSamplingEnabled); Assert.Null(existing.ReasoningProfile);
        var qualified = CompanionOptions.Parse([.. files.ReasonedArguments, "--thinking", "true", "--reasoning-profile", PairedReasoningProtocol.Profile]);
        Assert.True(qualified.Inference.QwenThinkingSamplingEnabled); Assert.Equal(PairedReasoningProtocol.Profile, qualified.ReasoningProfile);
        var packet = JsonSerializer.SerializeToElement(new { model = "qwen3:8b", think = true,
            messages = new[] { new { role = "user", content = "Synthetic only" } }, options = new { num_ctx = 12288, num_predict = 3500, temperature = 0 } });
        var request = AdaptiveCoreModelTransport.CreateRequest(QuickPrompt.Local.QwenThinkingSampling.Apply(packet, qualified.Inference.QwenThinkingSamplingEnabled), "synthetic");
        var limits = request.GetProperty("limits");
        Assert.True(limits.GetProperty("thinking").GetBoolean()); Assert.Equal(.6, limits.GetProperty("temperature").GetDouble());
        Assert.Equal(.95, limits.GetProperty("topP").GetDouble()); Assert.Equal(20, limits.GetProperty("topK").GetInt32());
        Assert.Equal(0, limits.GetProperty("minP").GetDouble()); Assert.Equal(42017, limits.GetProperty("seed").GetInt32());
    }

    [Theory]
    [InlineData("false", "qwen3-thinking-r2b/1")]
    [InlineData("true", "unknown")]
    public void InvalidLocalProfileFailsBeforeAnyWorker(string thinking, string profile)
    {
        using var files = new LocalFiles();
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse([.. files.ReasonedArguments, "--thinking", thinking, "--reasoning-profile", profile]));
        foreach (var command in new[] { "pair", "status", "disconnect" })
            Assert.Throws<ArgumentException>(() => CompanionOptions.Parse([command, "--server", "https://example.invalid/", "--reasoning-profile", profile]));
    }

    [Fact]
    public void PersistedReasoningProfileRequiresIndependentExplicitLocalRunPermission()
    {
        using var files = new LocalFiles();
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(files.ReasonedArguments));
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse([.. files.ReasonedArguments, "--thinking", "true"]));
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse([.. files.Arguments, "--thinking", "true", "--reasoning-profile", PairedReasoningProtocol.Profile]));
        var pair = CompanionOptions.Parse(["pair", "--server", "https://example.invalid/", "--capability", "nova-reasoned"]);
        Assert.Equal(PairedReasoningProtocol.GrantProfile, pair.Profile); Assert.False(pair.Inference.Thinking);
        Assert.Equal(LocalProposalProtocol.Scope, pair.Scope);
        Assert.Equal(LocalProposalProtocol.Version, LocalCompanionProfiles.Protocol(pair.Profile));
        Assert.Equal(LocalProposalProtocol.ResourcePath, LocalCompanionProfiles.ResourcePath(pair.Profile));
        Assert.Null(pair.ReasoningProfile);
    }

    [Theory]
    [InlineData(0, 12288, true)]
    [InlineData(-1, 8192, true)]
    [InlineData(-1, 12288, false)]
    public void PreflightRejectsIncompatiblePhysicalConfiguration(int reasoning, int context, bool samePin)
    {
        using var metadata = JsonDocument.Parse(Metadata(reasoning, context, samePin));
        Assert.Throws<InvalidOperationException>(() => CompanionReasoningPreflight.Validate(metadata.RootElement, PairedReasoningProtocol.PhysicalModel));
    }

    [Fact]
    public void PreflightOnlyAcceptsPinnedReasoningEnabledSingleParallelProfile()
    {
        using var metadata = JsonDocument.Parse(Metadata(-1, 12288, true));
        CompanionReasoningPreflight.Validate(metadata.RootElement, PairedReasoningProtocol.PhysicalModel);
        CompanionReasoningPreflight.Validate(metadata.RootElement, PairedReasoningProtocol.PhysicalModel[10..]);
    }

    private static string Metadata(int reasoning, int context, bool samePin) => JsonSerializer.Serialize(new[] { new {
        ModelID = samePin ? PairedReasoningProtocol.PhysicalModel.Split('@')[1] : "sha256:" + new string('0', 64), Backend = "llama.cpp", Mode = "completion",
        Config = new Dictionary<string, object> { ["context-size"] = context, ["llamacpp"] = new Dictionary<string, int> { ["reasoning-budget"] = reasoning }, ["runtime-flags"] = new[] { "--parallel", "1" }, ["keep_alive"] = "5m0s" }
    } });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewClientRequiresExplicitServerSupportAndAcknowledgement(bool support)
    {
        using var files = new LocalFiles(); var options = CompanionOptions.Parse([.. files.ReasonedArguments, "--thinking", "true", "--reasoning-profile", PairedReasoningProtocol.Profile]);
        using var handler = new HttpFixture(options, support, acknowledge: false);
        await using var connection = handler.Attach(options);
        if (!support) await Assert.ThrowsAsync<CompanionProfileException>(() => connection.Profile(default));
        else
        {
            await connection.Profile(default);
            await Assert.ThrowsAsync<CompanionProfileException>(() => connection.ClaimInference(default));
            Assert.Equal(1, handler.Claims);
        }
    }

    [Fact]
    public async Task QualifiedClientAssertsProfileOnClaimAndHeartbeatButNotFinish()
    {
        using var files = new LocalFiles(); var options = CompanionOptions.Parse([.. files.ReasonedArguments, "--thinking", "true", "--reasoning-profile", PairedReasoningProtocol.Profile]);
        using var handler = new HttpFixture(options, support: true, acknowledge: true);
        await using var connection = handler.Attach(options); await connection.Profile(default);
        var lease = (await connection.ClaimInference(default))!;
        await connection.HeartbeatInference(lease, default); await connection.FinishInference(lease, null, "MODEL_FAILED", default);
        Assert.Equal(2, handler.Assertions);
    }

    [Fact]
    public async Task LegacyClientActuallyDeserializesAdditiveProfileAndNeverAdvertisesPermission()
    {
        using var files = new LocalFiles(); var options = CompanionOptions.Parse(files.Arguments);
        using var handler = new HttpFixture(options, support: true, acknowledge: true);
        await using var connection = handler.Attach(options); var profile = await connection.Profile(default);
        Assert.Contains(PairedReasoningProtocol.Profile, profile.ReasoningContracts!);
        Assert.Null(await connection.ClaimInference(default)); Assert.Equal(0, handler.Assertions);
        // Exact old shape also remains accepted by the new DTO.
        var old = JsonSerializer.Deserialize<LocalCompanionProfile>("{\"connectionId\":\"" + Guid.NewGuid() + "\",\"profileId\":\"ollama-qwen3-8b-proposals\",\"model\":\"qwen3:8b\",\"workProtocol\":\"quickprompt-local-proposal/2\",\"dispatchReady\":true}", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Null(old!.ReasoningContracts);
        var oldDecoder = JsonSerializer.Deserialize<LegacyProfile>(JsonSerializer.Serialize(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(profile.ConnectionId, oldDecoder!.ConnectionId); Assert.Equal(profile.WorkProtocol, oldDecoder.WorkProtocol);
    }

    private sealed record LegacyProfile(Guid ConnectionId, string ProfileId, string Model, string WorkProtocol, bool DispatchReady);

    private sealed class HttpFixture(CompanionOptions options, bool support, bool acknowledge) : HttpMessageHandler
    {
        public int Claims { get; private set; }
        public int Assertions { get; private set; }
        public CompanionConnection Attach(CompanionOptions local)
        {
            var connection = new CompanionConnection(local, new CompanionSecrets(local));
            ((HttpClient)Field("http").GetValue(connection)!).Dispose();
            Field("http").SetValue(connection, new HttpClient(this, false) { BaseAddress = local.Origin });
            Field("accessToken").SetValue(connection, "synthetic-not-a-token"); Field("expiresAt").SetValue(connection, DateTimeOffset.MaxValue);
            return connection;
        }
        private static FieldInfo Field(string name) => typeof(CompanionConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("example.invalid", request.RequestUri!.Host);
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new LocalCompanionProfile(Guid.NewGuid(), options.Profile, "qwen3:8b", LocalProposalProtocol.Version, true,
                    support ? [PairedReasoningProtocol.Profile] : null)) });
            var exchange = request.RequestUri.AbsolutePath.EndsWith("/claim") || request.RequestUri.AbsolutePath.EndsWith("/heartbeat");
            if (exchange && options.ReasoningProfile is not null)
            {
                Assert.Equal(PairedReasoningProtocol.Profile, Assert.Single(request.Headers.GetValues(PairedReasoningProtocol.ProfileHeader)));
                Assertions++;
            }
            else Assert.False(request.Headers.Contains(PairedReasoningProtocol.ProfileHeader));
            HttpResponseMessage response;
            if (request.RequestUri.AbsolutePath.EndsWith("/claim"))
            {
                Claims++;
                response = options.ReasoningProfile is null ? new(HttpStatusCode.NoContent) : new(HttpStatusCode.OK) { Content = JsonContent.Create(new LocalProposalLease(Guid.NewGuid(), Guid.NewGuid(),
                    JsonSerializer.SerializeToElement(new { model = "qwen3:8b", stream = false, think = true, messages = new[] { new { role = "user", content = "Synthetic" } }, options = new { num_ctx = 12288, num_predict = 3500, temperature = 0 } }), 15, 90)) };
            }
            else response = request.RequestUri.AbsolutePath.EndsWith("/heartbeat") ? new(HttpStatusCode.OK) { Content = JsonContent.Create(new LocalLeasePulse(true, false)) } : new(HttpStatusCode.NoContent);
            if (exchange && acknowledge) response.Headers.Add(PairedReasoningProtocol.AcceptedHeader, PairedReasoningProtocol.Profile);
            return Task.FromResult(response);
        }
    }

    private sealed class LocalFiles : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "QuickPromptReasoning-" + Guid.NewGuid().ToString("N"));
        public string[] Arguments { get; }
        public string[] ReasonedArguments => Arguments.Select(x => x == "nova" ? "nova-reasoned" : x).ToArray();
        public LocalFiles()
        {
            Directory.CreateDirectory(Path.Combine(directory, "adaptive_core"));
            foreach (var file in new[] { "python.fixture", "worker.fixture", "adaptive_core/host.py", "adaptive_core/inference.py", "adaptive_core/dmr_framework.py" }) File.WriteAllText(Path.Combine(directory, file), "Synthetic: never execute");
            Arguments = ["run", "--server", "https://example.invalid/", "--capability", "nova", "--provider", "docker-model-runner", "--endpoint", "http://127.0.0.1:12434/", "--physical-model", PairedReasoningProtocol.PhysicalModel,
                "--python", Path.Combine(directory, "python.fixture"), "--worker", Path.Combine(directory, "worker.fixture"), "--sdk-root", directory];
        }
        public void Dispose()
        {
            var target = Path.GetFullPath(directory); var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || new DirectoryInfo(target).LinkTarget is not null) throw new IOException("Unexpected fixture cleanup target");
            Directory.Delete(target, true);
        }
    }
}
