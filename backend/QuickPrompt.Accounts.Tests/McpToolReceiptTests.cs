using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

// The real SDK/filter/HTTP composition with synthetic tools. No account, SQL,
// inference or external server; this does not certify production authorization.
public sealed class McpToolReceiptTests
{
    private const string Canary = "PRIVATE_CANARY_NEVER_LOG";

    [Theory]
    [InlineData("match", "CHANNELS_MATCH")]
    [InlineData("different", "CHANNELS_DIFFER")]
    [InlineData("invalid", "TEXT_JSON_UNREADABLE")]
    [InlineData("missing", "NO_STRUCTURED_CONTENT")]
    [InlineData("empty", "NON_SINGLE_TEXT")]
    [InlineData("multiple", "NON_SINGLE_TEXT")]
    [InlineData("large", "INSPECTION_LIMIT")]
    [InlineData("error", "NO_STRUCTURED_CONTENT")]
    public async Task ObservationKeepsTheExactRawResultAndNeverLogsItsContent(string mode, string shape)
    {
        await using var fixture = await Start();
        var result = await fixture.Client.CallToolAsync("receipt_raw", new Dictionary<string, object?> { ["mode"] = mode });
        Assert.Equal(1, fixture.State.Calls);
        var expected = JsonSerializer.SerializeToNode(fixture.State.RawResult, McpJsonUtilities.DefaultOptions)!;
        var actual = JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions)!;
        // The native SDK adds its public serverInfo after the tool filter.
        // Account for that exact addition, retaining every tool-authored field.
        expected["_meta"]!["io.modelcontextprotocol/serverInfo"] = JsonSerializer.SerializeToNode(fixture.Client.ServerInfo, McpJsonUtilities.DefaultOptions);
        Assert.True(JsonNode.DeepEquals(expected, actual));
        var receipt = Assert.Single(fixture.Logs.Entries, item => item.Id == 5321);
        Assert.Equal(shape, receipt.Fields["Shape"]);
        Assert.Equal("receipt_raw", receipt.Fields["Tool"]);
        Assert.Equal("RETURNED", receipt.Fields["Outcome"]);
        Assert.Equal(mode == "error", receipt.Fields["IsError"]);
        AssertSafe(fixture.Logs);
    }

    [Fact]
    public async Task TypedResultReceiptSharesTheHttpTraceAndHashesOnlyThePublicSchema()
    {
        await using var fixture = await Start();
        var result = await fixture.Client.CallToolAsync("receipt_typed", new Dictionary<string, object?>());
        var content = JsonSerializer.SerializeToElement(result.StructuredContent);
        Assert.Equal(Canary, content.GetProperty("text").GetString());
        Assert.Equal(7, content.GetProperty("revision").GetInt32());
        var receipt = Assert.Single(fixture.Logs.Entries, item => item.Id == 5321);
        Assert.Equal(true, receipt.Fields["ChannelsEqual"]);
        Assert.Equal(true, receipt.Fields["RequiredRootFieldsPresent"]);
        Assert.Matches("^[0-9a-f]{64}$", Assert.IsType<string>(receipt.Fields["SchemaSha256"]));
        var trace = Assert.IsType<string>(receipt.Fields["McpTraceId"]);
        Assert.Matches("^[0-9a-f]{32}$", trace);
        Assert.Contains(fixture.Logs.Entries, item => item.Id == 5320 && Equals(item.Fields["McpTraceId"], trace));
        Assert.Equal(1, fixture.State.Calls);
        AssertSafe(fixture.Logs);
    }

    [Fact]
    public async Task AFailingObservationSinkCannotFailOrRepeatTheTool()
    {
        await using var fixture = await Start(throwOnToolLog: true);
        var result = await fixture.Client.CallToolAsync("receipt_typed", new Dictionary<string, object?>());
        Assert.Equal(Canary, JsonSerializer.SerializeToElement(result.StructuredContent).GetProperty("text").GetString());
        Assert.Equal(1, fixture.State.Calls);
        Assert.DoesNotContain(fixture.Logs.Entries, item => item.Id == 5321);
    }

    [Fact]
    public async Task CancelledCallEndsAndTheNextCallKeepsItsOwnResultAndTrace()
    {
        await using var fixture = await Start();
        using var cancellation = new CancellationTokenSource();
        var waiting = fixture.Client.CallToolAsync("receipt_wait", new Dictionary<string, object?>(), cancellationToken: cancellation.Token).AsTask();
        await fixture.State.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await fixture.State.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next = await fixture.Client.CallToolAsync("receipt_typed", new Dictionary<string, object?>());
        Assert.Equal(Canary, JsonSerializer.SerializeToElement(next.StructuredContent).GetProperty("text").GetString());
        Assert.Equal(2, fixture.State.Calls);
        var receipt = Assert.Single(fixture.Logs.Entries, item => item.Id == 5321 && Equals(item.Fields["Tool"], "receipt_typed"));
        Assert.Equal("RETURNED", receipt.Fields["Outcome"]);
        Assert.Contains(fixture.Logs.Entries, item => item.Id == 5321
            && Equals(item.Fields["Tool"], "receipt_wait") && Equals(item.Fields["Outcome"], "CANCELLED"));
        AssertSafe(fixture.Logs);
    }

    [Fact]
    public async Task ToolFailureKeepsTheSdkErrorAndNeverLogsItsMessage()
    {
        await using var fixture = await Start();
        var result = await fixture.Client.CallToolAsync("receipt_fault", new Dictionary<string, object?>());
        Assert.True(result.IsError);
        Assert.Contains(Canary, Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(1, fixture.State.Calls);
        Assert.Single(fixture.Logs.Entries, item => item.Id == 5321 && Equals(item.Fields["Tool"], "receipt_fault"));
        AssertSafe(fixture.Logs);
    }

    [Fact]
    public async Task ExistingSdkAuthorizationFilterStillRejectsTheDeniedToolWithoutInvocation()
    {
        await using var fixture = await Start();
        await Assert.ThrowsAsync<McpProtocolException>(() => fixture.Client.CallToolAsync("receipt_denied", new Dictionary<string, object?>()).AsTask());
        Assert.Equal(0, fixture.State.Calls);
        AssertSafe(fixture.Logs);
    }

    [Theory]
    [InlineData(null, "MISSING", 415)]
    [InlineData("application/PRIVATE_CANARY_NEVER_LOG", "OTHER", 415)]
    [InlineData("application/json; private=PRIVATE_CANARY_NEVER_LOG", "JSON", 200)]
    public async Task HttpReceiptClassifiesMediaTypesWithoutReflectingHeaderOrRpcValues(string? mediaType, string expected, int status)
    {
        await using var fixture = await Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") {
            Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = Canary, method = "ping" }), Encoding.UTF8)
        };
        request.Content.Headers.ContentType = mediaType is null ? null : MediaTypeHeaderValue.Parse(mediaType);
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        using var response = await fixture.Http.SendAsync(request);
        await response.Content.ReadAsStringAsync();
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        var trace = Assert.Single(response.Headers.GetValues("X-QP-MCP-Trace"));
        var receipt = Assert.Single(fixture.Logs.Entries, item => item.Id == 5320 && Equals(item.Fields["McpTraceId"], trace));
        Assert.Equal(expected, receipt.Fields["RequestMediaType"]);
        Assert.Equal(0, fixture.State.Calls);
        AssertSafe(fixture.Logs);
    }

    [Fact]
    public async Task ARejectedConnectionKeepsIts401BeforeTheMissingContentTypeGuard()
    {
        await using var fixture = await Start();
        fixture.State.AllowRequests = false; // Controlled admission owner, not a real account/grant.
        using var content = new StringContent("{}"); content.Headers.ContentType = null;
        using var response = await fixture.Http.PostAsync("/mcp", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal(0, fixture.State.Calls);
        AssertSafe(fixture.Logs);
    }

    private static void AssertSafe(ReceiptLogs logs)
    {
        Assert.All(logs.Entries, item => {
            Assert.DoesNotContain(Canary, item.Message);
            Assert.All(item.Fields.Values, value => Assert.DoesNotContain(Canary, value?.ToString() ?? ""));
        });
    }

    private static async Task<Fixture> Start(bool throwOnToolLog = false)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var logs = new ReceiptLogs(throwOnToolLog); var state = new ReceiptState();
        builder.Logging.ClearProviders(); builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton(new DelegationOrigin(new Uri("https://issuer.example")));
        builder.Services.AddAccountMcp(); // Real production filter registration.
        builder.Services.AddAuthorization(options => options.AddPolicy("receipts-denied", policy => policy.RequireAssertion(_ => false)));
        builder.Services.AddMcpServer().WithTools<ReceiptTools>();
        var app = builder.Build(); app.UseAccountMcpChallenge();
        app.MapMcp("/mcp") // Synthetic admission owner; does not certify real account/grant isolation.
            .AddEndpointFilter((context, next) => state.AllowRequests ? next(context)
                : ValueTask.FromResult<object?>(Microsoft.AspNetCore.Http.Results.Unauthorized()))
            .AddEndpointFilter(AccountMcpModule.RejectMissingPostContentType);
        await app.StartAsync();
        var http = app.GetTestClient(); http.DefaultRequestHeaders.Add("X-QP-MCP-Trace", Canary);
        var client = await McpClient.CreateAsync(new HttpClientTransport(new() {
            Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false));
        return new(app, http, client, logs, state);
    }

    private sealed record Fixture(WebApplication App, HttpClient Http, McpClient Client, ReceiptLogs Logs, ReceiptState State) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Client.DisposeAsync(); Http.Dispose(); await App.DisposeAsync(); }
    }

    public sealed class ReceiptState
    {
        public int Calls;
        public CallToolResult? RawResult;
        public bool AllowRequests = true;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record Payload(string Text, int Revision);

    [McpServerToolType]
    public sealed class ReceiptTools(ReceiptState state)
    {
        [McpServerTool(Name = "receipt_typed", ReadOnly = true, UseStructuredContent = true)]
        public Payload Typed() { Interlocked.Increment(ref state.Calls); return new(Canary, 7); }

        [McpServerTool(Name = "receipt_raw", ReadOnly = true)]
        public CallToolResult Raw(string mode)
        {
            Interlocked.Increment(ref state.Calls);
            var body = JsonSerializer.Serialize(new { text = Canary });
            state.RawResult = new() {
                IsError = mode == "error", StructuredContent = mode is "missing" or "error" ? null : JsonSerializer.SerializeToElement(new { text = mode == "different" ? "different" : Canary }),
                Content = mode == "empty" ? [] : mode == "multiple" ? [new TextContentBlock { Text = body }, new TextContentBlock { Text = body }]
                    : [new TextContentBlock { Text = mode == "large" ? new string('x', 262145) : mode == "invalid" ? Canary : body }]
            };
            var encoded = JsonSerializer.SerializeToNode(state.RawResult, McpJsonUtilities.DefaultOptions)!;
            encoded["_meta"] = new JsonObject { ["private"] = Canary };
            state.RawResult = encoded.Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions)!;
            return state.RawResult;
        }

        [McpServerTool(Name = "receipt_wait", ReadOnly = true, UseStructuredContent = true)]
        public async Task<Payload> Wait(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref state.Calls); state.Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); return new(Canary, 7); }
            finally { state.Cancelled.TrySetResult(); }
        }

        [Authorize(Policy = "receipts-denied")]
        [McpServerTool(Name = "receipt_denied", ReadOnly = true, UseStructuredContent = true)]
        public Payload Denied() { Interlocked.Increment(ref state.Calls); return new(Canary, 7); }

        [McpServerTool(Name = "receipt_fault", ReadOnly = true, UseStructuredContent = true)]
        public Payload Fault() { Interlocked.Increment(ref state.Calls); throw new McpException(Canary); }
    }

    private sealed record Receipt(int Id, Dictionary<string, object?> Fields, string Message);

    private sealed class ReceiptLogs(bool throwOnToolLog) : ILoggerProvider
    {
        public ConcurrentQueue<Receipt> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => categoryName is "QuickPrompt.McpBoundary" or "QuickPrompt.McpToolContract" ? new ReceiptLogger(this, throwOnToolLog) : NullLogger.Instance;
        public void Dispose() { }

        private sealed class ReceiptLogger(ReceiptLogs owner, bool throwOnToolLog) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (id.Id == 5321 && throwOnToolLog) throw new InvalidOperationException(Canary);
                var fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
                owner.Entries.Enqueue(new(id.Id, fields, formatter(state, exception)));
            }
        }
    }
}
