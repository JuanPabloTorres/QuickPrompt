using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task SuccessfulLocalCompletionCountsOnceAndCopyAfterReloadUsesTheSameReceipt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var path = $"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}";
        var input = LocalInput(connection.Id) with { Values = new() { ["tema"] = "Unicode ñ <literal>\n二" } };
        var job = (await (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), input)).Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.Equal(0, (await owner.GetFromJsonAsync<AccountPrompt>(path))!.Uses);
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        Assert.Equal(0, (await owner.GetFromJsonAsync<AccountPrompt>(path))!.Uses);
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, "Synthetic response; no model call."))).StatusCode);
        var resultPath = $"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result";
        var result = (await owner.GetFromJsonAsync<ExecutionResultView>(resultPath))!;
        Assert.Equal(job.Id, result.Usage!.OperationId); Assert.Equal(input.Values["tema"], result.Usage.Values["tema"]);
        Assert.Equal(1, (await owner.GetFromJsonAsync<AccountPrompt>(path))!.Uses);
        var reloaded = (await owner.GetFromJsonAsync<ExecutionResultView>(resultPath))!;
        var use = new UseInput(reloaded.PromptRevision, reloaded.Usage!.Values, reloaded.Usage.OperationId, reloaded.Text);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync(path + "/use", use)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync(path + "/use", use)).StatusCode);
        Assert.Equal(1, (await owner.GetFromJsonAsync<AccountPrompt>(path))!.Uses);
    }

    [Theory]
    [InlineData("MODEL_FAILED")]
    [InlineData("MODEL_TIMEOUT")]
    [InlineData("LOCAL_STOPPED")]
    public async Task FailedOrStoppedLocalWorkNeverCountsUsage(string code)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var job = (await (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id))).Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, null, code))).StatusCode);
        Assert.Equal(0, (await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"))!.Uses);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result")).StatusCode);
    }

    [Fact]
    public async Task RevisionRaceDoesNotLoseCompletedModelResultOrForceUsageOntoANewerPrompt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var connection = await PollingLocalDevice(owner, device);
        var job = (await (await owner.PostAsJsonAsync(LocalPath(source.Library, source.Prompt.Id), LocalInput(connection.Id))).Content.ReadFromJsonAsync<ExecutionView>())!;
        var lease = (await (await ClaimLocal(device)).Content.ReadFromJsonAsync<LocalWorkLease>())!;
        var path = $"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}";
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(path, new { revision = 1, title = "Changed", template = "Other <tema>." })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsJsonAsync($"/api/companion/v1/work/{job.Id}/finish", new LocalWorkReceipt(lease.Token, "Genuine accepted synthetic result"))).StatusCode);
        var result = (await owner.GetFromJsonAsync<ExecutionResultView>($"/api/accounts/v1/libraries/{source.Library}/executions/{job.Id}/result"))!;
        Assert.Equal("Genuine accepted synthetic result", result.Response); Assert.Equal(1, result.PromptRevision);
        Assert.Equal(0, (await owner.GetFromJsonAsync<AccountPrompt>(path))!.Uses);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/use", new UseInput(1, result.Usage!.Values, job.Id, result.Text))).StatusCode);
    }
}
