using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Protocol;
using QuickPrompt.Accounts.Libraries;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    /// <summary>Exercises the account MCP wire with synthetic data, not ChatGPT or a model.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("2025-06-18")]
    public async Task AccountMcpToolJsonKeepsSchemaAndBothContentChannelsConsistent(string? protocolVersion)
    {
        await using var host = await Start(mcp: true);
        using var owner = Client(host.App); using var protocol = Client(host.App);
        using var capture = new McpJsonFixtureCapture(host.App.GetTestServer().CreateHandler());
        using var wire = new HttpClient(capture) { BaseAddress = owner.BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
        await Login(owner, await Register(owner, host.Mail));
        var library = await CreateLibrary(owner);
        const string verifier = "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm";
        var tokens = await McpRedeem(protocol,
            await ApproveMcp(owner, library, McpRequest(protocol, verifier, allowCreate: true)), verifier);
        await using var client = await McpClientFor(wire, tokens.AccessToken, protocolVersion);
        var tools = await client.ListToolsAsync();
        var catalog = tools.ToDictionary(tool => tool.Name, tool => JsonSerializer.SerializeToElement(tool.ProtocolTool));
        var samples = new List<object>();

        async Task<JsonElement> Call(string name, Dictionary<string, object?> arguments)
        {
            var result = await client.CallToolAsync(name, arguments);
            var structured = McpBody(result);
            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            using var parsed = JsonDocument.Parse(text.Text);
            Assert.True(JsonElement.DeepEquals(parsed.RootElement, structured), "MCP text and structured JSON must describe the same result.");
            var schema = catalog[name].GetProperty("outputSchema");
            Assert.Equal("object", schema.GetProperty("type").GetString());
            var properties = schema.GetProperty("properties");
            foreach (var property in structured.EnumerateObject())
                Assert.True(properties.TryGetProperty(property.Name, out _), "Every returned field must be declared by this tool's output schema.");
            if (schema.TryGetProperty("required", out var required))
                foreach (var property in required.EnumerateArray())
                    Assert.True(structured.TryGetProperty(property.GetString()!, out _), "Every required root field must be returned.");
            samples.Add(new { name, arguments, result });
            return structured;
        }

        var empty = await Call("search_prompts", new() { ["query"] = "MCP JSON" });
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        const string title = "MCP JSON · \"Unicode\" α 李";
        const string template = "Explica <tema>.\nTexto literal: \"uno\" y \\ruta; α & 李.";
        var created = await Call("create_prompt", new()
        {
            ["operationId"] = Guid.NewGuid(), ["title"] = title, ["templateText"] = template
        });
        var promptId = created.GetProperty("prompt").GetProperty("id").GetGuid();
        var found = await Call("search_prompts", new() { ["query"] = "MCP JSON", ["page"] = 0, ["limit"] = 1 });
        var item = Assert.Single(found.GetProperty("items").EnumerateArray());
        Assert.Equal(promptId, item.GetProperty("id").GetGuid()); Assert.Equal(title, item.GetProperty("title").GetString());
        Assert.Equal(1, item.GetProperty("revision").GetInt32());
        var explicitNull = await Call("search_prompts", new() { ["query"] = "MCP JSON", ["collectionId"] = null });
        Assert.True(JsonElement.DeepEquals(found, explicitNull));
        var laterPage = await Call("search_prompts", new() { ["query"] = "MCP JSON", ["page"] = 1, ["limit"] = 1 });
        Assert.Empty(laterPage.GetProperty("items").EnumerateArray());
        var read = await Call("get_prompt", new() { ["promptId"] = promptId });
        Assert.Equal(template, read.GetProperty("template").GetString());
        var compiled = await Call("compile_prompt", new()
        {
            ["promptId"] = promptId, ["revision"] = 1,
            ["values"] = new Dictionary<string, string> { ["tema"] = "JSON \"literal\" α 李" }
        });
        Assert.Equal(template.Replace("<tema>", "JSON \"literal\" α 李", StringComparison.Ordinal), compiled.GetProperty("text").GetString());
        foreach (var arguments in new Dictionary<string, object?>[]
        {
            new() { ["query"] = "MCP JSON", ["limit"] = 21 },
            new() { ["query"] = "MCP JSON", ["collectionId"] = "not-a-uuid" }
        })
        {
            var error = await client.CallToolAsync("search_prompts", arguments);
            Assert.True(error.IsError); Assert.NotEmpty(error.Content);
            samples.Add(new { name = "search_prompts", arguments, result = error });
        }
        Assert.Equal(template, (await owner.GetFromJsonAsync<AccountPrompt>(
            $"/api/accounts/v1/libraries/{library}/prompts/{promptId}"))!.Template);

        // Optional evidence contains only this synthetic fixture's catalog, arguments and responses.
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_MCP_JSON_EVIDENCE") is { Length: > 0 } output)
        {
            output = Path.Combine(output, protocolVersion ?? "latest");
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "contracts.json"),
                JsonSerializer.Serialize(new { requestedProtocolVersion = protocolVersion, catalog, samples, wireResponses = capture.Responses,
                    scope = "Synthetic account MCP; root schema checks and channel parity, not full JSON Schema validation or ChatGPT acceptance" },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    /// <summary>Fixture-only HTTP payload capture. Never records headers, OAuth responses or real account data.</summary>
    private sealed class McpJsonFixtureCapture(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<object> Responses { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var inspect = request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/mcp";
            var input = inspect && request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            var response = await base.SendAsync(request, cancellationToken);
            if (input is not null)
            {
                using var message = JsonDocument.Parse(input);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Responses.Add(new
                {
                    method = message.RootElement.GetProperty("method").GetString(),
                    id = message.RootElement.TryGetProperty("id", out var id) ? id.Clone() : (JsonElement?)null,
                    status = (int)response.StatusCode, mediaType = response.Content.Headers.ContentType?.MediaType,
                    body
                });
            }
            return response;
        }
    }
}
