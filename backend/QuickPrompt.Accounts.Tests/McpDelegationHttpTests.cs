using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static Dictionary<string, string> McpRequest(HttpClient client, string? verifier = null, bool allowImport = false, bool allowCreate = false, bool allowCollections = false) => new()
    {
        ["client_id"] = McpDelegationPolicy.ClientId,
        ["response_type"] = "code",
        ["redirect_uri"] = "https://client.example/callback",
        ["scope"] = McpDelegationPolicy.ReadScope + " offline_access" + (allowImport ? " " + McpDelegationPolicy.ImportScope : "") + (allowCreate ? " " + McpDelegationPolicy.CreateScope : "") + (allowCollections ? " " + McpDelegationPolicy.CollectionsScope : ""),
        ["resource"] = new Uri(client.BaseAddress!, "/mcp").AbsoluteUri,
        ["state"] = Guid.NewGuid().ToString("N"),
        ["code_challenge_method"] = "S256",
        ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier ?? new string('a', 64))))
    };

    private static async Task<string> ApproveMcp(HttpClient owner, Guid library, Dictionary<string, string> request)
    {
        var review = await owner.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!));
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Contains("Aprobar lectura", await review.Content.ReadAsStringAsync());
        var form = new Dictionary<string, string>(request) { ["action"] = "approve", ["library_id"] = library.ToString() };
        var response = await owner.PostAsync("/connect/authorize", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var callback = response.Headers.Location!;
        Assert.Equal("https://client.example/callback", callback.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.True(query["state"] == request["state"], "The original OAuth state must survive consent.");
        Assert.Equal(owner.BaseAddress!.GetLeftPart(UriPartial.Authority) + "/", query["iss"].ToString());
        Assert.True(query.TryGetValue("code", out var code), "Explicit consent must issue an authorization code, not an error.");
        return code.ToString();
    }

    private static Task<HttpResponseMessage> McpToken(HttpClient client, string grant, string parameter, string token, string verifier = "") =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = McpDelegationPolicy.ClientId,
            ["grant_type"] = grant,
            [parameter] = token,
            ["redirect_uri"] = "https://client.example/callback",
            ["resource"] = new Uri(client.BaseAddress!, "/mcp").AbsoluteUri,
            ["code_verifier"] = verifier
        }));

    private static async Task<TokenDocument> McpRedeem(HttpClient client, string code, string verifier)
    {
        var response = await McpToken(client, "authorization_code", "code", code, verifier);
        await AssertProtocolSuccess(response, "MCP code redemption");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!);
    }

    private static JsonElement McpBody(CallToolResult result)
    {
        Assert.False(result.IsError == true, "The authorized MCP call must succeed; sensitive payload omitted.");
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.SerializeToElement(result.StructuredContent);
    }

    private static Task<McpClient> McpClientFor(HttpClient http, string accessToken, string? protocolVersion = null) => McpClient.CreateAsync(new HttpClientTransport(new()
    {
        Endpoint = new Uri(http.BaseAddress!, "/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp,
        AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + accessToken }
    }, http, ownsHttpClient: false), new McpClientOptions { ProtocolVersion = protocolVersion });

    [Fact]
    public async Task McpCodeAndToolsAreBoundToTheApprovedLibraryAndRevocationStopsRefreshAndTools()
    {
        await using var host = await Start(mcp: true);
        using var a = Client(host.App); using var b = Client(host.App); using var protocol = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var second = await CreateLibrary(a); var foreign = await CreateLibrary(b);
        async Task<AccountPrompt> Save(HttpClient owner, Guid id, string title) => (await (await owner.PostAsJsonAsync(
            $"/api/accounts/v1/libraries/{id}/prompts", new PromptContent(title, "Explica <tema>. α & 李", 0))).Content.ReadFromJsonAsync<AccountPrompt>())!;
        var mine = await Save(a, library, "Autorizado"); var other = await Save(a, second, "No autorizado"); var theirs = await Save(b, foreign, "Privado B");
        const string verifier = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var code = await ApproveMcp(a, library, McpRequest(protocol, verifier));
        var tokens = await McpRedeem(protocol, code, verifier);
        using var wire = Client(host.App);
        await using var client = await McpClientFor(wire, tokens.AccessToken);
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "compile_prompt", "get_prompt", "list_collections", "search_prompts" }, tools.Select(x => x.Name).Order().ToArray());
        foreach (var tool in tools)
        {
            var schema = tool.JsonSchema.GetRawText();
            Assert.DoesNotContain("ownerId", schema); Assert.DoesNotContain("libraryId", schema); Assert.DoesNotContain("connectionId", schema);
        }
        var found = McpBody(await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["query"] = "" }));
        Assert.Equal(mine.Id, Assert.Single(found.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var content = McpBody(await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = mine.Id }));
        Assert.Equal(mine.Template, content.GetProperty("template").GetString());
        foreach (var denied in new[] { other.Id, theirs.Id })
            Assert.True((await client.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = denied })).IsError);
        using var protocolB = Client(host.App); using var wireB = Client(host.App);
        var codeB = await ApproveMcp(b, foreign, McpRequest(protocolB, verifier));
        var tokensB = await McpRedeem(protocolB, codeB, verifier);
        await using var clientB = await McpClientFor(wireB, tokensB.AccessToken);
        var foundB = McpBody(await clientB.CallToolAsync("search_prompts", new Dictionary<string, object?>()));
        Assert.Equal(theirs.Id, Assert.Single(foundB.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.True((await clientB.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = mine.Id })).IsError);
        var missing = McpBody(await client.CallToolAsync("compile_prompt", new Dictionary<string, object?> { ["promptId"] = mine.Id, ["revision"] = 1, ["values"] = new Dictionary<string, string>() }));
        Assert.Equal("tema", Assert.Single(missing.GetProperty("missingVariables").EnumerateArray()).GetString());
        var compiled = McpBody(await client.CallToolAsync("compile_prompt", new Dictionary<string, object?>
        {
            ["promptId"] = mine.Id,
            ["revision"] = 1,
            ["values"] = new Dictionary<string, string> { ["tema"] = "RLS" }
        }));
        Assert.Equal("Explica RLS. α & 李", compiled.GetProperty("text").GetString());
        Assert.True((await client.CallToolAsync("compile_prompt", new Dictionary<string, object?>
        {
            ["promptId"] = mine.Id,
            ["revision"] = 2,
            ["values"] = new Dictionary<string, string> { ["tema"] = "RLS" }
        })).IsError);
        Assert.True((await client.CallToolAsync("compile_prompt", new Dictionary<string, object?>
        {
            ["promptId"] = mine.Id,
            ["revision"] = 1,
            ["values"] = new Dictionary<string, string> { ["otro"] = "no autorizado" }
        })).IsError);
        Assert.True((await client.CallToolAsync("search_prompts", new Dictionary<string, object?> { ["limit"] = 21 })).IsError);
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await protocol.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocol.GetAsync($"/api/accounts/v1/libraries/{library}/prompts")).StatusCode);
        var renewed = await McpToken(protocol, "refresh_token", "refresh_token", tokens.RefreshToken);
        await AssertProtocolSuccess(renewed, "MCP refresh");
        var refreshed = await renewed.Content.ReadFromJsonAsync<JsonElement>();
        var connection = Assert.Single((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        Assert.Equal(library, connection.LibraryId); Assert.Equal(McpDelegationPolicy.LibraryProfile, connection.ProfileId);
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope }, connection.Scopes);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.GetProperty("access_token").GetString());
        var after = await protocol.PostAsJsonAsync("/mcp", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Contains("resource_metadata=", after.Headers.WwwAuthenticate.ToString());
        Assert.False((await McpToken(protocol, "refresh_token", "refresh_token", refreshed.GetProperty("refresh_token").GetString()!)).IsSuccessStatusCode);
        Assert.Equal(theirs.Template, McpBody(await clientB.CallToolAsync("get_prompt", new Dictionary<string, object?> { ["promptId"] = theirs.Id })).GetProperty("template").GetString());
    }

    [Fact]
    public async Task McpConsentRejectsMissingPkceForeignLibraryAndMissingCsrfWithoutIssuingAccess()
    {
        await using var host = await Start(mcp: true);
        using var a = Client(host.App); using var b = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var foreign = await CreateLibrary(b);
        var request = McpRequest(a); request.Remove("code_challenge"); request.Remove("code_challenge_method");
        var rejected = await a.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!));
        Assert.False(rejected.IsSuccessStatusCode);
        request = McpRequest(a); request["redirect_uri"] = "https://attacker.invalid/callback";
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!))).StatusCode);
        request = McpRequest(a); request["library_id"] = foreign.ToString(); request["action"] = "approve";
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync("/connect/authorize", new FormUrlEncodedContent(request))).StatusCode);
        request["action"] = "deny";
        var denial = await a.PostAsync("/connect/authorize", new FormUrlEncodedContent(request));
        Assert.Equal(HttpStatusCode.Redirect, denial.StatusCode);
        Assert.Equal("access_denied", QueryHelpers.ParseQuery(denial.Headers.Location!.Query)["error"].ToString());
        Assert.Equal(a.BaseAddress!.GetLeftPart(UriPartial.Authority) + "/", QueryHelpers.ParseQuery(denial.Headers.Location.Query)["iss"].ToString());
        request["action"] = "approve";
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/connect/authorize", new FormUrlEncodedContent(request))).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }

    [Fact]
    public async Task McpCodeRequiresTheOriginalVerifierAndCannotBeReplayed()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var protocol = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var library = await CreateLibrary(a);
        const string verifier = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        var code = await ApproveMcp(a, library, McpRequest(protocol, verifier));
        Assert.False((await McpToken(protocol, "authorization_code", "code", code, new string('d', 64))).IsSuccessStatusCode);
        await McpRedeem(protocol, code, verifier);
        Assert.False((await McpToken(protocol, "authorization_code", "code", code, verifier)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task McpPublishesExactResourceMetadataAndRejectsACompanionToken()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var device = Client(host.App);
        var resource = new Uri(device.BaseAddress!, "/mcp").AbsoluteUri;
        var metadata = await device.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp");
        Assert.Equal(resource, metadata.GetProperty("resource").GetString());
        Assert.Equal(new[] { McpDelegationPolicy.ReadScope, McpDelegationPolicy.ImportScope, McpDelegationPolicy.CreateScope, McpDelegationPolicy.CollectionsScope }, metadata.GetProperty("scopes_supported").EnumerateArray().Select(x => x.GetString()).ToArray());
        var initialChallenge = await device.PostAsJsonAsync("/mcp", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, initialChallenge.StatusCode);
        Assert.Contains("scope=\"prompts:read\"", initialChallenge.Headers.WwwAuthenticate.ToString());
        var discovery = await device.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server");
        var openIdDiscovery = await device.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        // Both public routes use the same live OpenIddict configuration handler,
        // including its public-client authentication-method advertisement.
        Assert.True(JsonElement.DeepEquals(discovery, openIdDiscovery), "The two live discovery routes must advertise the same configuration.");
        Assert.Equal(metadata.GetProperty("authorization_servers")[0].GetString(), discovery.GetProperty("issuer").GetString());
        Assert.True(discovery.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
        Assert.Contains(discovery.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray(), method => method.GetString() == "none");
        Assert.Equal(new Uri(device.BaseAddress!, "/connect/authorize").AbsoluteUri, discovery.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("S256", Assert.Single(discovery.GetProperty("code_challenge_methods_supported").EnumerateArray()).GetString());
        Assert.Equal(new Uri(device.BaseAddress!, "/connect/jwks").AbsoluteUri, discovery.GetProperty("jwks_uri").GetString());
        var currentKeys = await device.GetFromJsonAsync<JsonElement>("/connect/jwks");
        var legacyKeys = await device.GetFromJsonAsync<JsonElement>("/.well-known/jwks");
        Assert.NotEmpty(currentKeys.GetProperty("keys").EnumerateArray());
        Assert.Equal(currentKeys.GetRawText(), legacyKeys.GetRawText());
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_PUBLIC_METADATA_DIRECTORY") is { Length: > 0 } publicMetadataDirectory)
        {
            Directory.CreateDirectory(publicMetadataDirectory);
            foreach (var (name, document) in new[] { ("openid-configuration.json", openIdDiscovery),
                ("oauth-authorization-server.json", discovery), ("oauth-protected-resource.json", metadata) })
            {
                await using var output = new FileStream(Path.Combine(publicMetadataDirectory, name), FileMode.CreateNew, FileAccess.Write);
                await JsonSerializer.SerializeAsync(output, document);
            }
        }
        await Login(a, await Register(a, host.Mail)); var pending = await BeginDevice(device);
        await AssertProtocolSuccess(await VerifyDevice(a, pending, "approve"), "Companion approval");
        var token = await RedeemDevice(device, pending);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await device.PostAsJsonAsync("/mcp", new { })).StatusCode);
    }

    [Fact]
    public async Task McpRejectsScopeOrResourceEscalationBeforeConsentAndStampRevokesItsOwnConnection()
    {
        await using var host = await Start(mcp: true); using var a = Client(host.App); using var protocol = Client(host.App);
        await Login(a, await Register(a, host.Mail)); var library = await CreateLibrary(a);
        var request = McpRequest(protocol); request["scope"] += " " + DelegationPolicy.Scope;
        var rejectedScope = await a.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!));
        Assert.False(rejectedScope.IsSuccessStatusCode);
        Assert.False(QueryHelpers.ParseQuery(rejectedScope.Headers.Location?.Query ?? "").ContainsKey("code"));
        request = McpRequest(protocol); request["resource"] = new Uri(protocol.BaseAddress!, "/api/companion").AbsoluteUri;
        var rejectedResource = await a.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!));
        Assert.False(rejectedResource.IsSuccessStatusCode);
        Assert.False(QueryHelpers.ParseQuery(rejectedResource.Headers.Location?.Query ?? "").ContainsKey("code"));
        Assert.Empty((await a.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        const string verifier = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var tokens = await McpRedeem(protocol, await ApproveMcp(a, library, McpRequest(protocol, verifier)), verifier);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/account/logout-all", new { })).StatusCode);
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await protocol.PostAsJsonAsync("/mcp", new { })).StatusCode);
        Assert.False((await McpToken(protocol, "refresh_token", "refresh_token", tokens.RefreshToken)).IsSuccessStatusCode);
    }
}
