using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public void HostRefusesASeparateDispatcherDatabaseBeforeOpeningItsPools()
    {
        var wrong = new NpgsqlConnectionStringBuilder(postgres.ExecutionConnection) { Database = "quickprompt_accounts_local" };
        Assert.Throws<InvalidOperationException>(() => AccountHost.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QuickPrompt:Accounts:Origin"] = "https://127.0.0.1:5299",
            ["ConnectionStrings:Accounts"] = postgres.ApplicationConnection,
            ["ConnectionStrings:Executions"] = wrong.ConnectionString
        })));
    }

    [Fact]
    public async Task HttpDispatchesARealCompilationAndProtectsItsReceiptAndResult()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var other = await CreateLibrary(b);
        var response = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts", new PromptContent("Uso privado", "Analiza <tema>.", 0));
        response.EnsureSuccessStatusCode(); var prompt = (await response.Content.ReadFromJsonAsync<AccountPrompt>())!;
        var path = $"/api/accounts/v1/libraries/{library}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/executions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path + "/executions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", new { operationId = Guid.NewGuid(), revision = 1, values = new { tema = "RLS" }, ownerId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", new CompileSubmission(Guid.NewGuid(), 1, []))).StatusCode);
        var request = new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "<script>literal</script>" });
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", request)).StatusCode);
        await Csrf(a);
        var accepted = await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", request);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var wire = await accepted.Content.ReadAsStringAsync();
        Assert.DoesNotContain("lease", wire, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("payload", wire, StringComparison.OrdinalIgnoreCase);
        var job = JsonSerializer.Deserialize<ExecutionView>(wire, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        ExecutionView current = job;
        for (var count = 0; count < 100 && current.Status is ExecutionStatus.Queued or ExecutionStatus.Running; count++)
        { await Task.Delay(50); current = (await a.GetFromJsonAsync<ExecutionView>(path + $"/executions/{job.Id}"))!; }
        Assert.Equal(ExecutionStatus.Completed, current.Status);
        var result = (await a.GetFromJsonAsync<ExecutionResultView>(path + $"/executions/{job.Id}/result"))!;
        Assert.Equal("Analiza <script>literal</script>.", result.Text);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{other}/executions/{job.Id}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(path + $"/executions/{job.Id}/cancel", new { })).StatusCode);
        var retry = await a.PostAsJsonAsync(path + $"/prompts/{prompt.Id}/compile", request);
        Assert.Equal(job.Id, (await retry.Content.ReadFromJsonAsync<ExecutionView>())!.Id);
        Assert.Single((await a.GetFromJsonAsync<ExecutionView[]>(path + "/executions"))!);
    }
}
