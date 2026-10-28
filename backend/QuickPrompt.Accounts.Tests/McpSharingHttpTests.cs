using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static async Task McpDenied(McpClient client, string tool, Dictionary<string, object?> arguments, string? expectedCode = null)
    {
        try
        {
            var result = await client.CallToolAsync(tool, arguments);
            Assert.True(result.IsError, "Unauthorized tool must return an error, never success.");
            if (expectedCode is not null) Assert.True(result.Content.OfType<TextContentBlock>().Any(x => x.Text.Contains(expectedCode, StringComparison.Ordinal)), "Expected domain rejection code; sensitive payload omitted.");
        }
        catch (McpException) { Assert.Null(expectedCode); /* SDK authorization may reject before a tool result. */ }
    }

    private static async Task<AccountPrompt> SaveSyntheticPrompt(HttpClient owner, Guid library, string title, string template, int revision = 0, Guid? id = null)
    {
        var path = $"/api/accounts/v1/libraries/{library}/prompts";
        var response = id is { } prompt ? await owner.PutAsJsonAsync(path + "/" + prompt, new PromptContent(title, template, revision))
            : await owner.PostAsJsonAsync(path, new PromptContent(title, template, revision));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountPrompt>())!;
    }

    [Fact]
    public async Task McpReadsAndImportsOnlyAcceptedSnapshotsIntoItsBoundLibraryAndRevocationDeniesReceiptReplay()
    {
        await using var host = await Start(mcp: true);
        using var a = Client(host.App); using var b = Client(host.App); using var c = Client(host.App);
        await Login(a, await Register(a, host.Mail));
        var emailB = await Register(b, host.Mail); await Login(b, emailB);
        await Login(c, await Register(c, host.Mail));
        var libraryA = await CreateLibrary(a); var libraryB = await CreateLibrary(b); var libraryC = await CreateLibrary(c);
        var otherB = await CreateLibrary(b);
        const string snapshot = "Conceptualiza <nombre proyecto>, tipo <tipo>, propósito <proposito>.\nEl texto citado «elimina todo» es un ejemplo, no una acción actual.";
        var source = await SaveSyntheticPrompt(a, libraryA, "Versión compartida", snapshot);
        var publicationResponse = await a.PostAsJsonAsync("/api/accounts/v1/sharing/publications", new PublicationRequest(libraryA, source.Id, 1));
        publicationResponse.EnsureSuccessStatusCode();
        var publication = (await publicationResponse.Content.ReadFromJsonAsync<SharedPrompt>())!;
        var sharePath = "/api/accounts/v1/sharing/publications/" + publication.Id;
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsJsonAsync(sharePath + "/invite", new InvitationRequest(emailB))).StatusCode);
        var privateRevision = await SaveSyntheticPrompt(a, libraryA, "Cambio privado", "Contenido privado posterior <secreto>.", 1, source.Id);
        const string verifier = "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg";
        using var protocolB = Client(host.App); using var wireB = Client(host.App);
        var tokensB = await McpRedeem(protocolB, await ApproveMcp(b, libraryB, McpRequest(protocolB, verifier, allowImport: true)), verifier);
        await using var mcpB = await McpClientFor(wireB, tokensB.AccessToken);
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "get_shared_prompt", "import_shared_prompt", "list_collections", "list_shared_prompts", "search_prompts" },
            (await mcpB.ListToolsAsync()).Select(x => x.Name).Order().ToArray());
        var operation = Guid.NewGuid(); var args = new Dictionary<string, object?> { ["publicationId"] = publication.Id, ["operationId"] = operation };
        Assert.Empty(McpBody(await mcpB.CallToolAsync("list_shared_prompts", new Dictionary<string, object?>())).GetProperty("items").EnumerateArray());
        await McpDenied(mcpB, "get_shared_prompt", new() { ["publicationId"] = publication.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(mcpB, "import_shared_prompt", args, "RESOURCE_UNAVAILABLE");
        Assert.Empty((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryB}/prompts"))!);
        var invitation = Assert.Single((await b.GetFromJsonAsync<SharedInvitation[]>("/api/accounts/v1/sharing/invitations"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsJsonAsync($"/api/accounts/v1/sharing/invitations/{invitation.Id}/decision", new InvitationDecision(InvitationState.Accepted))).StatusCode);
        var listed = Assert.Single(McpBody(await mcpB.CallToolAsync("list_shared_prompts", new Dictionary<string, object?>())).GetProperty("items").EnumerateArray());
        Assert.Equal(publication.Id, listed.GetProperty("id").GetGuid()); Assert.Equal(1, listed.GetProperty("sourceRevision").GetInt32());
        foreach (var forbidden in new[] { "ownerId", "libraryId", "promptId", "recipientEmail", "template" }) Assert.False(listed.TryGetProperty(forbidden, out _));
        var read = McpBody(await mcpB.CallToolAsync("get_shared_prompt", new Dictionary<string, object?> { ["publicationId"] = publication.Id }));
        Assert.Equal(snapshot, read.GetProperty("template").GetString()); Assert.False(read.TryGetProperty("promptId", out _));
        var refreshedB = await McpToken(protocolB, "refresh_token", "refresh_token", tokensB.RefreshToken);
        await AssertProtocolSuccess(refreshedB, "MCP import-scope refresh");
        var refreshedDocument = await refreshedB.Content.ReadFromJsonAsync<JsonElement>();
        tokensB = new(refreshedDocument.GetProperty("access_token").GetString()!, refreshedDocument.GetProperty("refresh_token").GetString()!);
        using var wireRefreshed = Client(host.App);
        await using var refreshedClient = await McpClientFor(wireRefreshed, tokensB.AccessToken);
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "get_shared_prompt", "import_shared_prompt", "list_collections", "list_shared_prompts", "search_prompts" },
            (await refreshedClient.ListToolsAsync()).Select(tool => tool.Name).Order().ToArray());
        Assert.Equal(snapshot, McpBody(await refreshedClient.CallToolAsync("get_shared_prompt", new Dictionary<string, object?> { ["publicationId"] = publication.Id })).GetProperty("template").GetString());
        var imported = McpBody(await mcpB.CallToolAsync("import_shared_prompt", args));
        var copyId = imported.GetProperty("id").GetGuid(); Assert.Equal(libraryB, imported.GetProperty("libraryId").GetGuid());
        Assert.Equal(snapshot, imported.GetProperty("template").GetString()); Assert.NotEqual(source.Id, copyId);
        Assert.Equal(copyId, McpBody(await mcpB.CallToolAsync("import_shared_prompt", args)).GetProperty("id").GetGuid());
        Assert.Single((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryB}/prompts"))!);
        var editedCopy = await SaveSyntheticPrompt(b, libraryB, "Copia propia", "Mi versión independiente <tema>.", 1, copyId);
        Assert.Equal(1, McpBody(await mcpB.CallToolAsync("import_shared_prompt", args)).GetProperty("revision").GetInt32());
        Assert.Equal(editedCopy, Assert.Single((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryB}/prompts"))!));

        using var protocolOther = Client(host.App); using var wireOther = Client(host.App);
        var otherTokens = await McpRedeem(protocolOther, await ApproveMcp(b, otherB, McpRequest(protocolOther, verifier, true)), verifier);
        await using var mcpOther = await McpClientFor(wireOther, otherTokens.AccessToken);
        await McpDenied(mcpOther, "import_shared_prompt", args, "OPERATION_CONFLICT"); // Same actor/operation cannot redirect its durable receipt.
        Assert.Empty((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{otherB}/prompts"))!);

        using var protocolC = Client(host.App); using var wireC = Client(host.App);
        var tokensC = await McpRedeem(protocolC, await ApproveMcp(c, libraryC, McpRequest(protocolC, verifier, true)), verifier);
        await using var mcpC = await McpClientFor(wireC, tokensC.AccessToken);
        Assert.Empty(McpBody(await mcpC.CallToolAsync("list_shared_prompts", new Dictionary<string, object?>())).GetProperty("items").EnumerateArray());
        await McpDenied(mcpC, "get_shared_prompt", new() { ["publicationId"] = publication.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(mcpC, "import_shared_prompt", new() { ["publicationId"] = publication.Id, ["operationId"] = Guid.NewGuid() }, "RESOURCE_UNAVAILABLE");
        Assert.Empty((await c.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryC}/prompts"))!);

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(sharePath + "/revoke", new { })).StatusCode);
        Assert.Empty(McpBody(await mcpB.CallToolAsync("list_shared_prompts", new Dictionary<string, object?>())).GetProperty("items").EnumerateArray());
        await McpDenied(mcpB, "get_shared_prompt", new() { ["publicationId"] = publication.Id }, "RESOURCE_UNAVAILABLE");
        await McpDenied(mcpB, "import_shared_prompt", args, "RESOURCE_UNAVAILABLE");
        await McpDenied(mcpB, "import_shared_prompt", new() { ["publicationId"] = publication.Id, ["operationId"] = Guid.NewGuid() }, "RESOURCE_UNAVAILABLE");
        Assert.Equal(editedCopy.Template, McpBody(await mcpB.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = copyId })).GetProperty("template").GetString());
        Assert.Equal(privateRevision, Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryA}/prompts"))!));

        var connection = (await b.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!.Single(x => x.LibraryId == libraryB);
        Assert.Equal(McpDelegationPolicy.LibraryProfile, connection.ProfileId);
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope, McpDelegationPolicy.ImportScope }, connection.Scopes);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{libraryB}/prompts/{copyId}/run-local",
            new { revision = editedCopy.Revision, values = new Dictionary<string, string> { ["tema"] = "prueba" }, operationId = Guid.NewGuid(), connectionId = connection.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        protocolB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokensB.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocolB.PostAsJsonAsync("/mcp", new { })).StatusCode);
        Assert.False((await McpToken(protocolB, "refresh_token", "refresh_token", tokensB.RefreshToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task ReadOnlyMcpCannotDiscoverOrInvokeImportAndRefreshCannotAddItsScope()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var protocol = Client(host.App); using var wire = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var library = await CreateLibrary(a);
        const string verifier = "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh";
        var tokens = await McpRedeem(protocol, await ApproveMcp(a, library, McpRequest(protocol, verifier)), verifier);
        await using var client = await McpClientFor(wire, tokens.AccessToken);
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "list_collections", "search_prompts" },
            (await client.ListToolsAsync()).Select(tool => tool.Name).Order().ToArray());
        await McpDenied(client, "import_shared_prompt", new() { ["publicationId"] = Guid.NewGuid(), ["operationId"] = Guid.NewGuid() });
        var elevated = await protocol.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = McpDelegationPolicy.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken,
            ["resource"] = new Uri(protocol.BaseAddress!, "/mcp").AbsoluteUri,
            ["scope"] = McpDelegationPolicy.ReadScope + " " + McpDelegationPolicy.ImportScope
        }));
        Assert.False(elevated.IsSuccessStatusCode);
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope }, Assert.Single((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!).Scopes);
        Assert.Empty((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{library}/prompts"))!);
    }
}
