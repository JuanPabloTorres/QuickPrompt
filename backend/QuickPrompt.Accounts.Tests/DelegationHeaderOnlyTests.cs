using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class DelegationHeaderOnlyTests
{
    [Fact]
    public void ResourceValidationExtractsOnlyAuthorizationHeaderCredentials()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.AddAccountDelegation(new Uri("https://127.0.0.1:5299"));
        using var services = builder.Services.BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<OpenIddictValidationAspNetCoreOptions>>().Value;
        Assert.False(options.DisableAccessTokenExtractionFromAuthorizationHeader);
        Assert.True(options.DisableAccessTokenExtractionFromBodyForm);
        Assert.True(options.DisableAccessTokenExtractionFromQueryString);
    }
}

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task McpRejectsUrlAndFormTokensWhileHeaderAuthenticationStillWorks()
    {
        await using var host = await Start(mcp: true);
        using var owner = Client(host.App); using var protocol = Client(host.App); using var wire = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var library = await CreateLibrary(owner);
        var verifier = new string('z', 64);
        var tokens = await McpRedeem(protocol, await ApproveMcp(owner, library, McpRequest(protocol, verifier)), verifier);

        // These synthetic credentials are deliberately sent through forbidden transports
        // to prove rejection. Assertions and logs must never include their literal value.
        using var urlRequest = HeaderOnlyMcpProbe("/mcp?access_token=" + Uri.EscapeDataString(tokens.AccessToken));
        using var urlOnly = await wire.SendAsync(urlRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, urlOnly.StatusCode);
        Assert.Contains("resource_metadata=", urlOnly.Headers.WwwAuthenticate.ToString());
        using var formRequest = HeaderOnlyMcpProbe("/mcp");
        formRequest.Content!.Dispose();
        formRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = tokens.AccessToken });
        using var formOnly = await wire.SendAsync(formRequest);
        // MCP's JSON-only endpoint rejects this media type before authentication.
        // This proves a transport rejection; the pure options test proves form extraction is disabled.
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, formOnly.StatusCode);
        using var headerAndForm = HeaderOnlyMcpProbe("/mcp");
        headerAndForm.Content!.Dispose();
        headerAndForm.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = tokens.AccessToken });
        headerAndForm.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var rejectedMediaType = await wire.SendAsync(headerAndForm);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, rejectedMediaType.StatusCode);

        using var both = HeaderOnlyMcpProbe("/mcp?access_token=" + Uri.EscapeDataString(tokens.AccessToken));
        both.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-synthetic-token");
        using var rejectedHeader = await wire.SendAsync(both);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedHeader.StatusCode);

        // Prove the exact POST/RPC probe is otherwise accepted, so route or
        // method rejection cannot masquerade as a credential-transport guard.
        using var validHeader = HeaderOnlyMcpProbe("/mcp");
        validHeader.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var initialized = await wire.SendAsync(validHeader);
        Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);

        await using var client = await McpClientFor(wire, tokens.AccessToken);
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "search_prompts");
    }

    private static HttpRequestMessage HeaderOnlyMcpProbe(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new {
                protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "synthetic-header-probe", version = "1.0" }
            } })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }
}
