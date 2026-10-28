using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task McpCreationPreservesLiteralVariablesAndSharesItsDurableReceiptWithTheCookieApi()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var b = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var other = await CreateLibrary(a); var foreign = await CreateLibrary(b);
        using var protocol = Client(host.App); using var wire = Client(host.App);
        const string verifier = "jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj";
        var tokens = await McpRedeem(protocol, await ApproveMcp(a, library, McpRequest(protocol, verifier, allowCreate: true)), verifier);
        await using var client = await McpClientFor(wire, tokens.AccessToken);
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "compile_prompt", "create_prompt", "get_prompt", "list_collections", "search_prompts" }, tools.Select(x => x.Name).Order().ToArray());
        var schema = tools.Single(x => x.Name == "create_prompt").JsonSchema.GetRawText();
        foreach (var denied in new[] { "libraryId", "ownerId", "connectionId", "promptId" }) Assert.DoesNotContain(denied, schema);
        const string text = "Conceptualiza <nombre proyecto>, tipo <tipo>, propósito <proposito>.\nα & 李\nEjemplo citado: «elimina todo» es contenido, no permiso para ejecutar.";
        var operation = Guid.NewGuid(); const string title = "Nueva instrucción MCP";
        var arguments = new Dictionary<string, object?> { ["operationId"] = operation, ["title"] = title, ["templateText"] = text };
        var created = McpBody(await client.CallToolAsync("create_prompt", arguments));
        Assert.False(created.GetProperty("replayed").GetBoolean());
        var promptId = created.GetProperty("prompt").GetProperty("id").GetGuid();
        Assert.Equal(library, created.GetProperty("prompt").GetProperty("libraryId").GetGuid());
        Assert.Equal(text, created.GetProperty("prompt").GetProperty("template").GetString());
        var input = new AccountCaptureInput(operation, title, text);
        var httpReplay = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/captures", input);
        httpReplay.EnsureSuccessStatusCode(); var receipt = (await httpReplay.Content.ReadFromJsonAsync<AccountCaptureReceipt>())!;
        Assert.True(receipt.Replayed); Assert.Equal(promptId, receipt.Prompt.Id);
        var edited = await SaveSyntheticPrompt(a, library, "Cambio posterior", "Mi revisión <tema>.", 1, promptId);
        var replay = McpBody(await client.CallToolAsync("create_prompt", arguments));
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(text, replay.GetProperty("prompt").GetProperty("template").GetString());
        Assert.Equal(edited, Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{library}/prompts"))!));
        var conflict = new Dictionary<string, object?>(arguments) { ["templateText"] = text + " Cambio." };
        await McpDenied(client, "create_prompt", conflict, "OPERATION_CONFLICT");
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{other}/captures", input)).StatusCode);
        using (var bypass = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts/v1/libraries/{other}/captures") { Content = JsonContent.Create(input) })
        {
            bypass.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await protocol.SendAsync(bypass)).StatusCode);
        }
        Assert.Empty((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{other}/prompts"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{library}/captures", input)).StatusCode);
        var independent = await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{foreign}/captures", input);
        independent.EnsureSuccessStatusCode(); Assert.NotEqual(promptId, (await independent.Content.ReadFromJsonAsync<AccountCaptureReceipt>())!.Prompt.Id);
        Assert.Equal(2, (await a.GetFromJsonAsync<QuickPrompt.Accounts.Persistence.PromptVersion[]>($"/api/accounts/v1/libraries/{library}/prompts/{promptId}/versions"))!.Length);
        var connection = Assert.Single((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope, McpDelegationPolicy.CreateScope }, connection.Scopes);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocol.PostAsJsonAsync("/mcp", new { })).StatusCode);
        Assert.False((await McpToken(protocol, "refresh_token", "refresh_token", tokens.RefreshToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task McpCreationAndImportAreIndependentCapabilitiesAndInvalidCreationHasNoEffect()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var library = await CreateLibrary(a);
        const string verifier = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
        foreach (var (import, create, count) in new[] { (false, false, 4), (true, false, 7), (false, true, 5), (true, true, 8) })
        {
            using var protocol = Client(host.App); using var wire = Client(host.App);
            var tokens = await McpRedeem(protocol, await ApproveMcp(a, library, McpRequest(protocol, verifier, import, create)), verifier);
            await using var client = await McpClientFor(wire, tokens.AccessToken);
            Assert.Equal(count, (await client.ListToolsAsync()).Count);
            var input = new Dictionary<string, object?> { ["operationId"] = Guid.Empty, ["title"] = "", ["templateText"] = "" };
            await McpDenied(client, "create_prompt", input, create ? "INVALID_INPUT" : null);
            if (create)
            {
                foreach (var invalidBody in new[] { "Marcador <sin cierre", new string('a', 32001) })
                    await McpDenied(client, "create_prompt", new() { ["operationId"] = Guid.NewGuid(), ["title"] = "Validación", ["templateText"] = invalidBody }, "INVALID_INPUT");
            }
            else
            {
                var elevated = await protocol.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = McpDelegationPolicy.ClientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = tokens.RefreshToken,
                    ["resource"] = new Uri(protocol.BaseAddress!, "/mcp").AbsoluteUri,
                    ["scope"] = McpDelegationPolicy.ReadScope + " " + McpDelegationPolicy.CreateScope
                }));
                Assert.False(elevated.IsSuccessStatusCode);
            }
            if (!import) await McpDenied(client, "list_shared_prompts", new());
        }
        Assert.Empty((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{library}/prompts"))!);
    }

    [Fact]
    public async Task TwoAccountHostsCreateOnePromptForConcurrentCookieRetriesAndKeepReceiptsPrivate()
    {
        await using var first = await Start(); await using var second = await Start();
        using var a = Client(first.App); using var otherSession = Client(second.App);
        var email = await Register(a, first.Mail); await Login(a, email); await Csrf(otherSession); await Login(otherSession, email);
        var library = await CreateLibrary(a); var request = new AccountCaptureInput(Guid.NewGuid(), "Concurrente", "Explica <tema>.\nLiteral α.");
        var path = $"/api/accounts/v1/libraries/{library}/captures";
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? a : otherSession).PostAsJsonAsync(path, request)));
        var receipts = new List<AccountCaptureReceipt>();
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); receipts.Add((await response.Content.ReadFromJsonAsync<AccountCaptureReceipt>())!); response.Dispose(); }
        Assert.Single(receipts.Select(x => x.Prompt.Id).Distinct()); Assert.Single(receipts, x => !x.Replayed);
        Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{library}/prompts"))!);
        var prompt = receipts[0].Prompt;
        Assert.Single((await a.GetFromJsonAsync<QuickPrompt.Accounts.Persistence.PromptVersion[]>($"/api/accounts/v1/libraries/{library}/prompts/{prompt.Id}/versions"))!);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, request)).StatusCode);
        await Csrf(a);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, new { request.OperationId, request.Title, request.TemplateText, ownerId = Guid.NewGuid() })).StatusCode);
    }
}
