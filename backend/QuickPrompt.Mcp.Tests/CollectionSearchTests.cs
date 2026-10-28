using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

/// <summary>Real MCP → HTTP → catalogue → disposable SQLite, with independently assigned memberships.</summary>
public sealed class CollectionSearchTests(LibraryProcess library) : IClassFixture<LibraryProcess>
{
    private Task<McpClient> Client() => McpClient.CreateAsync(new StdioClientTransport(new()
    {
        Command = "dotnet", Arguments = [typeof(QuickPromptMcpHost).Assembly.Location],
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["QuickPromptMcp__Transport"] = "stdio", ["QuickPromptMcp__ApiBaseUrl"] = library.BaseUrl.ToString(),
            ["QuickPromptMcp__AllowCreate"] = "false"
        }
    }));

    private static JsonElement Body(CallToolResult result)
    {
        Assert.False(result.IsError == true, string.Join(" ", result.Content.OfType<TextContentBlock>().Select(x => x.Text)));
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.SerializeToElement(result.StructuredContent);
    }

    [Fact]
    public async Task DiscoveryAndExactMembershipIntersectTextBeforePagingWithoutWrites()
    {
        using var http = new HttpClient { BaseAddress = library.BaseUrl };
        http.DefaultRequestHeaders.Add("X-QuickPrompt-Client", "nova-v1");
        async Task<JsonElement> Post(string path, object input)
        {
            using var response = await http.PostAsJsonAsync(path, input);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        async Task Assign(Guid prompt, Guid group)
        {
            using var response = await http.PostAsJsonAsync($"/api/v1/prompts/{prompt}/groups", new { groupId = group, revision = 1, member = true });
            response.EnsureSuccessStatusCode();
        }
        var tag = Guid.NewGuid().ToString("N");
        var first = (await Post("/api/v1/groups", new { name = $"Álgebra {tag}" })).GetProperty("id").GetGuid();
        var second = (await Post("/api/v1/groups", new { name = $"Álgebra avanzada {tag}" })).GetProperty("id").GetGuid();
        var empty = (await Post("/api/v1/groups", new { name = $"Vacía {tag}" })).GetProperty("id").GetGuid();
        async Task<Guid> Prompt(string title) => (await Post("/api/v1/prompts", new { title, templateText = "Explica <tema>." })).GetProperty("id").GetGuid();
        var member = await Prompt($"Común A {tag}");
        var shared = await Prompt($"Común B {tag}");
        var outside = await Prompt($"Común Álgebra {tag}"); // Text matches the name but is not a member of first.
        var archived = await Prompt($"Común archivado {tag}");
        await Assign(member, first); await Assign(shared, first); await Assign(shared, second);
        await Assign(outside, second); await Assign(archived, first);
        await Post($"/api/v1/prompts/{archived}/archive", new { revision = 1 });

        await using var client = await Client();
        var tools = await client.ListToolsAsync();
        var discovery = Assert.Single(tools, x => x.Name == "list_collections");
        Assert.True(discovery.ProtocolTool.Annotations?.ReadOnlyHint);
        var search = Assert.Single(tools, x => x.Name == "search_prompts");
        Assert.True(search.JsonSchema.GetProperty("properties").TryGetProperty("collectionId", out _));
        var collections = Body(await client.CallToolAsync("list_collections", new Dictionary<string, object?> { ["query"] = $"algebra {tag}" }));
        Assert.Equal(2, collections.GetProperty("total").GetInt32());
        var collection = Assert.Single(collections.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == first);
        Assert.Equal(2, collection.GetProperty("promptCount").GetInt32());
        Assert.False(collection.TryGetProperty("instructions", out _));
        var all = Body(await client.CallToolAsync("list_collections", new Dictionary<string, object?> { ["query"] = tag }));
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        var page = Body(await client.CallToolAsync("list_collections", new Dictionary<string, object?> { ["query"] = tag, ["page"] = 1, ["limit"] = 1 }));
        Assert.Equal(all.GetProperty("items")[1].GetProperty("id").GetGuid(), Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        async Task<JsonElement> Find(Guid? group, string query = "", int pageIndex = 0, int limit = 10) => Body(await client.CallToolAsync("search_prompts",
            new Dictionary<string, object?> { ["query"] = query, ["collectionId"] = group?.ToString(), ["page"] = pageIndex, ["limit"] = limit }));
        var members = await Find(first);
        Assert.Equal(2, members.GetProperty("total").GetInt32());
        Assert.Equal(new[] { member, shared }.Order(), members.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Order());
        var filtered = await Find(first, $"comun {tag}");
        Assert.Equal(2, filtered.GetProperty("total").GetInt32());
        var paged = await Find(first, $"comun {tag}", 1, 1);
        Assert.Equal(2, paged.GetProperty("total").GetInt32());
        Assert.Equal(filtered.GetProperty("items")[1].GetProperty("id").GetGuid(), Assert.Single(paged.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, (await Find(first, "ausente")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await Find(empty)).GetProperty("total").GetInt32());
        Assert.Equal(0, (await Find(Guid.NewGuid())).GetProperty("total").GetInt32());
        Assert.Equal(3, (await Find(null, $"comun {tag}")).GetProperty("total").GetInt32());
        var saved = await http.GetFromJsonAsync<JsonElement>($"/api/v1/prompts/{member}");
        Assert.Equal(1, saved.GetProperty("revision").GetInt32());
        Assert.Equal(0, saved.GetProperty("uses").GetInt32());
        var memberships = await http.GetFromJsonAsync<Guid[]>($"/api/v1/prompts/{shared}/groups");
        Assert.Equal(new[] { first, second }.Order(), memberships!.Order());
    }

    [Fact]
    public async Task InvalidCollectionAndPagingNeverFallBackToUnrestrictedSearch()
    {
        await using var client = await Client();
        foreach (var id in new[] { Guid.Empty.ToString(), "not-a-uuid" })
            Assert.True((await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["collectionId"] = id })).IsError);
        foreach (var tool in new[] { "list_collections", "search_prompts" })
        {
            foreach (var input in new Dictionary<string, object?>[]
            {
                new() { ["query"] = new string('x', 201) }, new() { ["page"] = -1 },
                new() { ["page"] = 10001 }, new() { ["limit"] = 0 }, new() { ["limit"] = 21 }
            }) Assert.True((await client.CallToolAsync(tool, input)).IsError);
            var beyond = Body(await client.CallToolAsync(tool, new Dictionary<string, object?> { ["page"] = 10000 }));
            Assert.Empty(beyond.GetProperty("items").EnumerateArray());
        }
    }
}
