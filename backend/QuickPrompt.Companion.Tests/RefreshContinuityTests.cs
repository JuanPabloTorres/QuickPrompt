using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using OpenIddict.Abstractions;
using QuickPrompt.Nova.Application;
using Xunit;
using static OpenIddict.Client.OpenIddictClientModels;

namespace QuickPrompt.Companion.Tests;

/// <summary>Post-OAuth checkpoint with synthetic tokens, DPAPI and intercepted profile HTTP. No server, real grant or model.</summary>
public sealed class RefreshContinuityTests
{
    [Theory]
    [InlineData("quickprompt-local-proposal/1")]
    [InlineData("quickprompt-local-proposal/2")]
    public async Task KnownProposalVersionsCompleteTheSameApprovedPair(string protocol)
    {
        await using var fixture = new Fixture(null, protocol);
        fixture.PrepareInitialPair();
        await fixture.AcceptPair("synthetic-compatible-refresh");
        Assert.True(fixture.Secrets.Exists);
        Assert.False(File.Exists(fixture.PendingPath));
        Assert.Equal(fixture.Original.ConnectionId, fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile).ConnectionId);
        Assert.Equal(0, fixture.WorkCalls);
    }

    [Theory]
    [InlineData("quickprompt-local-proposal/0", "nova")]
    [InlineData("quickprompt-local-proposal/3", "nova")]
    [InlineData("quickprompt-local-proposal/1", "prompts")]
    public async Task UnknownOrOtherCapabilityVersionsCannotPromoteAPair(string protocol, string capability)
    {
        await using var fixture = new Fixture(null, protocol, capability);
        fixture.PrepareInitialPair();
        var error = await Record.ExceptionAsync(() => fixture.AcceptPair("synthetic-pending"));
        Assert.IsAssignableFrom<InvalidOperationException>(error);
        Assert.Equal("LOCAL_PROFILE_WRONGPROTOCOL", error!.Message);
        Assert.False(fixture.Secrets.Exists);
        Assert.True(File.Exists(fixture.PendingPath));
        Assert.Equal(0, fixture.WorkCalls);
    }

    [Theory]
    [InlineData("quickprompt-local-proposal/1", null, true)]
    [InlineData("quickprompt-local-proposal/1", 1, false)]
    [InlineData("quickprompt-local-proposal/1", 2, false)]
    [InlineData("quickprompt-local-proposal/2", null, true)]
    [InlineData("quickprompt-local-proposal/2", 1, true)]
    [InlineData("quickprompt-local-proposal/2", 2, true)]
    [InlineData("quickprompt-local-proposal/2", 3, false)]
    public async Task ClaimedPacketsRespectTheNegotiatedVersion(string protocol, int? maximumRequests, bool valid)
    {
        await using var fixture = new Fixture(null, protocol);
        await fixture.Accept("synthetic-compatible-refresh");
        var packet = System.Text.Json.Nodes.JsonNode.Parse("""
            {"model":"qwen3:8b","stream":false,"messages":[{"role":"user","content":"Ejemplo sintético"}],
             "options":{"num_ctx":8192,"num_predict":100,"temperature":0}}
            """)!;
        if (maximumRequests is { } limit) packet["options"]!["max_requests"] = limit;
        fixture.Packet = System.Text.Json.JsonSerializer.SerializeToElement(packet);
        if (valid) Assert.NotNull(await fixture.Claim());
        else await Assert.ThrowsAsync<QuickPrompt.Local.ValidationException>(() => fixture.Claim());
        Assert.Equal(1, fixture.WorkCalls); // Only a claim; no inference, heartbeat or finish.
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("cancel")]
    [InlineData("wrong-connection")]
    [InlineData("wrong-protocol")]
    [InlineData("wrong-capability")]
    [InlineData("wrong-model")]
    [InlineData("empty-connection")]
    [InlineData("not-ready")]
    public async Task AcceptedRotationSurvivesAProfileFailureWithoutGrantingWork(string failure)
    {
        await using var fixture = new Fixture(failure);
        var error = await Record.ExceptionAsync(() => fixture.Accept("synthetic-replacement"));
        switch (failure) {
            case "io": Assert.IsType<IOException>(error); break;
            case "timeout": Assert.IsAssignableFrom<OperationCanceledException>(error); break;
            case "cancel": Assert.IsAssignableFrom<OperationCanceledException>(error); break;
            default:
                Assert.IsAssignableFrom<InvalidOperationException>(error);
                Assert.StartsWith("LOCAL_PROFILE_", error!.Message);
                Assert.DoesNotContain("synthetic-private", error.Message);
                break;
        }
        var stored = fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile);
        Assert.Equal("synthetic-replacement", stored.RefreshToken);
        Assert.Equal(fixture.Original.ConnectionId, stored.ConnectionId);
        Assert.Equal(fixture.Original.Origin, stored.Origin);
        Assert.Equal(fixture.Original.ProfileId, stored.ProfileId);
        Assert.Equal(1, fixture.ProfileCalls);
        Assert.Equal(0, fixture.WorkCalls);
        Assert.DoesNotContain("synthetic-replacement", Encoding.UTF8.GetString(File.ReadAllBytes(fixture.SessionPath)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("synthetic-replacement")]
    public async Task ValidProfileKeepsTheExistingIdentityAndUsesTheReturnedTokenWhenPresent(string? refreshToken)
    {
        await using var fixture = new Fixture(null);
        await fixture.Accept(refreshToken);
        var stored = fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile);
        Assert.Equal(refreshToken ?? fixture.Original.RefreshToken, stored.RefreshToken);
        Assert.Equal(fixture.Original.ConnectionId, stored.ConnectionId);
        Assert.Equal(1, fixture.ProfileCalls);
        Assert.Equal(0, fixture.WorkCalls);
    }

    [Fact]
    public async Task FailedPrivateCheckpointStopsBeforeProfileOrWorkAndKeepsTheOldFile()
    {
        await using var fixture = new Fixture(null);
        File.SetAttributes(fixture.SessionPath, FileAttributes.ReadOnly);
        try {
            var error = await Record.ExceptionAsync(() => fixture.Accept("synthetic-replacement"));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.Equal(0, fixture.ProfileCalls);
            Assert.Equal(0, fixture.WorkCalls);
            Assert.Equal(fixture.Original.RefreshToken, fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile).RefreshToken);
        } finally { File.SetAttributes(fixture.SessionPath, FileAttributes.Normal); }
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("wrong-protocol")]
    public async Task ApprovedPairSurvivesProfileFailureThenPromotesOnlyAfterAValidProfile(string failure)
    {
        await using var fixture = new Fixture(failure);
        fixture.PrepareInitialPair();
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.AcceptPair("synthetic-pending-1")));
        Assert.False(fixture.Secrets.Exists);
        Assert.True(File.Exists(fixture.PendingPath));
        Assert.Equal("synthetic-pending-1", fixture.ReadPending());
        Assert.DoesNotContain("synthetic-pending-1", Encoding.UTF8.GetString(File.ReadAllBytes(fixture.PendingPath)));
        Assert.Equal(0, fixture.WorkCalls);

        fixture.AllowProfile();
        await fixture.AcceptPair("synthetic-pending-2");
        var stored = fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile);
        Assert.Equal("synthetic-pending-2", stored.RefreshToken);
        Assert.Equal(fixture.Original.ConnectionId, stored.ConnectionId);
        Assert.False(File.Exists(fixture.PendingPath));
        Assert.Equal(2, fixture.ProfileCalls);
        Assert.Equal(0, fixture.WorkCalls);
    }

    [Fact]
    public async Task ForgetRemovesAnApprovedButUnusablePendingGrantAsWellAsACompleteSession()
    {
        await using var fixture = new Fixture("io");
        fixture.PrepareInitialPair();
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.AcceptPair("synthetic-pending")));
        Assert.True(File.Exists(fixture.PendingPath));
        fixture.Secrets.Forget();
        Assert.False(File.Exists(fixture.PendingPath));
        Assert.False(fixture.Secrets.Exists);
    }

    [Fact]
    public async Task PendingCredentialsCannotBeLoadedAsAValidatedSessionOrAnotherCapability()
    {
        await using var fixture = new Fixture("io");
        fixture.PrepareInitialPair();
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.AcceptPair("synthetic-pending")));
        File.Copy(fixture.PendingPath, fixture.SessionPath);
        Assert.Throws<CryptographicException>(() => fixture.Secrets.Load(fixture.Options.Origin.AbsoluteUri, fixture.Options.Profile));
        var other = new CompanionSecrets(fixture.Options with { Profile = LocalWorkProtocol.Profile });
        var method = typeof(CompanionSecrets).GetMethod("LoadPendingPair", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(other, [fixture.Options.Origin.AbsoluteUri]));
        Assert.IsType<CryptographicException>(error.InnerException);
    }

    [Fact]
    public async Task PendingCredentialsRemainBoundToTheirOrigin()
    {
        await using var fixture = new Fixture("io");
        fixture.PrepareInitialPair();
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.AcceptPair("synthetic-pending")));
        var method = typeof(CompanionSecrets).GetMethod("LoadPendingPair", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(fixture.Secrets, ["https://other.example.invalid/"]));
        Assert.IsType<CryptographicException>(error.InnerException);
        Assert.False(fixture.Secrets.Exists);
    }

    private sealed class Fixture : HttpMessageHandler, IAsyncDisposable
    {
        public CompanionOptions Options { get; }
        public CompanionSecrets Secrets { get; }
        public CompanionSession Original { get; }
        public string SessionPath => Path.Combine(directory, "session.dpapi");
        public string PendingPath => Path.Combine(directory, "pending-pair.dpapi");
        public int ProfileCalls { get; private set; }
        public int WorkCalls { get; private set; }
        private readonly string directory;
        private string? failure;
        private readonly CompanionConnection connection;
        private string expectedRefresh = "synthetic-old-refresh";
        private bool initialPair;
        private readonly string? protocol;
        public System.Text.Json.JsonElement? Packet { get; set; }

        public Fixture(string? failure, string? protocol = null, string capability = "nova")
        {
            this.failure = failure;
            this.protocol = protocol;
            Options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N"), "--capability", capability]);
            Secrets = new(Options);
            Original = new(Options.Origin.AbsoluteUri, Guid.NewGuid(), "synthetic-old-refresh", Options.Profile);
            Secrets.Save(Original);
            var purpose = SHA256.HashData(Encoding.UTF8.GetBytes("QuickPrompt.Companion/1|" + Options.Origin.AbsoluteUri + "|" + Options.Slot));
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion", Convert.ToHexString(purpose));
            connection = new(Options, Secrets);
            var http = Field("http");
            ((HttpClient)http.GetValue(connection)!).Dispose();
            http.SetValue(connection, new HttpClient(this, false) { BaseAddress = Options.Origin });
            Field("session").SetValue(connection, Original);
        }

        public Task Accept(string? refreshToken)
        {
            expectedRefresh = refreshToken ?? Original.RefreshToken;
            var result = new RefreshTokenAuthenticationResult {
                AccessToken = "synthetic-access", AccessTokenExpirationDate = DateTimeOffset.UtcNow.AddMinutes(5),
                RefreshToken = refreshToken, Principal = new ClaimsPrincipal(), Properties = [], TokenResponse = new OpenIddictResponse(),
                IdentityToken = null, IdentityTokenPrincipal = null, UserInfoToken = null, UserInfoTokenPrincipal = null
            };
            return (Task)typeof(CompanionConnection).GetMethod("AcceptRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(connection, [result, CancellationToken.None])!;
        }

        public void PrepareInitialPair()
        {
            Secrets.Forget();
            Field("session").SetValue(connection, null);
            initialPair = true;
        }
        public void AllowProfile() => failure = null;
        public Task<LocalProposalLease?> Claim() => (Task<LocalProposalLease?>)typeof(CompanionConnection)
            .GetMethod("ClaimInference", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(connection, [CancellationToken.None])!;
        public string ReadPending() => (string)typeof(CompanionSecrets).GetMethod("LoadPendingPair", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Secrets, [Options.Origin.AbsoluteUri])!;
        public Task AcceptPair(string refreshToken)
        {
            expectedRefresh = refreshToken;
            return (Task)typeof(CompanionConnection).GetMethod("AcceptPair", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(connection, ["synthetic-access", DateTimeOffset.UtcNow.AddMinutes(5), refreshToken, CancellationToken.None])!;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("example.invalid", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath != "/api/companion/v1/profile") {
                WorkCalls++;
                Assert.Equal("/api/companion/v1/proposals/claim", request.RequestUri.AbsolutePath);
                Assert.NotNull(Packet);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = JsonContent.Create(new LocalProposalLease(Guid.NewGuid(), Guid.NewGuid(), Packet.Value, 30, 60))
                });
            }
            ProfileCalls++;
            Assert.Equal("synthetic-access", request.Headers.Authorization!.Parameter);
            // The issuer has already consumed the old token. Check the disk
            // checkpoint at the first dependent HTTP boundary, even on success.
            if (initialPair) {
                Assert.False(Secrets.Exists); // Pending approval data is never a usable session.
                Assert.Equal(expectedRefresh, ReadPending());
            } else Assert.Equal(expectedRefresh, Secrets.Load(Options.Origin.AbsoluteUri, Options.Profile).RefreshToken);
            if (failure == "io") throw new IOException("Synthetic interrupted profile response.");
            if (failure == "timeout") throw new TaskCanceledException("Synthetic profile timeout.");
            if (failure == "cancel") throw new OperationCanceledException();
            var id = failure == "empty-connection" ? Guid.Empty : failure == "wrong-connection" ? Guid.NewGuid() : Original.ConnectionId;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = JsonContent.Create(new LocalCompanionProfile(id,
                    failure == "wrong-capability" ? "synthetic-private-capability" : Options.Profile,
                    failure == "wrong-model" ? "synthetic-private-model" : LocalWorkProtocol.Model,
                    failure == "wrong-protocol" ? "synthetic-private-protocol" : protocol ?? LocalCompanionProfiles.Protocol(Options.Profile),
                    failure != "not-ready"))
            });
        }

        private static FieldInfo Field(string name) => typeof(CompanionConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            var parent = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion"));
            var target = Path.GetFullPath(directory);
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || new DirectoryInfo(target).LinkTarget is not null)
                throw new IOException("Unexpected fixture cleanup target.");
            Directory.Delete(target, true);
            Dispose();
        }
    }
}
