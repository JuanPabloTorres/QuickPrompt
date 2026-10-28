using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
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
    private static JsonElement ProposalPacket() => JsonSerializer.SerializeToElement(new
    {
        model = "qwen3:8b",
        stream = false,
        think = false,
        messages = new[] { new { role = "user", content = "Devuelve un título para una instrucción ficticia." } },
        options = new { num_ctx = 8192, num_predict = 800, temperature = 0 },
        format = new { type = "object", properties = new { title = new { type = "string" } }, required = new[] { "title" } }
    });
    private static JsonElement ProposalReply() => JsonSerializer.SerializeToElement(new
    {
        done = true,
        done_reason = "stop",
        model = "qwen3:8b",
        message = new { content = "{\"title\":\"Propuesta sintética, no inferencia\"}" },
        coreRequestId = Guid.NewGuid().ToString("N"),
        modelCalls = 1,
        prompt_eval_count = 30,
        eval_count = 20
    });
    private static async Task<LibraryActor> ProposalActor(HttpClient cookie)
    {
        var me = await cookie.GetFromJsonAsync<JsonElement>("/account/me");
        return LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, me.GetProperty("id").GetString()!)], "fixture")));
    }
    private static async Task<(ConnectionView Connection, TokenDocument Token)> ProposalDevice(HttpClient owner, HttpClient device)
    {
        var pending = await BeginDevice(device, profile: LocalProposalProtocol.Profile);
        var review = await VerifyDevice(owner, pending, "review");
        await AssertProtocolSuccess(review, "Review proposal consent");
        var shown = await review.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(LocalProposalProtocol.Profile, shown.GetProperty("profileId").GetString());
        Assert.Equal(2, shown.GetProperty("permissions").GetArrayLength());
        await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve separate proposal capability");
        var token = await RedeemDevice(device, pending, profile: LocalProposalProtocol.Profile);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var profile = await device.GetFromJsonAsync<LocalCompanionProfile>("/api/companion/v1/profile");
        Assert.Equal(LocalProposalProtocol.Version, profile!.WorkProtocol);
        return ((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!.Single(x => x.Id == profile.ConnectionId), token);
    }
    private static Task<HttpResponseMessage> ClaimProposal(HttpClient device) => device.PostAsJsonAsync("/api/companion/v1/proposals/claim", new { });
    private async Task<long> ProposalRows(Guid actor, Guid id)
    {
        await using var db = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync();
        await using (var identity = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@owner,true)", db, transaction))
        { identity.Parameters.AddWithValue("owner", actor.ToString()); await identity.ExecuteScalarAsync(); }
        await using var count = new NpgsqlCommand("SELECT count(*) FROM qp_operations.proposal_results WHERE \"ExecutionId\"=@id", db, transaction);
        count.Parameters.AddWithValue("id", id);
        return (long)(await count.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task ProposalCapabilityIsSeparatePrivateAndCannotSaveOrExecuteAnEffect()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var raw = Client(host.App); using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var sourceA = await LocalLibrary(a); var sourceB = await LocalLibrary(b);
        var rawConnection = await LocalDevice(a, raw);
        var grantA = await ProposalDevice(a, deviceA); var grantB = await ProposalDevice(b, deviceB);
        var actorA = await ProposalActor(a); var actorB = await ProposalActor(b);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(actorA, sourceB.Library, grantA.Connection.Id, Guid.NewGuid(), ProposalPacket()));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(actorA, sourceA.Library, grantB.Connection.Id, Guid.NewGuid(), ProposalPacket()));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(actorA, sourceA.Library, rawConnection.Id, Guid.NewGuid(), ProposalPacket()));
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimProposal(raw)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimLocal(deviceA)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClaimProposal(a)).StatusCode);
        var operation = Guid.NewGuid(); var packet = ProposalPacket();
        var job = await proposals.Submit(actorA, sourceA.Library, grantA.Connection.Id, operation, packet);
        Assert.Equal(job.Id, (await proposals.Submit(actorA, sourceA.Library, grantA.Connection.Id, operation, packet)).Id);
        Assert.Null(await host.App.Services.GetRequiredService<ExecutionQueue>().Claim());
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(deviceB)).StatusCode);
        var lease = (await (await ClaimProposal(deviceA)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        Assert.Equal(job.Id, lease.Id); Assert.True(JsonElement.DeepEquals(packet, lease.Packet));
        var finish = $"/api/companion/v1/proposals/{job.Id}/finish";
        Assert.Equal(HttpStatusCode.NotFound, (await deviceB.PostAsJsonAsync($"/api/companion/v1/proposals/{job.Id}/heartbeat", new LocalLeaseToken(lease.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await deviceA.PostAsJsonAsync(finish, new LocalProposalReceipt(Guid.NewGuid(), ProposalReply()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await deviceA.PostAsJsonAsync(finish, new LocalProposalReceipt(lease.Token, JsonSerializer.SerializeToElement(new { injected = "command" })))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await deviceA.PostAsJsonAsync(finish, new LocalProposalReceipt(lease.Token, ProposalReply()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await deviceA.PostAsJsonAsync(finish, new LocalProposalReceipt(lease.Token, ProposalReply()))).StatusCode);
        Assert.Contains("sintética", proposals.Result(actorA, sourceA.Library, job.Id).GetProperty("message").GetProperty("content").GetString());
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actorB, sourceB.Library, job.Id));
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actorB, sourceA.Library, job.Id));
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actorA, sourceB.Library, job.Id));
        Assert.Equal(1L, await ProposalRows(actorA.Id, job.Id));
        Assert.Equal(0L, await ProposalRows(actorB.Id, job.Id)); // No owner predicate: FORCE RLS is the independent barrier.
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/accounts/v1/libraries/{sourceA.Library}/executions/{job.Id}/result")).StatusCode);
        Assert.Equal(sourceA.Prompt, await a.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{sourceA.Library}/prompts/{sourceA.Prompt.Id}"));
        var executionAccess = new AccountExecutionAccess(scope.ServiceProvider.GetRequiredService<QuickPrompt.Accounts.Memory.AccountAgentMemory>(), host.App.Services.GetRequiredService<ExecutionQueue>());
        Assert.Empty(executionAccess.List(actorA, sourceA.Library));
        executionAccess.Forget(actorA, sourceA.Library, job.Id);
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actorA, sourceA.Library, job.Id));
        Assert.Equal(0L, await ProposalRows(actorA.Id, job.Id));
    }

    [Fact]
    public async Task RevocationRejectsProposalReplayRefreshAndLateCompletionWithoutUpgradingOldGrants()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App); using var raw = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        await LocalDevice(owner, raw); var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var operation = Guid.NewGuid(); var packet = ProposalPacket(); var job = await proposals.Submit(actor, source.Library, grant.Connection.Id, operation, packet);
        var lease = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync($"/api/companion/v1/proposals/{job.Id}/finish", new LocalProposalReceipt(lease.Token, ProposalReply()))).StatusCode);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => proposals.Submit(actor, source.Library, grant.Connection.Id, operation, packet));
        Assert.False((await Token(device, "refresh_token", "refresh_token", grant.Token.RefreshToken, LocalProposalProtocol.Profile)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await raw.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimProposal(raw)).StatusCode);
        Assert.Equal(ExecutionStatus.Cancelled, (await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}"))!.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProposalCancellationAndModelFailureNeverCreateAResult(bool cancel)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var job = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ProposalPacket());
        var lease = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        if (cancel) Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/cancel", new { })).StatusCode);
        var receipt = cancel ? new LocalProposalReceipt(lease.Token, ProposalReply()) : new LocalProposalReceipt(lease.Token, null, "MODEL_UNAVAILABLE");
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/proposals/{job.Id}/finish", receipt)).StatusCode);
        var current = await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}");
        Assert.Equal(cancel ? ExecutionStatus.Cancelled : ExecutionStatus.Failed, current!.Status);
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actor, source.Library, job.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task ProposalRetentionErasesContentAndExpiredLeasesNeverCommitOrRetry()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var queue = host.App.Services.GetRequiredService<ExecutionQueue>();
        var old = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ProposalPacket());
        var first = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/proposals/{old.Id}/finish", new LocalProposalReceipt(first.Token, ProposalReply()))).StatusCode);
        await using (var db = await host.App.Services.GetRequiredService<ExecutionDataSource>().Data.OpenConnectionAsync())
        {
            await using var age = new NpgsqlCommand("UPDATE qp_operations.executions SET \"FinishedAt\"=clock_timestamp()-interval '8 days' WHERE \"Id\"=@id", db);
            age.Parameters.AddWithValue("id", old.Id); Assert.Equal(1, await age.ExecuteNonQueryAsync());
        }
        Assert.Throws<LibraryUnavailableException>(() => proposals.Result(actor, source.Library, old.Id));
        Assert.Null(await queue.Claim()); Assert.Equal(0L, await ProposalRows(actor.Id, old.Id));
        var next = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ProposalPacket());
        var lease = (await (await ClaimProposal(device)).Content.ReadFromJsonAsync<LocalProposalLease>())!;
        await using (var db = await host.App.Services.GetRequiredService<ExecutionDataSource>().Data.OpenConnectionAsync())
        {
            await using var expire = new NpgsqlCommand("UPDATE qp_operations.executions SET \"StartedAt\"=clock_timestamp()-interval '1 minute',\"LeaseUntil\"=clock_timestamp()-interval '1 second' WHERE \"Id\"=@id", db);
            expire.Parameters.AddWithValue("id", next.Id); Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        Assert.False((await queue.HeartbeatProposal(actor, grant.Connection.Id, next.Id, lease.Token, _ => Task.FromResult(true), CancellationToken.None)).Owned);
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"/api/companion/v1/proposals/{next.Id}/finish", new LocalProposalReceipt(lease.Token, ProposalReply()))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        Assert.Equal(ExecutionStatus.Interrupted, (await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{next.Id}"))!.Status);
        Assert.Equal(0L, await ProposalRows(actor.Id, next.Id));
    }

    [Fact]
    public async Task ARefreshCannotExpandAPromptGrantToTheProposalResource()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var pending = await BeginDevice(device); await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve original grant");
        var token = await RedeemDevice(device, pending);
        Assert.False((await Token(device, "refresh_token", "refresh_token", token.RefreshToken, LocalProposalProtocol.Profile)).IsSuccessStatusCode);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimProposal(device)).StatusCode);
        Assert.Equal(DelegationPolicy.Profile, Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!).ProfileId);
    }
}
