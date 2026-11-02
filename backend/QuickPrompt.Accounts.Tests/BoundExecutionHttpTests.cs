using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    // Real HTTP, OAuth grants and PostgreSQL. Receipts are synthetic, not model acceptance.
    [Theory]
    [InlineData(LocalWorkProtocol.Version)]
    [InlineData(LocalWorkProtocol.BoundVersion)]
    public async Task NegotiatedExecutionPreservesSnapshotAndAccountIsolation(string protocol)
    {
        await using var host = await Start();
        using var owner = Client(host.App); using var other = Client(host.App);
        using var device = Client(host.App); using var foreign = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var source = await LocalLibrary(owner);
        var connection = await PollingLocalDevice(owner, device); await PollingLocalDevice(other, foreign);
        if (protocol == LocalWorkProtocol.BoundVersion)
        {
            device.DefaultRequestHeaders.Add(LocalWorkProtocol.VersionHeader, protocol);
            foreign.DefaultRequestHeaders.Add(LocalWorkProtocol.VersionHeader, protocol);
        }
        var profile = (await device.GetFromJsonAsync<LocalCompanionProfile>("/api/companion/v1/profile"))!;
        Assert.Equal(protocol, profile.WorkProtocol);
        const string value = "Texto.\nUnicode 二 <otro> y \"comillas\"";
        var input = LocalInput(connection.Id) with { Values = new() { ["tema"] = value } };
        using var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimLocal(foreign)).StatusCode);
        using var claimed = await ClaimLocal(device);
        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);
        var raw = await claimed.Content.ReadAsStringAsync();
        var lease = JsonSerializer.Deserialize<LocalWorkLease>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(job.Id, lease.Id); Assert.Equal("Analiza " + value + ".", lease.Instruction);
        using var document = JsonDocument.Parse(raw);
        Assert.Equal(protocol == LocalWorkProtocol.Version ? 6 : 7, document.RootElement.EnumerateObject().Count());
        if (protocol == LocalWorkProtocol.Version) Assert.Null(lease.Context);
        else
        {
            Assert.NotNull(lease.Context);
            Assert.Equal(source.Prompt.Template, lease.Context.Template);
            Assert.Equal(value, Assert.Single(lease.Context.Values).Value);
            Assert.Equal("tema", Assert.Single(lease.Context.Values).Key);
            Assert.Equal(lease.Context.BasisHash, lease.Context.Validate(lease.Instruction).BasisHash);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimLocal(device)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/heartbeat", new LocalLeaseToken(lease.Token))).StatusCode);
        const string response = "Synthetic response; no inference.";
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, response))).StatusCode);
        var path = $"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result";
        var result = (await owner.GetFromJsonAsync<ExecutionResultView>(path))!;
        Assert.Equal(lease.Instruction, result.Text); Assert.Equal(response, result.Response);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(source.Prompt.Template, (await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"))!.Template);
    }

    [Theory]
    [InlineData("quickprompt-local-work/3")]
    [InlineData("quickprompt-local-work/1, quickprompt-local-work/2")]
    public async Task UnsupportedWorkNegotiationCannotConsumeQueuedWork(string protocol)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var connection = await PollingLocalDevice(owner, device);
        using var submitted = await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id));
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var job = (await submitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        device.DefaultRequestHeaders.TryAddWithoutValidation(LocalWorkProtocol.VersionHeader, protocol);
        Assert.Equal(HttpStatusCode.BadRequest, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimLocal(device)).StatusCode);
        Assert.Equal(ExecutionStatus.Queued, (await owner.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}"))!.Status);
        device.DefaultRequestHeaders.Remove(LocalWorkProtocol.VersionHeader);
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        Assert.Equal(job.Id, lease.Id); Assert.Null(lease.Context);
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, null, "LOCAL_STOPPED"))).StatusCode);
    }

    [Fact]
    public async Task UnusedSubmissionValuesAreRejectedBeforeBoundWorkAdmission()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var connection = await PollingLocalDevice(owner, device);
        device.DefaultRequestHeaders.Add(LocalWorkProtocol.VersionHeader, LocalWorkProtocol.BoundVersion);
        var input = LocalInput(connection.Id) with { Values = new() { ["tema"] = "QA", ["unused"] = "SYNTHETIC_PRIVATE_UNUSED" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimLocal(device)).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<ExecutionView[]>($"/api/accounts/v1/libraries/{source.Library}/executions"))!);
    }

    [Fact]
    public async Task WorkProtocolNegotiationDoesNotGrantWorkScopeToNovaDevice()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await PollingProposalDevice(owner, device);
        device.DefaultRequestHeaders.Add(LocalWorkProtocol.VersionHeader, LocalWorkProtocol.BoundVersion);
        Assert.Equal(HttpStatusCode.BadRequest, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimLocal(device)).StatusCode);
    }
}
