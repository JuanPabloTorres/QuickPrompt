using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task HttpCollectionSharesFreezeTheCopyAndDoNotExpandExistingMcpGrants()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var b = Client(host.App); using var c = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var email = await Register(b, host.Mail); await Login(b, email); await Login(c, await Register(c, host.Mail));
        var origin = await CreateLibrary(a); var target = await CreateLibrary(b); var root = $"/api/accounts/v1/libraries/{origin}";
        var one = await SaveSyntheticPrompt(a, origin, "Uno", "Analiza <tema>."); var two = await SaveSyntheticPrompt(a, origin, "Dos", "Resume <texto>.");
        var groupResponse = await a.PostAsJsonAsync(root + "/groups", new GroupInput("Colección compartida")); groupResponse.EnsureSuccessStatusCode();
        var group = (await groupResponse.Content.ReadFromJsonAsync<PromptGroup>())!;
        foreach (var prompt in new[] { one, two }) Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(root + $"/prompts/{prompt.Id}/groups", new MembershipInput(group.Id, 1, true))).StatusCode);
        var input = new CollectionPublicationInput(origin, group.Id, 3, new() { [one.Id] = 1, [two.Id] = 1 });
        a.DefaultRequestHeaders.Remove("X-QP-CSRF"); Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/accounts/v1/sharing/collections", input)).StatusCode); await Csrf(a);
        var created = await a.PostAsJsonAsync("/api/accounts/v1/sharing/collections", input); created.EnsureSuccessStatusCode();
        var publication = (await created.Content.ReadFromJsonAsync<SharedCollection>())!; Assert.Equal(2, publication.Items.Length);
        var collectionPath = $"/api/accounts/v1/sharing/collections/{publication.Id}"; var grantPath = $"/api/accounts/v1/sharing/publications/{publication.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(collectionPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsJsonAsync(grantPath + "/invite", new InvitationRequest(email))).StatusCode);
        var invite = Assert.Single((await b.GetFromJsonAsync<SharedInvitation[]>("/api/accounts/v1/sharing/invitations"))!); Assert.True(invite.IsCollection);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(collectionPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsJsonAsync($"/api/accounts/v1/sharing/invitations/{invite.Id}/decision", new InvitationDecision(InvitationState.Accepted))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(collectionPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(grantPath)).StatusCode);

        using var protocol = Client(host.App); using var wire = Client(host.App);
        const string verifier = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
        var tokens = await McpRedeem(protocol, await ApproveMcp(b, target, McpRequest(protocol, verifier, true)), verifier);
        await using var mcp = await McpClientFor(wire, tokens.AccessToken);
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "get_shared_prompt", "import_shared_prompt", "list_collections", "list_shared_prompts", "search_prompts" },
            (await mcp.ListToolsAsync()).Select(tool => tool.Name).Order().ToArray());
        Assert.Empty(McpBody(await mcp.CallToolAsync("list_shared_prompts", new Dictionary<string, object?>())).GetProperty("items").EnumerateArray());
        await McpDenied(mcp, "get_shared_prompt", new() { ["publicationId"] = publication.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(mcp, "import_shared_prompt", new() { ["publicationId"] = publication.Id, ["operationId"] = Guid.NewGuid() }, "RESOURCE_UNAVAILABLE");
        using var bearer = Client(host.App); bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync(collectionPath)).StatusCode);

        var operation = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(collectionPath + "/import", new ImportRequest(origin, operation))).StatusCode);
        var imported = await b.PostAsJsonAsync(collectionPath + "/import", new ImportRequest(target, operation)); imported.EnsureSuccessStatusCode();
        var copy = (await imported.Content.ReadFromJsonAsync<ImportedCollection>())!; Assert.Equal(2, copy.Prompts.Length);
        var replay = await b.PostAsJsonAsync(collectionPath + "/import", new ImportRequest(target, operation)); replay.EnsureSuccessStatusCode();
        Assert.Equal(copy.Id, (await replay.Content.ReadFromJsonAsync<ImportedCollection>())!.Id);
        await SaveSyntheticPrompt(a, origin, "Cambio privado", "Texto no compartido.", 1, one.Id);
        Assert.Equal(publication.Items, (await b.GetFromJsonAsync<SharedCollection>(collectionPath))!.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(grantPath + "/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(collectionPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(collectionPath + "/import", new ImportRequest(target, operation))).StatusCode);
        Assert.Equal(2, (await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{target}/prompts?collectionId={copy.Id}"))!.Length);
    }
}
