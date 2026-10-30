using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class McpBoundaryReceiptTests
{
    private const string Canary = "PRIVATE_CANARY_NEVER_LOG";
    private const string ExistingChallenge = "Bearer error=\"invalid_token\"";

    [Theory]
    [InlineData("POST", 401, "POST")]
    [InlineData("POST", 200, "POST")]
    [InlineData("POST", 202, "POST")]
    [InlineData("GET", 405, "GET")]
    [InlineData("DELETE", 405, "DELETE")]
    [InlineData("PRIVATE_CANARY_NEVER_LOG", 403, "OTHER")]
    [InlineData("POST", 500, "POST")]
    public async Task CompletedHttpResponseHasOnlyBoundedReceiptAndIndependentCorrelation(string method, int status, string loggedMethod)
    {
        using var logs = new BoundaryLogs();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new DelegationOrigin(new Uri("https://issuer.example")));
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        await using var app = builder.Build();
        // Like the real host, the outer owner can classify a downstream failure.
        app.Use(async (context, next) =>
        {
            context.TraceIdentifier = Canary;
            try { await next(context); }
            catch (InvalidOperationException) { context.Response.StatusCode = 500; await context.Response.WriteAsync("safe failure"); }
        });
        app.UseAccountMcpChallenge();
        var observedGlobalTrace = "";
        app.Run(async context =>
        {
            observedGlobalTrace = context.TraceIdentifier;
            if (status == 500) throw new InvalidOperationException(Canary);
            context.Response.StatusCode = status;
            context.Response.Headers.WWWAuthenticate = ExistingChallenge;
            await context.Response.WriteAsync(Canary + "_TOOL_OUTPUT");
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/mcp?access_token=" + Canary)
        {
            Content = new StringContent("{\"arguments\":\"" + Canary + "_BODY\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", "Bearer " + Canary + "_TOKEN");
        request.Headers.Add("Cookie", "session=" + Canary);
        request.Headers.Add("X-QP-MCP-Trace", Canary);
        request.Headers.Add("X-Private", Canary);
        using var response = await client.SendAsync(request);
        await response.Content.ReadAsStringAsync();
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        var trace = Assert.Single(response.Headers.GetValues("X-QP-MCP-Trace"));
        Assert.Matches("^[0-9a-f]{32}$", trace);
        Assert.Equal(Canary, observedGlobalTrace);
        var receipt = await logs.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, receipt.Level);
        Assert.Equal(5320, receipt.Event.Id);
        Assert.Null(receipt.Exception);
        Assert.Equal(new[] { "Method", "StatusCode", "RequestMediaType", "ResponseMediaType", "ElapsedMilliseconds", "McpTraceId", "{OriginalFormat}" }, receipt.State.Keys);
        Assert.Equal(loggedMethod, receipt.State["Method"]);
        Assert.Equal(status, receipt.State["StatusCode"]);
        Assert.Equal("JSON", receipt.State["RequestMediaType"]);
        Assert.True(Assert.IsType<long>(receipt.State["ElapsedMilliseconds"]) >= 0);
        Assert.Equal(trace, receipt.State["McpTraceId"]);
        Assert.DoesNotContain(Canary, receipt.Message);
        Assert.All(receipt.State.Values, value => Assert.DoesNotContain(Canary, value?.ToString() ?? ""));
        if (status == 401)
            Assert.Equal(ExistingChallenge + ", resource_metadata=\"https://issuer.example/.well-known/oauth-protected-resource/mcp\", scope=\"prompts:read\"",
                Assert.Single(response.Headers.WwwAuthenticate.Select(value => value.ToString())));
        else if (status != 500)
            Assert.Equal(ExistingChallenge, Assert.Single(response.Headers.WwwAuthenticate.Select(value => value.ToString())));
    }

    [Fact]
    public async Task OtherRoutesHaveNeitherReceiptNorCorrelationHeader()
    {
        using var logs = new BoundaryLogs();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new DelegationOrigin(new Uri("https://issuer.example")));
        builder.Logging.ClearProviders(); builder.Logging.AddProvider(logs);
        await using var app = builder.Build(); app.UseAccountMcpChallenge();
        app.Run(context => context.Response.WriteAsync("healthy"));
        await app.StartAsync(); using var client = app.GetTestClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal("healthy", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.Contains("X-QP-MCP-Trace"));
        Assert.Empty(logs.Entries);
    }

    [Fact]
    public async Task ExistingChallengeScopeIsPreservedAndEveryCompletedRequestGetsANewTrace()
    {
        using var logs = new BoundaryLogs();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new DelegationOrigin(new Uri("https://issuer.example")));
        builder.Logging.ClearProviders(); builder.Logging.AddProvider(logs);
        await using var app = builder.Build(); app.UseAccountMcpChallenge();
        app.Run(async context =>
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer scope=\"prompts:read prompts:create\"";
            await context.Response.WriteAsync("unauthorized");
        });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var first = await client.PostAsync("/mcp", new StringContent("{}"));
        using var second = await client.PostAsync("/mcp", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal("Bearer scope=\"prompts:read prompts:create\", resource_metadata=\"https://issuer.example/.well-known/oauth-protected-resource/mcp\"",
            Assert.Single(second.Headers.WwwAuthenticate.Select(value => value.ToString())));
        Assert.NotEqual(Assert.Single(first.Headers.GetValues("X-QP-MCP-Trace")), Assert.Single(second.Headers.GetValues("X-QP-MCP-Trace")));
    }

    private sealed record Receipt(LogLevel Level, EventId Event, Dictionary<string, object?> State, string Message, Exception? Exception);

    private sealed class BoundaryLogs : ILoggerProvider
    {
        public ConcurrentQueue<Receipt> Entries { get; } = new();
        public TaskCompletionSource<Receipt> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ILogger CreateLogger(string categoryName) => categoryName == "QuickPrompt.McpBoundary" ? new BoundaryLogger(this) : NullLogger.Instance;
        public void Dispose() { }

        private sealed class BoundaryLogger(BoundaryLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
                var receipt = new Receipt(level, id, fields, formatter(state, exception), exception);
                owner.Entries.Enqueue(receipt); owner.Completed.TrySetResult(receipt);
            }
        }
    }
}
