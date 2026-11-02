using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class McpResponseDeliveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleToolResultArrivesAsOneCompleteJsonResponse(bool socket)
    {
        await using var fixture = await Start(socket);
        using var response = await Call(fixture.Http, "delivery_search");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), response.Content.Headers.ContentLength);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.NoTransform);
        Assert.False(response.Headers.TransferEncodingChunked ?? false);
        using var message = JsonDocument.Parse(body);
        Assert.Equal("delivery-1", message.RootElement.GetProperty("id").GetString());
        Assert.Empty(message.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("items").EnumerateArray());
        Assert.Equal(1, fixture.State.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2025-06-18")]
    public async Task RealSdkClientKeepsCatalogUnicodeErrorsAndBothContentChannels(string? version)
    {
        await using var fixture = await Start(socket: true);
        fixture.Http.DefaultRequestHeaders.Remove("MCP-Protocol-Version");
        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri(fixture.Http.BaseAddress!, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, fixture.Http, ownsHttpClient: false), new McpClientOptions { ProtocolVersion = version });
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "delivery_read" && tool.ProtocolTool.OutputSchema is not null);
        var read = await client.CallToolAsync("delivery_read", new Dictionary<string, object?>());
        Assert.False(read.IsError ?? false);
        var structured = read.StructuredContent!.Value;
        using var text = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(read.Content)).Text);
        Assert.True(JsonElement.DeepEquals(text.RootElement, structured));
        Assert.Equal(DeliveryTools.Literal, structured.GetProperty("text").GetString());
        Assert.Equal(7, structured.GetProperty("revision").GetInt32());
        // This metadata is added by the current protocol; the 2025 SDK reply legitimately omits it.
        if (version is null) Assert.NotNull(read.Meta);
        var error = await client.CallToolAsync("delivery_error", new Dictionary<string, object?>());
        Assert.True(error.IsError);
        Assert.Contains("Synthetic controlled error", Assert.IsType<TextContentBlock>(Assert.Single(error.Content)).Text);
        Assert.Equal(2, fixture.State.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InputAndAdmissionGuardsKeepTheirStatusAndNeverInvokeATool(bool denied)
    {
        await using var fixture = await Start(socket: true);
        fixture.State.AllowRequests = !denied;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}") };
        request.Content.Headers.ContentType = null;
        using var response = await fixture.Http.SendAsync(request);
        Assert.Equal(denied ? HttpStatusCode.Unauthorized : HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(0, fixture.State.Calls);
    }

    [Fact]
    public async Task CancelledSocketRequestDoesNotRepeatTheToolAndNextRequestCompletes()
    {
        await using var fixture = await Start(socket: true);
        using var cancellation = new CancellationTokenSource();
        var waiting = Call(fixture.Http, "delivery_wait", cancellation.Token);
        await fixture.State.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await fixture.State.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var next = await Call(fixture.Http, "delivery_search");
        Assert.Equal("application/json", next.Content.Headers.ContentType?.MediaType);
        Assert.Equal(2, fixture.State.Calls);
    }

    [Theory]
    [InlineData("multiple")]
    [InlineData("event-id")]
    public async Task MultipleOrResumableEventsAreKeptByteForByte(string mode)
    {
        const string response = "{\"jsonrpc\":\"2.0\",\"id\":\"one\",\"result\":{\"items\":[]}}";
        var wire = mode == "multiple"
            ? "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\n"
                + "event: message\ndata: " + response + "\n\n"
            : "id: event-1\nevent: message\ndata: " + response + "\n\n";
        await using var fixture = await StartRaw(wire);
        using var result = await fixture.Http.PostAsync("/mcp", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("text/event-stream", result.Content.Headers.ContentType?.MediaType);
        Assert.Equal(wire, await result.Content.ReadAsStringAsync());
        Assert.Equal(1, fixture.State.Calls);
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n", true)]
    public async Task SingleEnvelopePreservesLiteralJsonAndMetadata(string newline, bool rpcError)
    {
        var json = rpcError
            ? "{\"jsonrpc\":\"2.0\",\"id\":\"one\",\"error\":{\"code\":-32602,\"message\":\"Invalid fixture input\"}}"
            : "{\"jsonrpc\":\"2.0\",\"id\":\"one\",\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"α 李 \\\"uno\\\"\\n<tema>\"}],\"_meta\":{\"fixture\":\"preserve\"}}}";
        await using var fixture = await StartRaw("event: message" + newline + "data: " + json + newline + newline);
        using var response = await fixture.Http.PostAsync("/mcp", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(json, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, fixture.State.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":\"one\",\"result\":\n\n")]
    [InlineData("event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\n")]
    public async Task IncompleteSingleReplyIsRejectedBeforeAnySuccessHeadersAreSent(string wire)
    {
        await using var fixture = await StartRaw(wire);
        using var result = await fixture.Http.PostAsync("/mcp", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Equal("application/problem+json", result.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("notifications/progress", await result.Content.ReadAsStringAsync());
        Assert.Equal(1, fixture.State.Calls);
    }

    [Fact]
    public async Task OversizedReplyFailsWithoutSendingPartialContentOrRetrying()
    {
        await using var fixture = await StartRaw(new string('x', 1024 * 1024 + 1));
        using var result = await fixture.Http.PostAsync("/mcp", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.True((await result.Content.ReadAsByteArrayAsync()).Length < 1024);
        Assert.Equal(1, fixture.State.Calls);
    }

    [Fact]
    public async Task NotificationAcceptanceKeeps202AndAnEmptyBody()
    {
        await using var fixture = await Start(socket: true);
        using var result = await fixture.Http.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        });
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.Empty(await result.Content.ReadAsByteArrayAsync());
        Assert.Null(result.Content.Headers.ContentType);
        Assert.Equal(0, fixture.State.Calls);
    }

    private static Task<HttpResponseMessage> Call(HttpClient http, string tool, CancellationToken cancellation = default) =>
        http.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = "delivery-1",
            method = "tools/call",
            @params = new { name = tool, arguments = new { } }
        }, cancellation);

    private static async Task<Fixture> Start(bool socket)
    {
        var builder = WebApplication.CreateBuilder();
        if (socket) builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        else builder.WebHost.UseTestServer();
        var state = new DeliveryState();
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton(new DelegationOrigin(new Uri("https://issuer.example")));
        builder.Services.AddAccountMcp();
        builder.Services.AddMcpServer().WithTools<DeliveryTools>();
        var app = builder.Build();
        app.UseAccountMcpChallenge();
        // Synthetic admission owner. Real OAuth/library isolation belongs to the existing PostgreSQL suite.
        app.MapMcp("/mcp").AddEndpointFilter((context, next) => state.AllowRequests ? next(context)
                : ValueTask.FromResult<object?>(Results.Unauthorized()))
            .AddEndpointFilter(AccountMcpModule.RejectMissingPostContentType)
            .AddEndpointFilter(new McpSingleResponseFilter().InvokeAsync);
        await app.StartAsync();
        var http = socket ? new HttpClient { BaseAddress = new Uri(app.Urls.Single()) } : app.GetTestClient();
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        http.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-06-18");
        return new(app, http, state);
    }

    private static async Task<Fixture> StartRaw(string wire)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var state = new DeliveryState();
        app.MapPost("/mcp", async (HttpContext context) =>
        {
            Interlocked.Increment(ref state.Calls);
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.ContentEncoding = "identity";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            await context.Response.StartAsync();
            await context.Response.WriteAsync(wire);
            Assert.False(context.Response.HasStarted);
        }).AddEndpointFilter(new McpSingleResponseFilter().InvokeAsync);
        await app.StartAsync();
        return new(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }, state);
    }

    public sealed record SearchResult(string[] Items);
    public sealed record ReadResult(string Text, int Revision);

    public sealed class DeliveryState
    {
        public int Calls;
        public bool AllowRequests = true;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record Fixture(WebApplication App, HttpClient Http, DeliveryState State) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { Http.Dispose(); await App.DisposeAsync(); }
    }

    [McpServerToolType]
    public sealed class DeliveryTools(DeliveryState state)
    {
        public const string Literal = "Explica <tema>.\nTexto literal: \"uno\" y \\ruta; α & 李.";

        [McpServerTool(Name = "delivery_search", ReadOnly = true, UseStructuredContent = true)]
        public SearchResult Search() { Interlocked.Increment(ref state.Calls); return new([]); }

        [McpServerTool(Name = "delivery_read", ReadOnly = true, UseStructuredContent = true)]
        public ReadResult Read() { Interlocked.Increment(ref state.Calls); return new(Literal, 7); }

        [McpServerTool(Name = "delivery_error", ReadOnly = true, UseStructuredContent = true)]
        public SearchResult Error() { Interlocked.Increment(ref state.Calls); throw new McpException("Synthetic controlled error"); }

        [McpServerTool(Name = "delivery_wait", ReadOnly = true, UseStructuredContent = true)]
        public async Task<SearchResult> Wait(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref state.Calls);
            state.Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); return new([]); }
            finally { state.Cancelled.TrySetResult(); }
        }
    }
}
