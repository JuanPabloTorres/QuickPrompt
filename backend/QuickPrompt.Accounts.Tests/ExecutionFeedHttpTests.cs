using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task ExecutionFeedProtectsBothLibraryIdentityAndPrivateContentAndClosesAtCompletion()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var other = await CreateLibrary(a); var foreign = await CreateLibrary(b);
        var prompt = await SaveSyntheticPrompt(a, library, "Título privado", "Analiza <tema>.");
        var root = $"/api/accounts/v1/libraries/{library}";
        var response = await a.PostAsJsonAsync(root + $"/prompts/{prompt.Id}/compile", new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "CONTENIDO_PRIVADO_NO_STREAM" })); response.EnsureSuccessStatusCode();
        var job = (await response.Content.ReadFromJsonAsync<ExecutionView>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(root + "/executions/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(root + "/executions/events")).StatusCode);
        var feed = await a.GetAsync(root + "/executions/events"); feed.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", feed.Content.Headers.ContentType!.MediaType); Assert.True(feed.Headers.CacheControl!.NoStore);
        var wire = await feed.Content.ReadAsStringAsync();
        Assert.Contains(job.Id.ToString(), wire); Assert.Contains("\"Completed\"", wire); Assert.Contains("\"complete\"", wire);
        foreach (var secret in new[] { prompt.Title, prompt.Template, "CONTENIDO_PRIVADO_NO_STREAM", "leaseToken", "ownerId", "payload", "requestHash", "response" }) Assert.DoesNotContain(secret, wire);
        Assert.DoesNotContain(job.Id.ToString(), await a.GetStringAsync($"/api/accounts/v1/libraries/{other}/executions/events"));
        Assert.DoesNotContain(job.Id.ToString(), await b.GetStringAsync($"/api/accounts/v1/libraries/{foreign}/executions/events"));
    }

    [Fact]
    public async Task McpLibraryGrantDoesNotAuthorizePrivateExecutionEvents()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var protocol = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var library = await CreateLibrary(a);
        const string verifier = "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp";
        var tokens = await McpRedeem(protocol, await ApproveMcp(a, library, McpRequest(protocol, verifier)), verifier);
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocol.GetAsync(FeedPath(library))).StatusCode);
    }

    [Fact]
    public async Task OpenExecutionFeedRechecksStampAcrossHostsWithoutClosingTheOtherAccount()
    {
        await using var host = await Start(configureServices: services => services.AddSingleton<IAccountExecutionHandler, PendingFeedHandler>());
        using var a = Client(host.App); using var b = Client(host.App);
        var email = await Register(a, host.Mail); await Login(a, email); await Login(b, await Register(b, host.Mail));
        var first = await SubmitFeedJob(a); var second = await SubmitFeedJob(b);
        await Running(a, first.LibraryId, first.Id); await Running(b, second.LibraryId, second.Id);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var firstResponse = await a.GetAsync(FeedPath(first.LibraryId), HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var secondResponse = await b.GetAsync(FeedPath(second.LibraryId), HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var firstReader = new StreamReader(await firstResponse.Content.ReadAsStreamAsync(lifetime.Token));
        using var secondReader = new StreamReader(await secondResponse.Content.ReadAsStreamAsync(lifetime.Token));
        Assert.Equal(first.Id, Assert.Single((await Frame(firstReader, lifetime.Token))!.Items!).Id);
        Assert.Equal(second.Id, Assert.Single((await Frame(secondReader, lifetime.Token))!.Items!).Id);
        await using var remote = await Start(); using var current = Client(remote.App); await Csrf(current); await Login(current, email);
        Assert.Equal(HttpStatusCode.NoContent, (await current.PostAsJsonAsync("/account/logout-all", new { })).StatusCode);
        await Until(firstReader, "access-lost", lifetime.Token);
        Assert.Null(await Frame(firstReader, lifetime.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync(FeedPath(first.LibraryId))).StatusCode);
        Assert.NotEqual("access-lost", (await Frame(secondReader, lifetime.Token))!.Signal);
        Assert.Equal(ExecutionStatus.Running, (await b.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{second.LibraryId}/executions/{second.Id}"))!.Status);
        await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{second.LibraryId}/executions/{second.Id}/cancel", new { });
        await Until(secondReader, "complete", lifetime.Token);
    }

    [Fact]
    public async Task DisconnectOnlyStopsObservationAndTheOwnerCanResumeThenCancel()
    {
        await using var host = await Start(configureServices: services => services.AddSingleton<IAccountExecutionHandler, PendingFeedHandler>());
        using var a = Client(host.App); await Login(a, await Register(a, host.Mail)); var job = await SubmitFeedJob(a);
        await Running(a, job.LibraryId, job.Id);
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
        using (var response = await a.GetAsync(FeedPath(job.LibraryId), HttpCompletionOption.ResponseHeadersRead, stop.Token))
        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(stop.Token)))
        { Assert.Equal("snapshot", (await Frame(reader, stop.Token))!.Signal); stop.Cancel(); }
        Assert.Equal(ExecutionStatus.Running, (await a.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}"))!.Status);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var resumed = await a.GetAsync(FeedPath(job.LibraryId), HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var resumedReader = new StreamReader(await resumed.Content.ReadAsStreamAsync(lifetime.Token));
        Assert.Equal(job.Id, Assert.Single((await Frame(resumedReader, lifetime.Token))!.Items!).Id);
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}/cancel", new { })).StatusCode);
        var final = await Until(resumedReader, "complete", lifetime.Token);
        Assert.Equal("complete", final.Signal);
        Assert.Equal(ExecutionStatus.Cancelled, (await a.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}/result")).StatusCode);
    }

    [Fact]
    public async Task ExecutionFeedRenewsItsObservationWithoutRepeatingOrCancellingTheJob()
    {
        await using var host = await Start(configureServices: services => services.AddSingleton<IAccountExecutionHandler, PendingFeedHandler>());
        using var a = Client(host.App); await Login(a, await Register(a, host.Mail)); var job = await SubmitFeedJob(a);
        await Running(a, job.LibraryId, job.Id);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var response = await a.GetAsync(FeedPath(job.LibraryId), HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(lifetime.Token));
        Assert.Equal("snapshot", (await Frame(reader, lifetime.Token))!.Signal);
        await Until(reader, "renew", lifetime.Token); Assert.Null(await Frame(reader, lifetime.Token));
        Assert.Equal(ExecutionStatus.Running, (await a.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}"))!.Status);
        Assert.Single((await a.GetFromJsonAsync<ExecutionView[]>($"/api/accounts/v1/libraries/{job.LibraryId}/executions"))!);
        await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{job.LibraryId}/executions/{job.Id}/cancel", new { });
    }

    [Fact]
    public async Task CookieExpiryClosesAnAlreadyOpenFeed()
    {
        await using var host = await Start(configureServices: services =>
        {
            services.AddSingleton<IAccountExecutionHandler, PendingFeedHandler>();
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.ExpireTimeSpan = TimeSpan.FromSeconds(6));
        });
        using var a = Client(host.App); var email = await Register(a, host.Mail); await Login(a, email); var job = await SubmitFeedJob(a);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var response = await a.GetAsync(FeedPath(job.LibraryId), HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(lifetime.Token));
        Assert.Equal("snapshot", (await Frame(reader, lifetime.Token))!.Signal);
        await Until(reader, "access-lost", lifetime.Token); Assert.Null(await Frame(reader, lifetime.Token));
    }

    private static string FeedPath(Guid library) => $"/api/accounts/v1/libraries/{library}/executions/events";
    private static async Task<ExecutionView> SubmitFeedJob(HttpClient client)
    {
        var library = await CreateLibrary(client); var prompt = await SaveSyntheticPrompt(client, library, "Espera controlada", "Explica <tema>.");
        var response = await client.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts/{prompt.Id}/compile", new CompileSubmission(Guid.NewGuid(), 1, new() { ["tema"] = "RLS" })); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionView>())!;
    }
    private static async Task Running(HttpClient client, Guid library, Guid id)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if ((await client.GetFromJsonAsync<ExecutionView>($"/api/accounts/v1/libraries/{library}/executions/{id}"))!.Status == ExecutionStatus.Running) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("The synthetic handler did not receive its lease.");
    }
    private static async Task<ExecutionFeed?> Frame(StreamReader reader, CancellationToken cancellation)
    {
        while (await reader.ReadLineAsync(cancellation) is { } line)
            if (line.StartsWith("data:", StringComparison.Ordinal)) return JsonSerializer.Deserialize<ExecutionFeed>(line[5..].TrimStart(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return null;
    }
    private static async Task<ExecutionFeed> Until(StreamReader reader, string signal, CancellationToken cancellation)
    {
        while (await Frame(reader, cancellation) is { } item) if (item.Signal == signal) return item;
        throw new InvalidOperationException("The stream ended before the expected receipt.");
    }
    private sealed class PendingFeedHandler : IAccountExecutionHandler
    {
        public async Task<ExecutionOutput> Execute(ExecutionLease lease, CancellationToken cancellation)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("The synthetic handler cannot complete."); }
    }
}
