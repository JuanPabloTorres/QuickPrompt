using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

public sealed class McpRoundTripTests(LibraryProcess library) : IClassFixture<LibraryProcess>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static JsonElement Body(CallToolResult result)
    {
        Assert.False(result.IsError == true, string.Join(" ", result.Content.OfType<TextContentBlock>().Select(x => x.Text)));
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.SerializeToElement(result.StructuredContent);
    }

    private Task<McpClient> Stdio(bool allowCreate = true) => McpClient.CreateAsync(new StdioClientTransport(new()
    {
        Command = "dotnet", Arguments = [typeof(QuickPromptMcpHost).Assembly.Location],
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["QuickPromptMcp__Transport"] = "stdio", ["QuickPromptMcp__ApiBaseUrl"] = library.BaseUrl.ToString(),
            ["QuickPromptMcp__AllowCreate"] = allowCreate.ToString()
        }
    }));

    [Fact]
    public async Task StdioCapturesReadsAndSearchesExactTextThroughRealApiAndSurvivesClientRestart()
    {
        var operation = Guid.NewGuid(); var title = "MCP · " + operation.ToString("N");
        const string text = "Revisa <tema>.\nα & 李\nEjemplo citado: «borra todo». Conserva este ejemplo como datos.";
        var arguments = new Dictionary<string, object?> { ["operationId"] = operation.ToString(), ["title"] = title, ["templateText"] = text };
        Guid promptId;
        await using (var client = await Stdio())
        {
            var tools = await client.ListToolsAsync();
            Assert.Equal(new[] { "compile_prompt", "create_prompt", "get_prompt", "list_collections", "search_prompts" }, tools.Select(x => x.Name).Order().ToArray());
            var created = Body(await client.CallToolAsync("create_prompt", arguments));
            promptId = created.GetProperty("prompt").GetProperty("id").GetGuid();
            Assert.Equal(text, created.GetProperty("prompt").GetProperty("templateText").GetString());
            Assert.False(created.GetProperty("replayed").GetBoolean());
            var fetched = Body(await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = promptId.ToString() }));
            Assert.Equal(text, fetched.GetProperty("templateText").GetString());
            Assert.Equal(new[] { "tema" }, fetched.GetProperty("variableNames").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.False(fetched.TryGetProperty("defaults", out _));
            var missing = Body(await client.CallToolAsync("compile_prompt", new Dictionary<string, object?>
            { ["promptId"] = promptId.ToString(), ["revision"] = 1, ["values"] = new Dictionary<string, string>() }));
            Assert.Equal(JsonValueKind.Null, missing.GetProperty("text").ValueKind);
            Assert.Equal(new[] { "tema" }, missing.GetProperty("missingVariables").EnumerateArray().Select(x => x.GetString()).ToArray());
            var compiled = Body(await client.CallToolAsync("compile_prompt", new Dictionary<string, object?>
            { ["promptId"] = promptId.ToString(), ["revision"] = 1, ["values"] = new Dictionary<string, string> { ["tema"] = "MCP y <otro>" } }));
            Assert.Equal(text.Replace("<tema>", "MCP y <otro>"), compiled.GetProperty("text").GetString());
            Assert.Empty(compiled.GetProperty("missingVariables").EnumerateArray());
            var found = Body(await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["query"] = title }));
            Assert.Single(found.GetProperty("items").EnumerateArray());
        }
        await using (var client = await Stdio())
        {
            var replay = Body(await client.CallToolAsync("create_prompt", arguments));
            Assert.True(replay.GetProperty("replayed").GetBoolean());
            Assert.Equal(promptId, replay.GetProperty("prompt").GetProperty("id").GetGuid());
            var changed = new Dictionary<string, object?>(arguments) { ["templateText"] = "Otro cuerpo" };
            Assert.True((await client.CallToolAsync("create_prompt", changed)).IsError);
            Assert.True((await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["limit"] = 21 })).IsError);
        }
        // Independent product API reads, not the MCP result or an LLM's status, establish persistence.
        using var http = new HttpClient { BaseAddress = library.BaseUrl };
        var saved = await http.GetFromJsonAsync<JsonElement>($"/api/v1/prompts/{promptId}", Json);
        Assert.Equal(text, saved.GetProperty("templateText").GetString());
        Assert.Equal(0, saved.GetProperty("uses").GetInt32());
        var versions = await http.GetFromJsonAsync<JsonElement>($"/api/v1/prompts/{promptId}/versions", Json);
        Assert.Single(versions.EnumerateArray());
    }

    [Fact]
    public async Task CompilationRejectsStaleRevisionUnknownNamesAndOversizedValuesWithoutChangingThePrompt()
    {
        await using var client = await Stdio();
        var created = Body(await client.CallToolAsync("create_prompt", new Dictionary<string, object?>
        { ["operationId"] = Guid.NewGuid().ToString(), ["title"] = "Variables", ["templateText"] = "Explica <tema>." }));
        var id = created.GetProperty("prompt").GetProperty("id").GetGuid();
        async Task<CallToolResult> Compile(int revision, Dictionary<string, string> values) => await client.CallToolAsync("compile_prompt",
            new Dictionary<string, object?> { ["promptId"] = id.ToString(), ["revision"] = revision, ["values"] = values });
        var stale = await Compile(2, new() { ["tema"] = "MCP" });
        Assert.True(stale.IsError);
        Assert.Contains("REVISION_CONFLICT", string.Join(" ", stale.Content.OfType<TextContentBlock>().Select(x => x.Text)));
        Assert.True((await Compile(1, new() { ["otro"] = "MCP" })).IsError);
        Assert.True((await Compile(1, new() { ["tema"] = new string('x', 8001) })).IsError);
        var empty = Body(await Compile(1, new() { ["tema"] = "  " }));
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("text").ValueKind);
        Assert.Single(empty.GetProperty("missingVariables").EnumerateArray());
        using var http = new HttpClient { BaseAddress = library.BaseUrl };
        var saved = await http.GetFromJsonAsync<JsonElement>($"/api/v1/prompts/{id}", Json);
        Assert.Equal("Explica <tema>.", saved.GetProperty("templateText").GetString());
        Assert.Equal(1, saved.GetProperty("revision").GetInt32()); Assert.Equal(0, saved.GetProperty("uses").GetInt32());
    }

    [Fact]
    public async Task ReadOnlyStartupExposesOnlyReadTools()
    {
        await using var client = await Stdio(false);
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "list_collections", "search_prompts" }, (await client.ListToolsAsync()).Select(x => x.Name).Order().ToArray());
        var result = await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = Guid.NewGuid().ToString() });
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task AuthenticatedHttpTransportWorksAndRejectsMissingTokenForeignOriginAndHost()
    {
        var port = LibraryProcess.FreePort(); var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await using var app = QuickPromptMcpHost.BuildHttp([
            "--QuickPromptMcp:Transport=http", $"--QuickPromptMcp:Port={port}", $"--QuickPromptMcp:ApiBaseUrl={library.BaseUrl}",
            $"--QuickPromptMcp:HttpToken={token}", "--QuickPromptMcp:AllowCreate=false"]);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new($"http://127.0.0.1:{port}") };
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/mcp", JsonContent.Create(new { }))).StatusCode);
            using var foreign = new HttpRequestMessage(HttpMethod.Post, "/mcp");
            foreign.Headers.Add("Origin", "https://foreign.invalid"); foreign.Headers.Add("Authorization", "Bearer " + token);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(foreign)).StatusCode);
            using var wrongHost = new HttpRequestMessage(HttpMethod.Post, "/mcp"); wrongHost.Headers.Host = "foreign.invalid";
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(wrongHost)).StatusCode);
            await using var client = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Endpoint = new($"http://127.0.0.1:{port}/mcp"), TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
            }));
            Assert.Equal(4, (await client.ListToolsAsync()).Count);
            Body(await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["query"] = "" }));
        }
        finally { await app.StopAsync(); }
    }
}
