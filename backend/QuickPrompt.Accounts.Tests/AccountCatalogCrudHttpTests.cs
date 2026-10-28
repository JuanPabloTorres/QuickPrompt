using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task PrivateCatalogueUsesCanonicalSearchMetadataActionsAndPagingWithoutWideningIdentity()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a);
        var source = await CatalogCreate(a, library, "CAFÉ privado", "Explica <tema>.");
        await CatalogCreate(a, library, "Otro privado", "Otra instrucción.");
        var path = $"/api/accounts/v1/libraries/{library}/prompts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path + "/catalog")).StatusCode);
        var page = await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?search=cafe&size=1");
        Assert.Equal(1, page!.Total); Assert.Equal(0, page.Page); Assert.Equal(1, page.Size);
        var found = Assert.Single(page.Items); Assert.Equal(source.Id, found.Id); Assert.Equal(new[] { "tema" }, found.VariableNames);
        Assert.NotNull(found.CreatedAt); Assert.NotNull(found.UpdatedAt);
        var favorite = await CatalogAction(a, path, source, "favorite"); Assert.True(favorite.IsFavorite);
        Assert.Equal(source.Id, Assert.Single((await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?filter=favorites"))!.Items).Id);
        var archived = await CatalogAction(a, path, favorite, "archive"); Assert.True(archived.IsArchived);
        Assert.DoesNotContain((await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog"))!.Items, item => item.Id == source.Id);
        Assert.Equal(source.Id, Assert.Single((await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?filter=archived"))!.Items).Id);
        var restored = await CatalogAction(a, path, archived, "restore"); Assert.False(restored.IsArchived); Assert.True(restored.IsFavorite);
        var duplicate = await CatalogAction(a, path, restored, "duplicate");
        Assert.NotEqual(source.Id, duplicate.Id); Assert.Equal(source.Template, duplicate.Template); Assert.False(duplicate.IsFavorite);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(path + $"/{source.Id}/favorite", new { revision = 1 })).StatusCode);
        var operation = Guid.NewGuid(); var use = new { revision = restored.Revision, values = new Dictionary<string, string> { ["tema"] = "locks" }, operationId = operation, expectedText = "Explica locks." };
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(path + $"/{source.Id}/use", use)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(path + $"/{source.Id}/use", use)).StatusCode);
        var recent = Assert.Single((await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?filter=recent"))!.Items);
        Assert.Equal(source.Id, recent.Id); Assert.Equal(1, recent.Uses); Assert.NotNull(recent.LastUsedAt);
        var first = await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?size=1&page=0&sort=title");
        var second = await a.GetFromJsonAsync<AccountPromptPage>(path + "/catalog?size=1&page=1&sort=title");
        Assert.Equal(3, first!.Total); Assert.NotEqual(Assert.Single(first.Items).Id, Assert.Single(second!.Items).Id);
        // The legacy array route and all metadata share the same private records.
        Assert.Equal(3, (await a.GetFromJsonAsync<AccountPrompt[]>(path))!.Length);
        Assert.True((await a.GetFromJsonAsync<AccountPrompt>(path + $"/{source.Id}"))!.IsFavorite);
    }

    [Fact]
    public async Task CataloguePreservesLiteralWildcardsEmptyCollectionAndInvalidQueryBoundaries()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var own = await CreateLibrary(a); var foreign = await CreateLibrary(b);
        var source = await CatalogCreate(a, own, "100%_literal", "Literal wildcard.");
        await CatalogCreate(a, own, "Unrelated", "No wildcard.");
        var root = $"/api/accounts/v1/libraries/{own}";
        var foreignRoot = $"/api/accounts/v1/libraries/{foreign}";
        var empty = await (await a.PostAsJsonAsync(root + "/groups", new { name = "Vacía" })).Content.ReadFromJsonAsync<JsonElement>();
        var other = await (await b.PostAsJsonAsync(foreignRoot + "/groups", new { name = "Ajena" })).Content.ReadFromJsonAsync<JsonElement>();
        var match = await a.GetFromJsonAsync<AccountPromptPage>(root + "/prompts/catalog?search=%25_");
        Assert.Equal(source.Id, Assert.Single(match!.Items).Id);
        var emptyPage = await a.GetFromJsonAsync<AccountPromptPage>(root + $"/prompts/catalog?collectionId={empty.GetProperty("id").GetGuid()}");
        Assert.Empty(emptyPage!.Items); Assert.Equal(0, emptyPage.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(root + $"/prompts/catalog?collectionId={other.GetProperty("id").GetGuid()}")).StatusCode);
        foreach (var query in new[] { "size=0", "size=101", "page=-1", "filter=unknown", "sort=unknown", "search=" + new string('x', 201), $"collectionId={empty.GetProperty("id").GetGuid()}&ungrouped=true" })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync(root + "/prompts/catalog?" + query)).StatusCode);
    }

    [Fact]
    public async Task PrivateDeleteRequiresCsrfConfirmationAndExactRevisionAndClosesEveryPrivateRoute()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var source = await CatalogCreate(a, library, "Reviewed source", "Explain <tema>.");
        var root = $"/api/accounts/v1/libraries/{library}/prompts"; var target = root + $"/{source.Id}";
        var confirmed = new { revision = 1, confirmed = true };
        Assert.Equal(HttpStatusCode.Unauthorized, (await CatalogDelete(anonymous, target, confirmed)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CatalogDelete(b, target, confirmed)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await CatalogDelete(a, target, confirmed)).StatusCode);
        await Csrf(a);
        Assert.Equal(HttpStatusCode.BadRequest, (await CatalogDelete(a, target, new { revision = 1, confirmed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CatalogDelete(a, target, new { revision = 2, confirmed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CatalogDelete(a, target, new { revision = 1, confirmed = true, ownerId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CatalogDelete(a, target, confirmed)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CatalogDelete(a, target, confirmed)).StatusCode);
        foreach (var suffix in new[] { "", "/versions", "/share?revision=1" })
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(target + suffix)).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<AccountPromptPage>(root + "/catalog"))!.Items);
        foreach (var action in new[] { "favorite", "archive", "restore", "duplicate" })
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync(target + "/" + action, new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PutAsJsonAsync(target, new { title = "No revival", template = "Other.", revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync(target + "/render", new { revision = 1, values = new { tema = "synthetic" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync(target + "/compile", new { revision = 1, values = new { tema = "synthetic" }, operationId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task AVisibleRunMustStopBeforeItsLibraryCanEraseAPrivateSource()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = VisibleStore(scope.ServiceProvider, actor, source.Library, grant.Connection.Id);
        var record = NewVisibleRecord(); Assert.True(store.TryCreate(record));
        var target = $"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}";
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await CatalogDelete(owner, target, new { revision = 1, confirmed = true })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(target)).StatusCode);
        }
        finally { store.Cancel(record.RunId); }
        Assert.Equal(HttpStatusCode.NoContent, (await CatalogDelete(owner, target, new { revision = 1, confirmed = true })).StatusCode);
    }

    [Fact]
    public async Task ExistingReadOnlyMcpGrantCannotReadCompileOrSearchAnErasedPrivateSource()
    {
        await using var host = await Start(mcp: true); using var owner = Client(host.App); using var protocol = Client(host.App); using var wire = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var library = await CreateLibrary(owner);
        var source = await CatalogCreate(owner, library, "MCP private deletion", "Read <tema>.");
        const string verifier = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var code = await ApproveMcp(owner, library, McpRequest(protocol, verifier));
        var token = await McpRedeem(protocol, code, verifier);
        await using var client = await McpClientFor(wire, token.AccessToken);
        var readable = McpBody(await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = source.Id }));
        Assert.Equal(source.Id, readable.GetProperty("id").GetGuid());
        Assert.Equal(source.Template, readable.GetProperty("template").GetString());
        Assert.DoesNotContain(await client.ListToolsAsync(), tool => tool.Name.Contains("delete", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.NoContent, (await CatalogDelete(owner, $"/api/accounts/v1/libraries/{library}/prompts/{source.Id}", new { revision = 1, confirmed = true })).StatusCode);
        Assert.True((await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = source.Id })).IsError);
        Assert.True((await client.CallToolAsync("compile_prompt", new Dictionary<string, object?> { ["promptId"] = source.Id, ["revision"] = 1, ["values"] = new Dictionary<string, string> { ["tema"] = "synthetic" } })).IsError);
        var search = McpBody(await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["query"] = "" }));
        Assert.Empty(search.GetProperty("items").EnumerateArray());
    }

    private static async Task<AccountPrompt> CatalogCreate(HttpClient client, Guid library, string title, string template)
    {
        var response = await client.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/prompts", new { title, template, revision = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountPrompt>())!;
    }
    private static async Task<AccountPrompt> CatalogAction(HttpClient client, string path, AccountPrompt prompt, string action)
    {
        var response = await client.PostAsJsonAsync(path + $"/{prompt.Id}/{action}", new { revision = prompt.Revision });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountPrompt>())!;
    }
    private static Task<HttpResponseMessage> CatalogDelete(HttpClient client, string path, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(body) });
}
