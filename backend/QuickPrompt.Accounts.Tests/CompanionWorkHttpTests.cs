using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;
using Xunit;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static async Task<(Guid Library, AccountPrompt Prompt)> LocalLibrary(HttpClient owner)
    {
        var created = await owner.PostAsJsonAsync("/api/accounts/v1/libraries/", new { name = "QA · modelo local" });
        var library = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var saved = await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts", new { revision = 0, title = "Instrucción QA", template = "Analiza <tema>." });
        return (library, (await saved.Content.ReadFromJsonAsync<AccountPrompt>())!);
    }
    private static async Task<ConnectionView> LocalDevice(HttpClient owner, HttpClient device)
    {
        var pending = await BeginDevice(device);
        await AssertProtocolSuccess(await VerifyDevice(owner, pending, "approve"), "Approve local test device");
        var tokens = await RedeemDevice(device, pending);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }
    private static string LocalPath(Guid library, Guid prompt) => $"/api/accounts/v1/libraries/{library}/prompts/{prompt}/run-local";
    private static Task<HttpResponseMessage> ClaimLocal(HttpClient device) => device.PostAsJsonAsync("/api/companion/v1/work/claim", new { });
    private static LocalSubmission LocalInput(Guid connection) => new(connection, Guid.NewGuid(), 1, new() { ["tema"] = "proyecto ficticio" });

    [Fact]
    public async Task LocalJobOnlyReachesItsDeviceAndKeepsTheCompiledInstructionSeparate()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var source = await LocalLibrary(a); var connection = await PollingLocalDevice(a, deviceA); await PollingLocalDevice(b, deviceB);
        var otherSource = await LocalLibrary(b);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(LocalPath(otherSource.Library, otherSource.Prompt.Id), LocalInput(connection.Id))).StatusCode);
        var submitted = await a.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.Equal("RunLocalPrompt", job.Kind);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimLocal(deviceB)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClaimLocal(a)).StatusCode);
        var claimed = await ClaimLocal(deviceA); Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);
        var lease = (await claimed.Content.ReadFromJsonAsync<LocalWorkLease>())!;
        Assert.Equal(job.Id, lease.Id); Assert.Equal("Analiza proyecto ficticio.", lease.Instruction); Assert.Equal(DelegationPolicy.Model, lease.Model);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimLocal(deviceA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await deviceB.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/heartbeat", new LocalLeaseToken(lease.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await deviceA.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, "Respuesta sintética; no inferencia."))).StatusCode);
        var result = (await a.GetFromJsonAsync<ExecutionResultView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result"))!;
        Assert.Equal(lease.Instruction, result.Text); Assert.Equal("Respuesta sintética; no inferencia.", result.Response); Assert.Equal(DelegationPolicy.Model, result.Model);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result")).StatusCode);
        var original = (await a.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"))!;
        Assert.Equal(source.Prompt.Template, original.Template); Assert.Equal(1, original.Uses);
    }

    [Fact]
    public async Task RevocationCancelsTheLocalTurnAndRejectsLateCompletion()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, "Tardía"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClaimLocal(device)).StatusCode);
        var current = (await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}"))!;
        Assert.Equal(ExecutionStatus.Cancelled, current.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id))).StatusCode);
    }

    [Fact]
    public async Task WrongLeaseAndCancellationCannotSaveALocalResponse()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        var finish = $"/api/companion/v1/work/{job.Id}/finish";
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync(finish, new LocalWorkReceipt(Guid.NewGuid(), "Falsa"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await device.PostAsJsonAsync(finish, new LocalWorkReceipt(lease.Token, ""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync(finish, new LocalWorkReceipt(lease.Token, "No guardar"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result")).StatusCode);
        Assert.Equal(ExecutionStatus.Cancelled, (await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}"))!.Status);
    }

    [Fact]
    public async Task LocalCompletionRechecksTheClockAfterItsAuthorityCheck()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        var account = await owner.GetFromJsonAsync<JsonElement>("/account/me");
        var actor = LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, account.GetProperty("id").GetString()!)], "test")));
        await using (var worker = await host.App.Services.GetRequiredService<ExecutionDataSource>().Data.OpenConnectionAsync())
        {
            await using var expire = new NpgsqlCommand("UPDATE qp_operations.executions SET \"LeaseUntil\"=clock_timestamp()+interval '2 seconds' WHERE \"Id\"=@id", worker);
            expire.Parameters.AddWithValue("id", job.Id); Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        // This scheduling test isolates the final time check. HTTP tests separately exercise real authority.
        var queue = host.App.Services.GetRequiredService<ExecutionQueue>();
        Assert.False(await queue.FinishLocal(actor, connection.Id, job.Id, new(lease.Token, "Demorada"), async _ => { await Task.Delay(2200); return true; }, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result")).StatusCode);
    }

    [Fact]
    public async Task DatabaseRejectsAnUnlabelledModelResponse()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        var account = await owner.GetFromJsonAsync<JsonElement>("/account/me");
        await using (var worker = await host.App.Services.GetRequiredService<ExecutionDataSource>().Data.OpenConnectionAsync())
        {
            await using var transaction = await worker.BeginTransactionAsync();
            await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @owner, true)", worker, transaction);
            actor.Parameters.AddWithValue("owner", account.GetProperty("id").GetString()!); await actor.ExecuteScalarAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO qp_operations.execution_results ("ExecutionId","OwnerId","LibraryId","PromptId","PromptRevision","Title","Text","CreatedAt","Response","Model")
                VALUES (@id,@owner,@library,@prompt,1,'QA','Instrucción',clock_timestamp(),'Sin modelo',NULL)
                """, worker, transaction);
            insert.Parameters.AddWithValue("id", job.Id); insert.Parameters.AddWithValue("owner", account.GetProperty("id").GetGuid());
            insert.Parameters.AddWithValue("library", source.Library); insert.Parameters.AddWithValue("prompt", source.Prompt.Id);
            Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).SqlState);
            await transaction.RollbackAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, "Con modelo fijo"))).StatusCode);
    }
}
