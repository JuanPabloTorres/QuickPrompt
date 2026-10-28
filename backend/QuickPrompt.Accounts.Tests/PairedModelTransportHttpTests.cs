using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static async Task<LocalProposalLease> AwaitProposal(HttpClient device, Task? pending = null)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (pending?.IsCompleted == true)
            {
                await pending;
                throw new InvalidOperationException("The model call finished before its device could claim a lease.");
            }
            using var claim = await ClaimProposal(device);
            if (claim.StatusCode == HttpStatusCode.OK) return (await claim.Content.ReadFromJsonAsync<LocalProposalLease>())!;
            Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
            await Task.Delay(50);
        }
        throw new TimeoutException("The paired transport did not stage its private proposal.");
    }
    private static Task<HttpResponseMessage> FinishProposal(HttpClient device, LocalProposalLease lease, JsonElement? reply, string? error = null) =>
        device.PostAsJsonAsync($"/api/companion/v1/proposals/{lease.Id}/finish", new LocalProposalReceipt(lease.Token, reply, error));

    [Fact]
    public async Task PairedModelPortsIndependentlyCompleteForTwoAccountsWithoutLibraryEffects()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var libraryA = await LocalLibrary(a); var libraryB = await LocalLibrary(b);
        var grantA = await ProposalDevice(a, deviceA); var grantB = await ProposalDevice(b, deviceB);
        var actorA = await ProposalActor(a); var actorB = await ProposalActor(b);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var portA = proposals.ForConnection(actorA, libraryA.Library, grantA.Connection.Id);
        var portB = proposals.ForConnection(actorB, libraryB.Library, grantB.Connection.Id);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var packetA = ProposalPacket();
        // The existing model port accepts typed objects with Web naming as well as
        // already encoded JsonElements. Changing adapters must preserve that codec.
        var typedPacket = new
        {
            Model = "qwen3:8b",
            Stream = false,
            Think = false,
            Messages = new[] { new { Role = "user", Content = "Caso sintético A." } },
            Options = packetA.GetProperty("options"),
            Format = packetA.GetProperty("format")
        };
        var requestA = portA.Send(typedPacket, lifetime.Token, "test-a");
        var requestB = portB.Send(ProposalPacket(), lifetime.Token, "test-b");
        var leaseA = await AwaitProposal(deviceA); var leaseB = await AwaitProposal(deviceB);
        Assert.NotEqual(leaseA.Id, leaseB.Id); Assert.False(requestA.IsCompleted); Assert.False(requestB.IsCompleted);
        var replyB = ProposalReply();
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(deviceB, leaseB, replyB)).StatusCode);
        using var resultB = await requestB; Assert.Equal("qwen3:8b", resultB.RootElement.GetProperty("model").GetString());
        Assert.True(JsonElement.DeepEquals(replyB, resultB.RootElement));
        Assert.False(requestA.IsCompleted);
        var replyA = ProposalReply();
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(deviceA, leaseA, replyA)).StatusCode);
        using var resultA = await requestA;
        Assert.True(JsonElement.DeepEquals(replyA, resultA.RootElement));
        Assert.NotEqual(resultA.RootElement.GetProperty("coreRequestId").GetString(), resultB.RootElement.GetProperty("coreRequestId").GetString());
        Assert.Equal(libraryA.Prompt, await a.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{libraryA.Library}/prompts/{libraryA.Prompt.Id}"));
        Assert.Equal(libraryB.Prompt, await b.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{libraryB.Library}/prompts/{libraryB.Prompt.Id}"));
        Assert.True(portA.UsesThinking("qwen3:8b")); Assert.Throws<QuickPrompt.Local.ValidationException>(() => portA.UsesThinking("other-model"));
    }

    [Fact]
    public async Task CancellingTheModelCallRequestsCancellationAndRejectsALateProposal()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var port = scope.ServiceProvider.GetRequiredService<AccountModelProposals>().ForConnection(actor, source.Library, grant.Connection.Id);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var inference = port.Send(ProposalPacket(), stop.Token, "test-cancel");
        var lease = await AwaitProposal(device);
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inference);
        var current = await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{lease.Id}");
        Assert.True(current!.CancelRequested);
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, ProposalReply())).StatusCode);
        Assert.Equal(0L, await ProposalRows(actor.Id, lease.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderFailureOrRevocationTerminatesTheModelCallWithoutRetry(bool revoke)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var port = proposals.ForConnection(actor, source.Library, grant.Connection.Id);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var inference = port.Send(ProposalPacket(), stop.Token, "test-failure");
        var lease = await AwaitProposal(device);
        if (revoke)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).StatusCode);
            var failure = await Assert.ThrowsAsync<LlmUnavailableException>(() => inference);
            Assert.Equal("CONNECTION_REVOKED", failure.Code);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, null, "MODEL_UNAVAILABLE")).StatusCode);
            var failure = await Assert.ThrowsAsync<LlmUnavailableException>(() => inference); Assert.Contains("Ollama", failure.Message);
            Assert.Equal("MODEL_UNAVAILABLE", failure.Code);
            Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        }
        Assert.Equal(0L, await ProposalRows(actor.Id, lease.Id));
    }

    [Fact]
    public async Task AModelPortWithAnIneligibleLibraryOrGrantCannotAdmitWork()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var device = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await LocalLibrary(a); var foreign = await LocalLibrary(b); var grant = await ProposalDevice(a, device);
        var actor = await ProposalActor(a); var other = await ProposalActor(b);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.ForConnection(other, foreign.Library, grant.Connection.Id).Send(ProposalPacket(), CancellationToken.None, "wrong-grant"));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.ForConnection(actor, foreign.Library, grant.Connection.Id).Send(ProposalPacket(), CancellationToken.None, "wrong-library"));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<ExecutionView[]>($"/api/accounts/v1/libraries/{library.Library}/executions"))!);
    }

    [Fact]
    public async Task AnAbsentDeviceCannotLeaveTheCallerWaitingWithoutABound()
    {
        await using var host = await Start(configureServices: services => services.AddSingleton(new ExecutionLimits(MaximumSeconds: 1)));
        using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var port = proposals.ForConnection(actor, source.Library, grant.Connection.Id);
        var failure = await Assert.ThrowsAsync<LlmUnavailableException>(() => port.Send(ProposalPacket(), CancellationToken.None, "absent-device").WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Contains("plazo", failure.Message);
        Assert.Equal("MODEL_TIMEOUT", failure.Code);
        // No device claimed the request. Dispatch cleanup consumes the pending cancellation,
        // rather than picking up a job whose caller has already stopped waiting.
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }
}
