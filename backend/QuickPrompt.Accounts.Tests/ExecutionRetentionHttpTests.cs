using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task HttpForgetsOnlyTheOwnersTerminalContentWithCsrfAndKeepsItsAdmissionReceipt()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var sameOwnerOtherLibrary = await CreateLibrary(a); var foreign = await CreateLibrary(b);
        var promptResponse = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts", new PromptContent("Olvido QA", "Explica <tema>.", 0));
        promptResponse.EnsureSuccessStatusCode(); var prompt = (await promptResponse.Content.ReadFromJsonAsync<AccountPrompt>())!;
        var path = $"/api/accounts/v1/libraries/{library}";
        var input = new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "dato sintético privado" });
        var admitted = await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", input); admitted.EnsureSuccessStatusCode();
        var job = (await admitted.Content.ReadFromJsonAsync<ExecutionView>())!;
        for (var i = 0; i < 100 && job.Status is ExecutionStatus.Queued or ExecutionStatus.Running; i++)
        { await Task.Delay(50); job = (await a.GetFromJsonAsync<ExecutionView>(path + $"/executions/{job.Id}"))!; }
        Assert.Equal(ExecutionStatus.Completed, job.Status);
        var erase = path + $"/executions/{job.Id}/content";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(erase)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync(erase)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/accounts/v1/libraries/{foreign}/executions/{job.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.DeleteAsync($"/api/accounts/v1/libraries/{sameOwnerOtherLibrary}/executions/{job.Id}/content")).StatusCode);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.DeleteAsync(erase)).StatusCode);
        Assert.NotNull(await a.GetFromJsonAsync<ExecutionResultView>(path + $"/executions/{job.Id}/result"));
        await Csrf(a);
        var forgottenResponse = await a.DeleteAsync(erase); forgottenResponse.EnsureSuccessStatusCode();
        var forgotten = (await forgottenResponse.Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.NotNull(forgotten.ForgottenAt); Assert.Equal(job.Id, forgotten.Id); Assert.Equal(job.Status, forgotten.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(path + $"/executions/{job.Id}/result")).StatusCode);
        Assert.Equal(forgotten, await a.GetFromJsonAsync<ExecutionView>(path + $"/executions/{job.Id}"));
        var retry = await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", input); retry.EnsureSuccessStatusCode();
        Assert.Equal(forgotten, await retry.Content.ReadFromJsonAsync<ExecutionView>());
        Assert.Equal(forgotten, Assert.Single((await a.GetFromJsonAsync<ExecutionView[]>(path + "/executions"))!));
        Assert.Equal(prompt, await a.GetFromJsonAsync<AccountPrompt>(path + $"/prompts/{prompt.Id}"));
    }
}
