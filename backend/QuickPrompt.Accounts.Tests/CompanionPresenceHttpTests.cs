using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static async Task<ConnectionView> PresenceConnection(HttpClient owner, Guid id) =>
        Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!, item => item.Id == id);

    private static Guid PresenceObservation(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return Guid.Parse(Assert.Single(response.Headers.GetValues(CompanionPresenceProtocol.Header)));
    }

    private static async Task<bool> WithdrawPresence(HttpClient device, Guid observation)
    {
        using var response = await device.PostAsJsonAsync(CompanionPresenceProtocol.StopPath, new LocalPresenceStop(observation));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stopped").GetBoolean();
    }

    private async Task<(long Runs, long Jobs)> PresenceAdmissionCounts(Guid actor)
    {
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT set_config('quickprompt.actor_id',@actor,true);
            SELECT (SELECT count(*) FROM qp_operations.agent_runs WHERE "OwnerId"=@owner),
                   (SELECT count(*) FROM qp_operations.executions WHERE "OwnerId"=@owner);
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", actor.ToString()); command.Parameters.AddWithValue("owner", actor);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.NextResultAsync(); Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task GrantAndProfileAreNotPresenceAndAnOfflineNovaStartConsumesNoAdmission()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var actor = (await owner.GetFromJsonAsync<JsonElement>("/account/me")).GetProperty("id").GetGuid();
        var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        Assert.True(grant.Connection.Active);
        Assert.Equal("NOT_OBSERVED", (await PresenceConnection(owner, grant.Connection.Id)).Presence!.Status);
        (await device.GetAsync("/api/companion/v1/profile")).EnsureSuccessStatusCode();
        Assert.Equal("NOT_OBSERVED", (await PresenceConnection(owner, grant.Connection.Id)).Presence!.Status);
        VisibleSurface(owner, new string('a', 64)); var request = OpenVisible(grant.Connection.Id, source.Prompt);
        var before = await PresenceAdmissionCounts(actor);
        using var unavailable = await owner.PostAsJsonAsync(VisiblePath(source.Library) + "/live", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("señal reciente", await unavailable.Content.ReadAsStringAsync());
        Assert.Equal(before, await PresenceAdmissionCounts(actor));
        // This fixture now actually polls. Pairing alone was deliberately insufficient.
        using var poll = await ClaimProposal(device); var observation = PresenceObservation(poll);
        Assert.Equal("POLLING", (await PresenceConnection(owner, grant.Connection.Id)).Presence!.Status);
        (await owner.PostAsJsonAsync(VisiblePath(source.Library) + "/live", request)).EnsureSuccessStatusCode();
        var completed = await DriveVisibleRead(owner, source.Library, request.Request.RunId, source.Prompt);
        Assert.Equal(LiveStatus.COMPLETED, completed.Run.Status);
        Assert.True(await WithdrawPresence(device, observation));
        Assert.Equal("STOPPED", (await PresenceConnection(owner, grant.Connection.Id)).Presence!.Status);
        var admitted = await PresenceAdmissionCounts(actor);
        // A stopped runner does not invalidate an existing receipt or start another task.
        (await owner.PostAsJsonAsync(VisiblePath(source.Library) + "/live", request)).EnsureSuccessStatusCode();
        Assert.Equal(admitted, await PresenceAdmissionCounts(actor));
        using var newRequest = await owner.PostAsJsonAsync(VisiblePath(source.Library) + "/live", request with { Request = request.Request with { RunId = Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, newRequest.StatusCode);
        Assert.Equal(admitted, await PresenceAdmissionCounts(actor));
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task PresenceWithdrawalIsOwnerBoundAndAnOldObservationCannotStopANewerPoll()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var other = Client(host.App);
        using var device = Client(host.App); using var foreignDevice = Client(host.App); using var anonymous = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var own = await ProposalDevice(owner, device); await ProposalDevice(other, foreignDevice);
        using var first = await ClaimProposal(device); var old = PresenceObservation(first);
        using var second = await ClaimProposal(device); var latest = PresenceObservation(second);
        Assert.NotEqual(old, latest);
        Assert.False(await WithdrawPresence(foreignDevice, latest));
        Assert.False(await WithdrawPresence(device, old));
        Assert.Equal("POLLING", (await PresenceConnection(owner, own.Connection.Id)).Presence!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(CompanionPresenceProtocol.StopPath, new LocalPresenceStop(latest))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync(CompanionPresenceProtocol.StopPath, new LocalPresenceStop(latest))).StatusCode);
        Assert.True(await WithdrawPresence(device, latest)); Assert.True(await WithdrawPresence(device, latest));
        Assert.Equal("STOPPED", (await PresenceConnection(owner, own.Connection.Id)).Presence!.Status);
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task StalePresenceExpiresWithoutRevokingTheGrantAndANewPollRecoversIt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var grant = await ProposalDevice(owner, device);
        using var first = await ClaimProposal(device); var old = PresenceObservation(first);
        await using (var connection = new NpgsqlConnection(postgres.MigrationConnection))
        {
            await connection.OpenAsync(); await using var expire = new NpgsqlCommand("""
                UPDATE qp_operations.companion_presence SET "ObservedAt"=clock_timestamp()-interval '21 seconds',
                    "ExpiresAt"=clock_timestamp()-interval '2 seconds' WHERE "ConnectionId"=@id
                """, connection);
            expire.Parameters.AddWithValue("id", grant.Connection.Id); Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        var stale = await PresenceConnection(owner, grant.Connection.Id);
        Assert.True(stale.Active); Assert.Equal("STALE", stale.Presence!.Status);
        using var recovered = await ClaimProposal(device); PresenceObservation(recovered);
        Assert.False(await WithdrawPresence(device, old));
        Assert.Equal("POLLING", (await PresenceConnection(owner, grant.Connection.Id)).Presence!.Status);
        (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).EnsureSuccessStatusCode();
        Assert.False((await PresenceConnection(owner, grant.Connection.Id)).Active);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClaimProposal(device)).StatusCode);
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task ALocalExecutionRequiresItsOwnPollAndOfflineReplayKeepsTheOriginalReceipt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await LocalDevice(owner, device);
        var input = LocalInput(grant.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input)).StatusCode);
        using var poll = await ClaimLocal(device); var observation = PresenceObservation(poll);
        using var admitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var receipt = (await admitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.True(await WithdrawPresence(device, observation));
        using var replay = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(receipt.Id, (await replay.Content.ReadFromJsonAsync<ExecutionView>())!.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input with { OperationId = Guid.NewGuid() })).StatusCode);
        (await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{receipt.Id}/cancel", new { })).EnsureSuccessStatusCode();
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task AnExecutionPollDoesNotAdvertisePresenceForTheSeparateNovaCapability()
    {
        await using var host = await Start(); using var owner = Client(host.App);
        using var prompts = Client(host.App); using var nova = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var workGrant = await LocalDevice(owner, prompts); var proposalGrant = await ProposalDevice(owner, nova);
        using var poll = await ClaimLocal(prompts); PresenceObservation(poll);
        Assert.Equal("POLLING", (await PresenceConnection(owner, workGrant.Id)).Presence!.Status);
        Assert.Equal("NOT_OBSERVED", (await PresenceConnection(owner, proposalGrant.Connection.Id)).Presence!.Status);
        VisibleSurface(owner, new string('b', 64));
        using var rejected = await owner.PostAsJsonAsync(VisiblePath(source.Library) + "/live", OpenVisible(proposalGrant.Connection.Id, source.Prompt));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
    }

    [Fact, Trait("Category", "CompanionPresence")]
    public async Task OnlyAnOwnedLeaseHeartbeatRefreshesWorkingPresenceWithoutExecutingAModel()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await LocalDevice(owner, device);
        using var poll = await ClaimLocal(device); PresenceObservation(poll);
        (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(grant.Id))).EnsureSuccessStatusCode();
        using var claim = await ClaimLocal(device); Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var lease = (await claim.Content.ReadFromJsonAsync<LocalWorkLease>())!;
        var observation = Guid.Parse(Assert.Single(claim.Headers.GetValues(CompanionPresenceProtocol.Header)));
        Assert.Equal("WORKING", (await PresenceConnection(owner, grant.Id)).Presence!.Status);
        using var heartbeat = await device.PostAsJsonAsync($"/api/companion/v1/work/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token));
        heartbeat.EnsureSuccessStatusCode(); Assert.True((await heartbeat.Content.ReadFromJsonAsync<LeasePulse>())!.Owned);
        var latest = Guid.Parse(Assert.Single(heartbeat.Headers.GetValues(CompanionPresenceProtocol.Header)));
        Assert.NotEqual(observation, latest); Assert.False(await WithdrawPresence(device, observation));
        // A synthetic cancellation completes this lease; no provider or subprocess is called.
        (await device.PostAsJsonAsync($"/api/companion/v1/work/{lease.Id}/finish", new LocalWorkReceipt(lease.Token, null, "LOCAL_STOPPED"))).EnsureSuccessStatusCode();
        Assert.True(await WithdrawPresence(device, latest));
    }
}
