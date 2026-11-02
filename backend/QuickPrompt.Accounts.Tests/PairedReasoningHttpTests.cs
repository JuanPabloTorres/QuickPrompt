using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static JsonElement ThinkingProposalPacket()
    {
        var packet = JsonNode.Parse(ProposalPacket().GetRawText())!;
        packet["think"] = true; packet["options"]!["num_ctx"] = 12288;
        return JsonSerializer.SerializeToElement(packet);
    }

    private static async Task<(ConnectionView Connection, TokenDocument Token)> ReasonedDevice(HttpClient owner, HttpClient device)
    {
        var pending = await BeginDevice(device, profile: PairedReasoningProtocol.GrantProfile);
        using var review = await VerifyDevice(owner, pending, "review"); await AssertProtocolSuccess(review, "Review explicit reasoning consent");
        var shown = await review.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PairedReasoningProtocol.GrantProfile, shown.GetProperty("profileId").GetString());
        Assert.Equal(3, shown.GetProperty("permissions").GetArrayLength());
        Assert.Contains("operador", shown.GetProperty("permissions")[2].GetString());
        using var approval = await VerifyDevice(owner, pending, "approve"); await AssertProtocolSuccess(approval, "Approve explicit reasoning profile");
        var token = await RedeemDevice(device, pending, profile: PairedReasoningProtocol.GrantProfile);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var profile = (await device.GetFromJsonAsync<LocalCompanionProfile>("/api/companion/v1/profile"))!;
        Assert.Equal(LocalProposalProtocol.Version, profile.WorkProtocol);
        Assert.Equal(PairedReasoningProtocol.Profile, Assert.Single(profile.ReasoningContracts!));
        return ((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!.Single(x => x.Id == profile.ConnectionId), token);
    }

    private static void AssertLocalReasoning(HttpClient device)
    {
        device.DefaultRequestHeaders.Remove(PairedReasoningProtocol.ProfileHeader);
        device.DefaultRequestHeaders.Add(PairedReasoningProtocol.ProfileHeader, PairedReasoningProtocol.Profile);
    }

    [Fact]
    public async Task ExplicitPersistedReasoningGrantAndLocalHeaderAreBothRequiredBeforeAnyLease()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App); using var legacy = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var oldGrant = await PollingProposalDevice(owner, legacy); var grant = await ReasonedDevice(owner, device); var actor = await ProposalActor(owner);
        var oldProfile = (await legacy.GetFromJsonAsync<LocalCompanionProfile>("/api/companion/v1/profile"))!;
        Assert.Empty(oldProfile.ReasoningContracts!);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimProposal(device)).StatusCode);
        AssertLocalReasoning(legacy);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimProposal(legacy)).StatusCode);
        legacy.DefaultRequestHeaders.Remove(PairedReasoningProtocol.ProfileHeader);
        AssertLocalReasoning(device);
        using var poll = await ClaimProposal(device);
        Assert.Equal(HttpStatusCode.NoContent, poll.StatusCode);
        Assert.Equal(PairedReasoningProtocol.Profile, Assert.Single(poll.Headers.GetValues(PairedReasoningProtocol.AcceptedHeader)));
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        Assert.False(proposals.ForConnection(actor, source.Library, oldGrant.Connection.Id).UsesThinking("qwen3:8b"));
        Assert.True(proposals.ForConnection(actor, source.Library, grant.Connection.Id).UsesThinking("qwen3:8b"));
        var before = await PresenceAdmissionCounts(actor.Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(actor, source.Library, oldGrant.Connection.Id, Guid.NewGuid(), ThinkingProposalPacket()));
        Assert.Equal(before, await PresenceAdmissionCounts(actor.Id));
        var job = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ThinkingProposalPacket());
        device.DefaultRequestHeaders.Remove(PairedReasoningProtocol.ProfileHeader);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimProposal(device)).StatusCode);
        device.DefaultRequestHeaders.Add(PairedReasoningProtocol.ProfileHeader, [PairedReasoningProtocol.Profile, PairedReasoningProtocol.Profile]);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimProposal(device)).StatusCode);
        AssertLocalReasoning(device);
        var lease = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        Assert.Equal(job.Id, lease.Id); Assert.True(lease.Packet.GetProperty("think").GetBoolean());
        device.DefaultRequestHeaders.Remove(PairedReasoningProtocol.ProfileHeader);
        Assert.Equal(HttpStatusCode.BadRequest, (await device.PostAsJsonAsync($"/api/companion/v1/proposals/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token))).StatusCode);
        AssertLocalReasoning(device);
        using var pulse = await device.PostAsJsonAsync($"/api/companion/v1/proposals/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token));
        Assert.True((await pulse.Content.ReadFromJsonAsync<LocalLeasePulse>())!.Owned);
        Assert.Equal(PairedReasoningProtocol.Profile, Assert.Single(pulse.Headers.GetValues(PairedReasoningProtocol.AcceptedHeader)));
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, ProposalReply())).StatusCode);
        Assert.Equal(source.Prompt, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"));
    }

    [Fact]
    public async Task ReasoningProfileIsOwnerBoundAndRevocationRejectsStaleSnapshotsBeforeQuota()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var other = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var source = await LocalLibrary(owner); var foreign = await LocalLibrary(other);
        var grant = await ReasonedDevice(owner, device); AssertLocalReasoning(device); using var poll = await ClaimProposal(device);
        var actor = await ProposalActor(owner); var otherActor = await ProposalActor(other);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var snapshot = proposals.ForConnection(actor, source.Library, grant.Connection.Id); Assert.True(snapshot.UsesThinking("qwen3:8b"));
        Assert.False(proposals.ForConnection(otherActor, foreign.Library, grant.Connection.Id).UsesThinking("qwen3:8b"));
        var before = await PresenceAdmissionCounts(actor.Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(otherActor, foreign.Library, grant.Connection.Id, Guid.NewGuid(), ThinkingProposalPacket()));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).StatusCode);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => snapshot.Send(ThinkingProposalPacket(), default, "synthetic-only"));
        Assert.Equal(before, await PresenceAdmissionCounts(actor.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClaimProposal(device)).StatusCode);
        Assert.False((await Token(device, "refresh_token", "refresh_token", grant.Token.RefreshToken, PairedReasoningProtocol.GrantProfile)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task ExpiredPresenceStillBlocksReasoningAndQueuedCancellationKeepsItsExistingReceipt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ReasonedDevice(owner, device); AssertLocalReasoning(device); var actor = await ProposalActor(owner);
        using var poll = await ClaimProposal(device); var observation = PresenceObservation(poll);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var before = await PresenceAdmissionCounts(actor.Id);
        Assert.True(await WithdrawPresence(device, observation));
        await Assert.ThrowsAsync<CompanionUnavailableException>(() => proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ThinkingProposalPacket()));
        Assert.Equal(before, await PresenceAdmissionCounts(actor.Id));
        using var nextPoll = await ClaimProposal(device);
        var job = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ThinkingProposalPacket());
        using var cancelled = await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/cancel", new { });
        cancelled.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actor, source.Library, job.Id));
        Assert.Equal(source.Prompt, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"));
    }

    [Fact]
    public async Task ReasoningMigrationDownRefusesExistingProfilesBeforeAlteringAnyGuard()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        await ReasonedDevice(owner, device);
        await using var database = new NpgsqlConnection(postgres.MigrationConnection); await database.OpenAsync();
        await using var transaction = await database.BeginTransactionAsync();
        await using var rollback = new NpgsqlCommand(PairedReasoningMigrationTests.Sql("Down"), database, transaction);
        var blocked = await Assert.ThrowsAsync<PostgresException>(() => rollback.ExecuteNonQueryAsync());
        Assert.Equal("P0001", blocked.SqlState);
        Assert.Contains("no grant will be converted or deleted", blocked.MessageText);
        await transaction.RollbackAsync();
        await using var check = new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='bounded_connection' AND conrelid='qp_operations.connections'::regclass", database);
        Assert.Contains(PairedReasoningProtocol.GrantProfile, (string)(await check.ExecuteScalarAsync())!);
        Assert.Equal(PairedReasoningProtocol.GrantProfile, (await device.GetFromJsonAsync<LocalCompanionProfile>("/api/companion/v1/profile"))!.ProfileId);
    }
}
