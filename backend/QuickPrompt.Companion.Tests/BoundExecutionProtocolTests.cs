using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Companion.Tests;

/// <summary>Synthetic HTTP and model replies only; no OAuth, credentials, provider, process or live server.</summary>
public sealed class BoundExecutionProtocolTests
{
    [Fact]
    public void OperatorMustSelectV2ExplicitlyAndCannotApplyItToNova()
    {
        var legacy = CompanionOptions.Parse(["status", "--server", "https://example.invalid/"]);
        Assert.Equal(LocalWorkProtocol.Version, legacy.WorkProtocol);
        var requested = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--work-protocol", LocalWorkProtocol.BoundVersion]);
        Assert.Equal(LocalWorkProtocol.BoundVersion, requested.WorkProtocol);
        Assert.Equal(legacy.Scope, requested.Scope); Assert.Equal(legacy.Resource, requested.Resource);
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--work-protocol", "unknown"]));
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--capability", "nova", "--work-protocol", LocalWorkProtocol.BoundVersion]));
    }

    [Fact]
    public async Task ExplicitProfileNegotiationFailsBeforeClaimsWhenServerOnlyAdvertisesV1()
    {
        await using var fixture = new Fixture(LocalWorkProtocol.BoundVersion) { AdvertisedProtocol = LocalWorkProtocol.Version };
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.Connection.Profile(default));
        Assert.Equal(0, fixture.Claims); Assert.Empty(fixture.Receipts);
    }

    [Fact]
    public async Task V2RunnerProjectsContextOnceAndReturnsOnlyTheModelResponseThroughTheExistingReceipt()
    {
        await using var fixture = new Fixture(LocalWorkProtocol.BoundVersion);
        var profile = await fixture.Connection.Profile(default);
        Assert.Equal(LocalWorkProtocol.BoundVersion, profile.WorkProtocol);
        var model = new CaptureModel();
        Assert.Equal(CompanionRunEnd.JobLimit, await new CompanionRunner(fixture.Connection, model).Run(1, default));
        var packet = Assert.Single(model.Packets);
        Assert.True(JsonElement.DeepEquals(LocalExecutionFrame.Packet(fixture.Lease.Context!, fixture.Lease.Instruction), packet));
        var receipt = Assert.Single(fixture.Receipts);
        Assert.Equal(fixture.Lease.Token, receipt.GetProperty("token").GetGuid());
        Assert.Equal("SYNTHETIC_RESPONSE", receipt.GetProperty("response").GetString());
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("errorCode").ValueKind);
        Assert.Equal(1, fixture.Claims);
    }

    [Fact]
    public async Task DefaultV1StillSendsTheOriginalSingleMessageWithoutTheVersionHeader()
    {
        await using var fixture = new Fixture(LocalWorkProtocol.Version);
        var model = new CaptureModel();
        Assert.Equal(CompanionRunEnd.JobLimit, await new CompanionRunner(fixture.Connection, model).Run(1, default));
        Assert.Equal(fixture.Lease.Instruction, Assert.Single(model.Packets).GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal(1, Assert.Single(model.Packets).GetProperty("messages").GetArrayLength());
        Assert.False(Assert.Single(model.Packets).GetProperty("options").TryGetProperty("max_requests", out _));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("compilation")]
    [InlineData("unexpected-v1-context")]
    public async Task IncompatibleContextStopsBeforeAnyModelCall(string failure)
    {
        var protocol = failure == "unexpected-v1-context" ? LocalWorkProtocol.Version : LocalWorkProtocol.BoundVersion;
        await using var fixture = new Fixture(protocol);
        var context = LocalExecutionContext.Create("<x>", new Dictionary<string, string> { ["x"] = "Dato." });
        fixture.Lease = fixture.Lease with { Context = failure switch { "missing" => null, "hash" => context with { BasisHash = "changed" }, _ => context } };
        if (failure == "compilation") fixture.Lease = fixture.Lease with { Instruction = "Another value" };
        var model = new CaptureModel();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => new CompanionRunner(fixture.Connection, model).Run(1, default));
        Assert.Empty(model.Packets); Assert.Empty(fixture.Receipts); Assert.Equal(1, fixture.Claims);
    }

    private sealed class CaptureModel : IAgentModelTransport
    {
        public List<JsonElement> Packets { get; } = [];
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        {
            Packets.Add(JsonSerializer.SerializeToElement(packet));
            return Task.FromResult(JsonDocument.Parse("""{"message":{"content":"SYNTHETIC_RESPONSE"}}"""));
        }
    }

    private sealed class Fixture : HttpMessageHandler, IAsyncDisposable
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly string protocol;
        private readonly Guid connection = Guid.NewGuid();
        public CompanionConnection Connection { get; }
        public string AdvertisedProtocol { get; init; }
        public LocalWorkLease Lease { get; set; }
        public int Claims { get; private set; }
        public List<JsonElement> Receipts { get; } = [];
        public Fixture(string protocol)
        {
            this.protocol = protocol; AdvertisedProtocol = protocol;
            var options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N"), "--work-protocol", protocol]);
            Connection = new(options, new CompanionSecrets(options));
            ((HttpClient)Field("http").GetValue(Connection)!).Dispose();
            Field("http").SetValue(Connection, new HttpClient(this, false) { BaseAddress = options.Origin });
            Field("accessToken").SetValue(Connection, "synthetic-not-a-credential");
            Field("expiresAt").SetValue(Connection, DateTimeOffset.MaxValue);
            Lease = new(Guid.NewGuid(), Guid.NewGuid(), "Dato.", LocalWorkProtocol.Model, 30, 90) {
                Context = protocol == LocalWorkProtocol.BoundVersion ? LocalExecutionContext.Create("<x>", new Dictionary<string, string> { ["x"] = "Dato." }) : null
            };
        }
        private static FieldInfo Field(string name) => typeof(CompanionConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal("example.invalid", request.RequestUri!.Host);
            if (protocol == LocalWorkProtocol.Version) Assert.False(request.Headers.Contains(LocalWorkProtocol.VersionHeader));
            else Assert.Equal(protocol, Assert.Single(request.Headers.GetValues(LocalWorkProtocol.VersionHeader)));
            var path = request.RequestUri.AbsolutePath;
            if (path == "/api/companion/v1/profile") return Reply(new LocalCompanionProfile(connection, LocalWorkProtocol.Profile, LocalWorkProtocol.Model, AdvertisedProtocol, true));
            if (path.EndsWith("/claim", StringComparison.Ordinal)) { Claims++; return Reply(Lease); }
            if (path.EndsWith("/finish", StringComparison.Ordinal)) {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                Receipts.Add(body.RootElement.Clone()); return new(HttpStatusCode.NoContent);
            }
            if (path == CompanionPresenceProtocol.StopPath) return new(HttpStatusCode.NoContent);
            throw new InvalidOperationException("Unexpected synthetic route.");
        }
        private static HttpResponseMessage Reply(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };
        public async ValueTask DisposeAsync() { await Connection.DisposeAsync(); Dispose(); }
    }
}
