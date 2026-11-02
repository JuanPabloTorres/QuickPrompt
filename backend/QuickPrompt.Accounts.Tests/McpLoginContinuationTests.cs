using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class McpLoginContinuationTests
{
    private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
    private static readonly DelegationOrigin Origin = new(new Uri("https://quickprompt.example"));
    private static readonly McpAccountSettings Settings = new(new Uri("https://chatgpt.com/connector_platform_oauth_redirect"));

    private static OpenIddictRequest Request(string state = "flow-state") => new()
    {
        ClientId = "quickprompt-mcp", RedirectUri = Settings.RedirectUri!.AbsoluteUri,
        ResponseType = "code", Scope = "prompts:read prompts:create offline_access", Resources = [Origin.McpResource],
        State = state, CodeChallenge = new string('a', 43), CodeChallengeMethod = "S256"
    };

    [Theory]
    [InlineData(null)]
    [InlineData("query")]
    [InlineData("form_post")]
    [InlineData("fragment")]
    public void TheExactProtocolFieldsReturnOnlyToAuthorize(string? responseMode)
    {
        var request = Request("first & second = Ω"); request.ResponseMode = responseMode;
        var ticket = McpLoginContinuation.Issue(protection, Origin, Settings, request);
        Assert.True(McpLoginContinuation.TryRestore(protection, Origin, Settings, ticket, out var path));
        Assert.StartsWith("/connect/authorize?", path);
        var query = QueryHelpers.ParseQuery(new Uri(Origin.Value, path).Query);
        Assert.Equal(McpLoginContinuation.Parameters(request).OrderBy(x => x.Key),
            query.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value.ToString())).OrderBy(x => x.Key));
        Assert.DoesNotContain("library_id", query.Keys); Assert.DoesNotContain("action", query.Keys);
        Assert.DoesNotContain("actor", query.Keys); Assert.DoesNotContain("returnUrl", query.Keys);
    }

    [Fact]
    public void TwoFlowsRemainIndependentWithoutAGlobalReturnCookie()
    {
        var first = McpLoginContinuation.Issue(protection, Origin, Settings, Request("first"));
        var second = McpLoginContinuation.Issue(protection, Origin, Settings, Request("second"));
        Assert.NotEqual(first, second);
        Assert.True(McpLoginContinuation.TryRestore(protection, Origin, Settings, second, out var two));
        Assert.True(McpLoginContinuation.TryRestore(protection, Origin, Settings, first, out var one));
        Assert.Equal("first", QueryHelpers.ParseQuery(new Uri(Origin.Value, one).Query)["state"]);
        Assert.Equal("second", QueryHelpers.ParseQuery(new Uri(Origin.Value, two).Query)["state"]);
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("expired")]
    [InlineData("other-origin")]
    [InlineData("other-callback")]
    [InlineData("disabled")]
    public void AChangedExpiredOrReconfiguredTicketCannotContinue(string defect)
    {
        var ticket = McpLoginContinuation.Issue(protection, Origin, Settings, Request());
        var protector = protection.CreateProtector(McpLoginContinuation.Purpose, Origin.Value.AbsoluteUri).ToTimeLimitedDataProtector();
        if (defect == "tamper") ticket = ticket[..30] + (ticket[30] == 'A' ? 'B' : 'A') + ticket[31..];
        if (defect == "expired") ticket = protector.Protect(protector.Unprotect(ticket), DateTimeOffset.UtcNow.AddMinutes(-1));
        var origin = defect == "other-origin" ? new DelegationOrigin(new Uri("https://other.example")) : Origin;
        var settings = defect == "other-callback" ? new McpAccountSettings(new Uri("https://attacker.invalid/callback"))
            : defect == "disabled" ? new McpAccountSettings(null) : Settings;
        Assert.False(McpLoginContinuation.TryRestore(protection, origin, settings, ticket, out var path));
        Assert.Empty(path);
    }

    [Theory]
    [InlineData("client_id", "attacker")]
    [InlineData("redirect_uri", "https://attacker.invalid/")]
    [InlineData("resource", "https://quickprompt.example/api/companion")]
    [InlineData("scope", "prompts:read companion:work")]
    [InlineData("state", "")]
    [InlineData("code_challenge", "invalid")]
    [InlineData("code_challenge_method", "plain")]
    [InlineData("response_mode", "invalid")]
    [InlineData("library_id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("action", "approve")]
    [InlineData("returnUrl", "//attacker.invalid/")]
    public void EvenAProtectedEnvelopeCannotChangeFixedPolicyOrCarryApproval(string field, string value)
    {
        var data = McpLoginContinuation.Parameters(Request()); data[field] = value;
        var protector = protection.CreateProtector(McpLoginContinuation.Purpose, Origin.Value.AbsoluteUri).ToTimeLimitedDataProtector();
        // Test-owned keys create a structurally protected but invalid envelope.
        var ticket = protector.Protect(System.Text.Json.JsonSerializer.Serialize(data), TimeSpan.FromMinutes(1));
        Assert.False(McpLoginContinuation.TryRestore(protection, Origin, Settings, ticket, out var path));
        Assert.Empty(path);
    }

    [Fact]
    public void MissingContinuationPreservesOrdinaryGoogleAndDisabledDelegationFailsClosed()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        Assert.True(McpLoginContinuation.TryComplete(context, new AuthenticationProperties(), out var ordinary));
        Assert.Equal("/", ordinary);
        Assert.False(McpLoginContinuation.TryComplete(context, new AuthenticationProperties(new Dictionary<string, string?> {
            [McpLoginContinuation.Property] = "forged"
        }), out _));
    }

    [Fact]
    public void CompletionAcceptsOnlyItsVerifiedPropertiesAndRechecksExpiry()
    {
        using var services = new ServiceCollection().AddSingleton(protection).AddSingleton(Origin).AddSingleton(Settings).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var ticket = McpLoginContinuation.Issue(protection, Origin, Settings, Request());
        context.Request.QueryString = new QueryString("?mcp_login=" + Uri.EscapeDataString(ticket));
        Assert.True(McpLoginContinuation.TryComplete(context, new AuthenticationProperties(), out var ordinary));
        Assert.Equal("/", ordinary); // Request/query data cannot inject a return into a completed external login.
        var properties = new AuthenticationProperties(new Dictionary<string, string?> { [McpLoginContinuation.Property] = ticket });
        Assert.True(McpLoginContinuation.TryComplete(context, properties, out var authorized));
        Assert.StartsWith("/connect/authorize?", authorized);
        var protector = protection.CreateProtector(McpLoginContinuation.Purpose, Origin.Value.AbsoluteUri).ToTimeLimitedDataProtector();
        properties.Items[McpLoginContinuation.Property] = protector.Protect(protector.Unprotect(ticket), DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(McpLoginContinuation.TryComplete(context, properties, out var expired));
        Assert.Empty(expired);
    }
}
