using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task PrivateMcpCollectionsStayInTheApprovedLibraryForThreeAccountsAndNeverWidenAnEmptyFilter()
    {
        await using var host = await Start(mcp: true);
        using var a = Client(host.App); using var b = Client(host.App); using var c = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail)); await Login(c, await Register(c, host.Mail));
        var libraryA = await CreateLibrary(a); var otherA = await CreateLibrary(a); var libraryB = await CreateLibrary(b); var libraryC = await CreateLibrary(c);
        var promptA = await SaveSyntheticPrompt(a, libraryA, "Incluido A", "Contenido A.");
        await SaveSyntheticPrompt(a, libraryA, "Sin colección A", "Contenido fuera de la colección.");
        var promptB = await SaveSyntheticPrompt(b, libraryB, "Incluido B", "Contenido B.");
        var promptC = await SaveSyntheticPrompt(c, libraryC, "Incluido C", "Contenido C.");
        async Task<PromptGroup> Collection(HttpClient owner, Guid library, string name, AccountPrompt? member = null)
        {
            using var created = await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/groups", new GroupInput(name));
            created.EnsureSuccessStatusCode(); var result = (await created.Content.ReadFromJsonAsync<PromptGroup>())!;
            if (member is not null)
                Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts/{member.Id}/groups", new MembershipInput(result.Id, member.Revision, true))).StatusCode);
            return result;
        }
        var collectionA = await Collection(a, libraryA, "Formación", promptA);
        var emptyA = await Collection(a, libraryA, "Vacía");
        var hiddenA = await Collection(a, otherA, "Formación");
        var collectionB = await Collection(b, libraryB, "Formación", promptB);
        var collectionC = await Collection(c, libraryC, "Formación", promptC);
        using var protocolA = Client(host.App); using var protocolB = Client(host.App); using var protocolC = Client(host.App);
        using var wireA = Client(host.App); using var wireB = Client(host.App); using var wireC = Client(host.App);
        var verifier = new string('y', 64);
        var tokenA = await McpRedeem(protocolA, await ApproveMcp(a, libraryA, McpRequest(protocolA, verifier)), verifier);
        var tokenB = await McpRedeem(protocolB, await ApproveMcp(b, libraryB, McpRequest(protocolB, verifier)), verifier);
        var tokenC = await McpRedeem(protocolC, await ApproveMcp(c, libraryC, McpRequest(protocolC, verifier)), verifier);
        await using var clientA = await McpClientFor(wireA, tokenA.AccessToken);
        await using var clientB = await McpClientFor(wireB, tokenB.AccessToken);
        await using var clientC = await McpClientFor(wireC, tokenC.AccessToken);
        foreach (var (client, expected) in new[] { (clientA, collectionA), (clientB, collectionB), (clientC, collectionC) })
        {
            var collections = McpBody(await client.CallToolAsync("list_collections", new Dictionary<string, object?> { ["query"] = "FORMacion" }));
            var item = Assert.Single(collections.GetProperty("items").EnumerateArray());
            Assert.Equal(expected.Id, item.GetProperty("id").GetGuid()); Assert.Equal(1, item.GetProperty("promptCount").GetInt32());
            Assert.Equal(1, collections.GetProperty("total").GetInt32());
            Assert.Equal(new[] { "id", "name", "promptCount" }, item.EnumerateObject().Select(property => property.Name).Order());
            var schema = (await client.ListToolsAsync()).Single(tool => tool.Name == "list_collections").JsonSchema.GetRawText();
            Assert.DoesNotContain("ownerId", schema); Assert.DoesNotContain("libraryId", schema);
        }
        var included = McpBody(await clientA.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["collectionId"] = collectionA.Id }));
        Assert.Equal(promptA.Id, Assert.Single(included.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var empty = McpBody(await clientA.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["collectionId"] = emptyA.Id }));
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        var all = McpBody(await clientA.CallToolAsync("search_prompts", new Dictionary<string, object?>()));
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        foreach (var foreign in new[] { hiddenA.Id, collectionB.Id, collectionC.Id, Guid.NewGuid() })
            await McpDenied(clientA, "search_prompts", new Dictionary<string, object?> { ["collectionId"] = foreign }, "RESOURCE_UNAVAILABLE");
        await McpDenied(clientB, "search_prompts", new Dictionary<string, object?> { ["collectionId"] = collectionA.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(clientC, "search_prompts", new Dictionary<string, object?> { ["collectionId"] = collectionA.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(clientA, "search_prompts", new Dictionary<string, object?> { ["collectionId"] = Guid.Empty }, "INVALID_INPUT");
        await McpDenied(clientA, "list_collections", new Dictionary<string, object?> { ["limit"] = 21 }, "INVALID_INPUT");
        var later = McpBody(await clientA.CallToolAsync("list_collections", new Dictionary<string, object?> { ["page"] = 10000, ["limit"] = 20 }));
        Assert.Empty(later.GetProperty("items").EnumerateArray()); Assert.Equal(2, later.GetProperty("total").GetInt32());
        var connection = Assert.Single((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope }, connection.Scopes);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        protocolA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocolA.PostAsJsonAsync("/mcp", new { })).StatusCode);
        var surviving = McpBody(await clientB.CallToolAsync("list_collections", new Dictionary<string, object?>()));
        Assert.Equal(collectionB.Id, Assert.Single(surviving.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }
}
