using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task PendingDeniedAndCsrfRequestsNeverCreateConsent()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var pending = await BeginDevice(device);
        Assert.False((await Token(device, "urn:ietf:params:oauth:grant-type:device_code", "device_code", pending.DeviceCode)).IsSuccessStatusCode);
        owner.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyDevice(owner, pending, "approve")).StatusCode);
        await Csrf(device);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyDevice(device, pending, "approve")).StatusCode);
        await Csrf(owner);
        Assert.False((await VerifyDevice(owner, pending, "deny")).IsSuccessStatusCode);
        Assert.False((await VerifyDevice(owner, pending, "approve")).IsSuccessStatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }

    [Theory]
    [InlineData("client_id", "unregistered")]
    [InlineData("scope", "companion:work library:write")]
    [InlineData("profile_id", "arbitrary-shell")]
    [InlineData("resource", "https://unrelated.example.invalid/api/companion")]
    [InlineData("device_name", "")]
    public async Task DeviceRequestCannotBroadenItsCapabilities(string field, string value)
    {
        await using var host = await Start(); using var client = Client(host.App);
        var input = new Dictionary<string, string> { ["client_id"] = DelegationPolicy.ClientId,
            ["scope"] = "companion:work offline_access", ["profile_id"] = DelegationPolicy.Profile,
            ["resource"] = new Uri(client.BaseAddress!, "/api/companion").AbsoluteUri, ["device_name"] = "QA local" };
        input[field] = value;
        Assert.False((await client.PostAsync("/connect/device", new FormUrlEncodedContent(input))).IsSuccessStatusCode);
        await BeginDevice(client); // Rejection must not poison a subsequent valid request.
    }

    [Theory]
    [InlineData(LocalWorkProtocol.Profile)]
    [InlineData(PairedReasoningProtocol.GrantProfile)]
    public async Task DeviceCodeIsSingleUseAndRevocationReachesAnotherHost(string profile)
    {
        await using var first = await Start(); await using var second = await Start();
        using var owner = Client(first.App); using var secondOwner = Client(second.App);
        using var other = Client(first.App); using var device = Client(second.App);
        var email = await Register(owner, first.Mail); await Login(owner, email); await Csrf(secondOwner); await Login(secondOwner, email);
        await Login(other, await Register(other, first.Mail));
        var pending = await BeginDevice(device, profile: profile);
        await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve on first host");
        var token = await RedeemDevice(device, pending, second.ProtocolLog, profile: profile);
        var refreshed = await Token(device, "refresh_token", "refresh_token", token.RefreshToken, profile);
        await AssertProtocolSuccess(refreshed, "Refresh on another host");
        var renewed = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        // The cookie on the other instance sees the same grant, then revokes it.
        var connection = Assert.Single((await secondOwner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        await using var scope = first.App.Services.CreateAsyncScope();
        Func<Task>? rejectStaleAdmission = null;
        if (profile == PairedReasoningProtocol.GrantProfile)
        {
            // Only the second host observed the local flag. The first host
            // reads the persisted profile without any process-local state.
            AssertLocalReasoning(device); using var poll = await ClaimProposal(device);
            Assert.Equal(HttpStatusCode.NoContent, poll.StatusCode);
            var source = await LocalLibrary(owner); var actor = await ProposalActor(owner);
            var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
            var port = proposals.ForConnection(actor, source.Library, connection.Id); Assert.True(port.UsesThinking("qwen3:8b"));
            var job = await proposals.Submit(actor, source.Library, connection.Id, Guid.NewGuid(), ThinkingProposalPacket());
            var lease = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
            Assert.Equal(job.Id, lease.Id);
            Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, ProposalReply())).StatusCode);
            var before = await PresenceAdmissionCounts(actor.Id);
            rejectStaleAdmission = async () =>
            {
                await Assert.ThrowsAsync<LibraryUnavailableException>(() => port.Send(ThinkingProposalPacket(), default, "two-host-revocation"));
                Assert.Equal(before, await PresenceAdmissionCounts(actor.Id));
            };
        }
        Assert.Equal(HttpStatusCode.NoContent, (await secondOwner.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        using var firstDevice = Client(first.App); firstDevice.DefaultRequestHeaders.Authorization = device.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstDevice.GetAsync("/api/companion/v1/profile")).StatusCode);
        if (rejectStaleAdmission is not null) await rejectStaleAdmission();
        Assert.False((await Token(device, "refresh_token", "refresh_token", renewed.GetProperty("refresh_token").GetString()!, profile)).IsSuccessStatusCode);
        Assert.False((await Token(device, "urn:ietf:params:oauth:grant-type:device_code", "device_code", pending.DeviceCode, profile)).IsSuccessStatusCode);
        Assert.False((await VerifyDevice(other, pending, "approve")).IsSuccessStatusCode);
        Assert.Empty((await other.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }

    [Fact]
    public async Task LogoutAllInvalidatesDelegatedAccessAndItsDisplayedStatus()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        var email = await Register(owner, host.Mail); await Login(owner, email);
        var pending = await BeginDevice(device);
        await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve before logout-all");
        var token = await RedeemDevice(device, pending, host.ProtocolLog);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/account/logout-all", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.False((await Token(device, "refresh_token", "refresh_token", token.RefreshToken)).IsSuccessStatusCode);
        await Csrf(owner); await Login(owner, email);
        Assert.False(Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!).Active);
    }

    [Fact]
    public async Task AccountsRemainUsableWhenDelegationIsExplicitlyDisabled()
    {
        await using var host = await Start(delegation: false); using var owner = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/account/me")).StatusCode);
        var options = await owner.GetFromJsonAsync<JsonElement>("/account/options");
        Assert.False(options.GetProperty("delegation").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync("/connect/device", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/companion/v1/profile")).StatusCode);
    }

    [Fact]
    public async Task CachedProtocolAuthorizationCannotPreserveARevokedDatabaseGrant()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var pending = await BeginDevice(device);
        await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve before external revocation");
        var token = await RedeemDevice(device, pending);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        var account = await owner.GetFromJsonAsync<JsonElement>("/account/me");
        var grant = Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        await using (var database = await postgres.Application.OpenConnectionAsync())
        {
            await using var transaction = await database.BeginTransactionAsync();
            await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", database, transaction);
            actor.Parameters.AddWithValue("actor", account.GetProperty("id").GetGuid().ToString()); await actor.ExecuteScalarAsync();
            await using var revoke = new NpgsqlCommand("""
                UPDATE qp_identity."OpenIddictAuthorizations" SET "Status"='revoked'
                WHERE "Id"=(SELECT "AuthorizationId" FROM qp_operations.connections WHERE "Id"=@id)
                """, database, transaction);
            revoke.Parameters.AddWithValue("id", grant.Id); Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
            await transaction.CommitAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.False(Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!).Active);
    }
}
