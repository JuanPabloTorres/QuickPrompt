using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Cloud.Authentication;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    // Uses the existing isolated PostgreSQL fixture, real OpenIddict/cookies/CSRF
    // and a signed synthetic Google provider. No request reaches an external IdP.
    private Task<RunningHost> StartMcpLogin(FakeMcpGoogle google) => Start(mcp: true, configureServices: services =>
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Accounts:Google:ClientId"] = FakeMcpGoogle.Client,
            ["QuickPrompt:Accounts:Google:ClientSecret"] = "synthetic-test-only-secret"
        }).Build();
        services.AddAuthentication().AddGoogleAccounts(configuration, services, new Uri("https://127.0.0.1:5299"));
        services.Configure<OpenIdConnectOptions>(GoogleAccounts.Scheme, options =>
        {
            options.Configuration = new OpenIdConnectConfiguration {
                Issuer = FakeMcpGoogle.Issuer, AuthorizationEndpoint = FakeMcpGoogle.Issuer + "/authorize",
                TokenEndpoint = FakeMcpGoogle.Issuer + "/token"
            };
            options.Configuration.SigningKeys.Add(google.SigningKey);
            options.BackchannelHttpHandler = google;
        });
    });

    private static Dictionary<string, string> McpHiddenFields(string html) => Regex.Matches(html,
        "name=\"([^\"]+)\" value=\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
        .ToDictionary(match => WebUtility.HtmlDecode(match.Groups[1].Value), match => WebUtility.HtmlDecode(match.Groups[2].Value));

    private static async Task<Dictionary<string, string>> McpLoginForm(HttpClient client, Dictionary<string, string> request)
    {
        var response = await client.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", request!));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Iniciar sesión", html); Assert.DoesNotContain("name=library_id", html);
        var form = McpHiddenFields(html);
        Assert.Contains(McpLoginContinuation.FormField, form.Keys); Assert.DoesNotContain("action", form.Keys);
        return form;
    }

    private static async Task<Uri> McpGoogleChallenge(HttpClient client, Dictionary<string, string> form)
    {
        var response = await client.PostAsync("/account/google", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(FakeMcpGoogle.Issuer, location.GetLeftPart(UriPartial.Authority));
        Assert.Equal("S256", QueryHelpers.ParseQuery(location.Query)["code_challenge_method"]);
        return location;
    }

    private static async Task<string> CompleteMcpGoogle(HttpClient client, FakeMcpGoogle google, Uri challenge, string code)
    {
        var query = QueryHelpers.ParseQuery(challenge.Query);
        google.Nonces.Add(code, query["nonce"].ToString());
        var callback = await client.GetAsync(QueryHelpers.AddQueryString("/signin-google", new Dictionary<string, string?> {
            ["code"] = code, ["state"] = query["state"].ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/account/google/complete", callback.Headers.Location!.OriginalString);
        var completed = await client.GetAsync(callback.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        return completed.Headers.Location!.OriginalString;
    }

    [Theory]
    [InlineData("query")]
    [InlineData("form_post")]
    [InlineData("fragment")]
    public async Task McpAnonymousGoogleReturnsToExactConsentAndCreatesNoGrantUntilApproval(string responseMode)
    {
        using var google = new FakeMcpGoogle(); await using var host = await StartMcpLogin(google);
        using var browser = Client(host.App); using var protocol = Client(host.App);
        const string verifier = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";
        var request = McpRequest(protocol, verifier, allowCreate: true); request["response_mode"] = responseMode;
        var form = await McpLoginForm(browser, request);
        var challenge = await McpGoogleChallenge(browser, form);
        var returnPath = await CompleteMcpGoogle(browser, google, challenge, "flow-complete");
        Assert.StartsWith("/connect/authorize?", returnPath);
        var resumed = QueryHelpers.ParseQuery(new Uri(browser.BaseAddress!, returnPath).Query);
        foreach (var pair in request) Assert.Equal(pair.Value, resumed[pair.Key]);
        Assert.Empty((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        await Csrf(browser); var library = await CreateLibrary(browser);
        var consent = await browser.GetAsync(returnPath); Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        var approval = McpHiddenFields(await consent.Content.ReadAsStringAsync());
        Assert.Equal(responseMode, approval["response_mode"]);
        Assert.Empty((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        approval["action"] = "approve"; approval["library_id"] = library.ToString();
        var approved = await browser.PostAsync("/connect/authorize", new FormUrlEncodedContent(approval));
        Dictionary<string, string> result;
        if (responseMode == "form_post")
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            var html = await approved.Content.ReadAsStringAsync(); Assert.Contains("https://client.example/callback", html);
            result = McpHiddenFields(html);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
            var location = approved.Headers.Location!; Assert.Equal("https://client.example/callback", location.GetLeftPart(UriPartial.Path));
            result = QueryHelpers.ParseQuery(responseMode == "fragment" ? "?" + location.Fragment[1..] : location.Query)
                .ToDictionary(x => x.Key, x => x.Value.ToString());
        }
        Assert.Equal(request["state"], result["state"]);
        Assert.Equal(browser.BaseAddress!.GetLeftPart(UriPartial.Authority) + "/", result["iss"]);
        Assert.True(result.TryGetValue("code", out var authorizationCode), "Consent must issue a code only after explicit approval.");
        await McpRedeem(protocol, authorizationCode!, verifier);
        var connection = Assert.Single((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        Assert.Equal(library, connection.LibraryId); Assert.Equal(new[] { "prompts:read", "prompts:create" }, connection.Scopes);
        Assert.Equal(1, google.TokenCalls);
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("expired")]
    [InlineData("missing-csrf")]
    [InlineData("duplicate-ticket")]
    public async Task McpGoogleLoginRejectsInvalidContinuationBeforeTheExternalChallenge(string defect)
    {
        using var google = new FakeMcpGoogle(); await using var host = await StartMcpLogin(google); using var browser = Client(host.App);
        var form = await McpLoginForm(browser, McpRequest(browser)); var ticket = form[McpLoginContinuation.FormField];
        if (defect == "tamper") form[McpLoginContinuation.FormField] = ticket[..30] + (ticket[30] == 'A' ? 'B' : 'A') + ticket[31..];
        if (defect == "expired")
        {
            var provider = host.App.Services.GetRequiredService<IDataProtectionProvider>();
            var origin = host.App.Services.GetRequiredService<DelegationOrigin>();
            var protector = provider.CreateProtector(McpLoginContinuation.Purpose, origin.Value.AbsoluteUri).ToTimeLimitedDataProtector();
            form[McpLoginContinuation.FormField] = protector.Protect(protector.Unprotect(ticket), DateTimeOffset.UtcNow.AddMinutes(-1));
        }
        if (defect == "missing-csrf") form.Remove("__RequestVerificationToken");
        var fields = form.ToList(); if (defect == "duplicate-ticket") fields.Add(new(McpLoginContinuation.FormField, ticket));
        var response = await browser.PostAsync("/account/google", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Null(response.Headers.Location); Assert.Equal(0, google.TokenCalls);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/account/me")).StatusCode);
    }

    [Fact]
    public async Task McpTwoGoogleFlowsResumeTheirOwnRequestsAndDenialCreatesNoGrant()
    {
        using var google = new FakeMcpGoogle(); await using var host = await StartMcpLogin(google); using var browser = Client(host.App);
        var first = McpRequest(browser); var second = McpRequest(browser, allowCreate: true);
        var firstChallenge = await McpGoogleChallenge(browser, await McpLoginForm(browser, first));
        var secondChallenge = await McpGoogleChallenge(browser, await McpLoginForm(browser, second));
        var secondPath = await CompleteMcpGoogle(browser, google, secondChallenge, "second-flow");
        var firstPath = await CompleteMcpGoogle(browser, google, firstChallenge, "first-flow");
        Assert.Equal(first["state"], QueryHelpers.ParseQuery(new Uri(browser.BaseAddress!, firstPath).Query)["state"]);
        Assert.Equal(second["state"], QueryHelpers.ParseQuery(new Uri(browser.BaseAddress!, secondPath).Query)["state"]);
        Assert.Empty((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        var review = await browser.GetAsync(firstPath); var denial = McpHiddenFields(await review.Content.ReadAsStringAsync()); denial["action"] = "deny";
        var denied = await browser.PostAsync("/connect/authorize", new FormUrlEncodedContent(denial));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        var query = QueryHelpers.ParseQuery(denied.Headers.Location!.Query);
        Assert.Equal("access_denied", query["error"]); Assert.Equal(first["state"], query["state"]);
        Assert.Empty((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }

    [Fact]
    public async Task OrdinaryGoogleLoginIgnoresGeneralReturnUrlsAndKeepsItsHomeDestination()
    {
        using var google = new FakeMcpGoogle(); await using var host = await StartMcpLogin(google); using var browser = Client(host.App);
        await Csrf(browser);
        var challenge = await McpGoogleChallenge(browser, new() { ["returnUrl"] = "https://attacker.invalid/" });
        Assert.Equal("/", await CompleteMcpGoogle(browser, google, challenge, "ordinary-flow"));
        Assert.Empty((await browser.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GoogleDenialOrForeignStateCannotResumeOrCreateAnAuthenticatedSession(bool foreignState)
    {
        using var google = new FakeMcpGoogle(); await using var host = await StartMcpLogin(google); using var browser = Client(host.App);
        var challenge = await McpGoogleChallenge(browser, await McpLoginForm(browser, McpRequest(browser)));
        var state = QueryHelpers.ParseQuery(challenge.Query)["state"].ToString();
        var rejected = await browser.GetAsync(QueryHelpers.AddQueryString("/signin-google", new Dictionary<string, string?> {
            ["state"] = foreignState ? "foreign-synthetic-state" : state, ["error"] = "access_denied"
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode); Assert.Null(rejected.Headers.Location);
        Assert.Equal(0, google.TokenCalls); Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/account/me")).StatusCode);
    }

    private sealed class FakeMcpGoogle : HttpMessageHandler
    {
        internal const string Client = "synthetic-mcp-login-client";
        internal const string Issuer = "https://accounts.google.com";
        private readonly RSA rsa = RSA.Create(2048);
        internal RsaSecurityKey SigningKey { get; }
        internal Dictionary<string, string> Nonces { get; } = [];
        internal int TokenCalls { get; private set; }
        private readonly string subject = Guid.NewGuid().ToString("N");
        internal FakeMcpGoogle() => SigningKey = new(rsa) { KeyId = "synthetic-signing-key" };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal(Issuer + "/token", request.RequestUri!.AbsoluteUri); Assert.Equal(HttpMethod.Post, request.Method);
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("authorization_code", form["grant_type"]); Assert.NotEmpty(form["code_verifier"].ToString());
            var code = form["code"].ToString(); Assert.True(Nonces.TryGetValue(code, out var nonce)); TokenCalls++;
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(Issuer, Client, [new Claim("sub", subject), new Claim("iat", EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64), new Claim("email", subject + "@example.invalid"),
                new Claim("email_verified", "true", ClaimValueTypes.Boolean), new Claim("nonce", nonce!)], now.AddSeconds(-10), now.AddMinutes(5),
                new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                access_token = "synthetic-google-access", token_type = "Bearer", expires_in = 300,
                id_token = new JwtSecurityTokenHandler().WriteToken(token)
            }) };
        }

        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
}
