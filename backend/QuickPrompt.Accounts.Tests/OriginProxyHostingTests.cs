using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using QuickPrompt.Cloud;
using QuickPrompt.Cloud.Authentication;
using Xunit;
using CertificateFiles = QuickPrompt.Accounts.Tests.AccountHostingTests.CertificateFiles;

namespace QuickPrompt.Accounts.Tests;

public sealed class OriginProxyHostingTests
{
    private const string PublicOrigin = "https://quickprompt-pilot.vercel.app";
    private const string BackendHost = "quickprompt-backend.onrender.com";
    private const string SyntheticSecret = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("short")]
    [InlineData("malformed")]
    [InlineData("multiple")]
    public async Task DirectOrForgedRequestsCannotReachThePublicPipeline(string credential)
    {
        using var files = new CertificateFiles(); var hosting = Read(Settings(files));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        await using var app = builder.Build(); var reached = false;
        hosting.ConfigureIngress(app);
        app.Run(_ => { reached = true; return Task.CompletedTask; });
        await app.StartAsync(); using var client = app.GetTestClient(); using var request = Request("/api/test", includeSecret: false);
        request.Headers.Add("X-Forwarded-For", "127.0.0.1"); request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", new Uri(PublicOrigin).Host);
        request.Headers.Add("Forwarded", "for=127.0.0.1;proto=https;host=" + new Uri(PublicOrigin).Host);
        if (credential != "missing") request.Headers.Add(AccountHosting.OriginSecretHeader, credential switch {
            "wrong" => new[] { new string('A', 64) }, "short" => new[] { "123" }, "malformed" => new[] { new string('Z', 64) },
            "multiple" => new[] { SyntheticSecret, SyntheticSecret }, _ => throw new InvalidOperationException()
        });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.False(reached);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData(BackendHost, PublicOrigin, "/api/test", true)]
    [InlineData(BackendHost, null, "/api/test", true)]
    [InlineData("attacker.example", PublicOrigin, "/api/test", false)]
    [InlineData("quickprompt-pilot.vercel.app", PublicOrigin, "/api/test", false)]
    [InlineData(BackendHost, "https://attacker.example", "/api/test", false)]
    [InlineData(BackendHost, "null", "/api/test", false)]
    [InlineData(BackendHost, "duplicate", "/api/test", false)]
    [InlineData(BackendHost, "https://accounts.google.com", "/signin-google", true)]
    public async Task HostAndOriginAreValidatedBeforeRebasingAndGoogleKeepsItsExistingException(string host, string? origin, string path, bool permitted)
    {
        using var files = new CertificateFiles(); var hosting = Read(Settings(files));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.12"); return next(context); });
        hosting.ConfigureIngress(app);
        app.Run(async context => {
            Assert.True(hosting.Accepts(context.Request));
            Assert.False(context.Request.Headers.ContainsKey(AccountHosting.OriginSecretHeader));
            await context.Response.WriteAsync(context.Request.Scheme + "://" + context.Request.Host + "|" + context.Connection.RemoteIpAddress);
        });
        await app.StartAsync(); using var client = app.GetTestClient(); using var request = Request(path);
        request.Headers.Host = host; request.Headers.Remove("Origin");
        if (origin == "duplicate") request.Headers.Add("Origin", new[] { PublicOrigin, PublicOrigin });
        else if (origin is not null) request.Headers.Add("Origin", origin);
        request.Headers.Add("X-Forwarded-For", "127.0.0.1"); request.Headers.Add("X-Forwarded-Proto", "http");
        request.Headers.Add("X-Forwarded-Host", BackendHost); request.Headers.Add("Forwarded", "for=127.0.0.1;proto=http;host=attacker.example");
        using var response = await client.SendAsync(request);
        Assert.Equal(permitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (permitted) Assert.Equal(PublicOrigin + "|203.0.113.12", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("GET", "/health", BackendHost, null, true)]
    [InlineData("HEAD", "/health", BackendHost, PublicOrigin, true)]
    [InlineData("POST", "/health", BackendHost, null, false)]
    [InlineData("OPTIONS", "/health", BackendHost, null, false)]
    [InlineData("GET", "/health/", BackendHost, null, false)]
    [InlineData("GET", "/health?probe=other", BackendHost, null, false)]
    [InlineData("GET", "/api/test", BackendHost, null, false)]
    [InlineData("GET", "/health", "attacker.example", null, false)]
    [InlineData("GET", "/health", BackendHost, "https://attacker.example", false)]
    public async Task PhysicalHealthIsTheOnlyUnauthenticatedExceptionAndNeverRebasesOrReachesAuthentication(string method, string path, string host, string? origin, bool allowed)
    {
        using var files = new CertificateFiles(); var hosting = Read(Settings(files));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        await using var app = builder.Build(); var authenticationReached = false; var healthReached = false;
        hosting.ConfigureIngress(app, context => {
            healthReached = true;
            Assert.False(context.User.Identity?.IsAuthenticated ?? false);
            Assert.Equal(BackendHost, context.Request.Host.Value); Assert.Equal("http", context.Request.Scheme);
            context.Response.StatusCode = 204; return Task.CompletedTask;
        });
        app.Use((context, next) => { authenticationReached = true; return next(context); });
        app.Run(_ => Task.CompletedTask);
        await app.StartAsync(); using var client = app.GetTestClient(); using var request = Request(path, includeSecret: false);
        request.Method = new(method); request.Headers.Host = host; request.Headers.Remove("Origin");
        if (origin is not null) request.Headers.Add("Origin", origin);
        request.Headers.Add("Cookie", "__Host-qp.accounts.synthetic=untrusted");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(allowed, healthReached); Assert.False(authenticationReached); Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", null)]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "http://backend.example")]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "https://127.0.0.1")]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "https://user:password@backend.example")]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "https://backend.example/path")]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "https://backend.example?query=1")]
    [InlineData("QuickPrompt:Hosting:OriginSecret", null)]
    [InlineData("QuickPrompt:Hosting:OriginSecret", "short")]
    [InlineData("QuickPrompt:Hosting:TrustedProxies:0", "10.32.0.7")]
    [InlineData("QuickPrompt:Hosting:TrustedNetworks:0", "10.32.0.0/16")]
    [InlineData("QuickPrompt:Hosting:IngressProfile", "TrustedProxy")]
    [InlineData("QuickPrompt:Hosting:Mode", "Local")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void ProxyConfigurationRejectsMissingOrAmbiguousAuthority(string key, string? value)
    {
        using var files = new CertificateFiles(); var settings = Settings(files); settings[key] = value;
        var error = Assert.Throws<InvalidOperationException>(() => Read(settings));
        Assert.DoesNotContain(SyntheticSecret, error.Message);
    }

    [Fact]
    public void ProxyCredentialIsAbsentFromRecordDiagnosticsAndJson()
    {
        using var files = new CertificateFiles(); var hosting = Read(Settings(files));
        Assert.True(hosting.OriginProxy); Assert.Throws<InvalidOperationException>(() => hosting.Forwarding());
        Assert.DoesNotContain(SyntheticSecret, hosting.ToString());
        Assert.DoesNotContain(SyntheticSecret, System.Text.Json.JsonSerializer.Serialize(hosting));
    }

    [Fact]
    public async Task RebasingPreservesFirstPartyCookiesCsrfAndTheActualGoogleChallengeCallback()
    {
        using var files = new CertificateFiles(); var settings = Settings(files);
        settings["QuickPrompt:Accounts:Google:ClientId"] = "synthetic-client";
        settings["QuickPrompt:Accounts:Google:ClientSecret"] = "synthetic-google-secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build(); var hosting = Read(settings);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options => {
            options.HeaderName = "X-QP-CSRF"; options.Cookie.Name = "__Host-proxy-test-csrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always; options.Cookie.SameSite = SameSiteMode.Strict;
        });
        var authentication = builder.Services.AddAuthentication("proxy-test-cookie").AddCookie("proxy-test-cookie", options => {
            options.Cookie.Name = "__Host-proxy-test-session"; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        }).AddCookie(IdentityConstants.ExternalScheme);
        authentication.AddGoogleAccounts(configuration, builder.Services, hosting.Origin);
        builder.Services.Configure<OpenIdConnectOptions>(GoogleAccounts.Scheme, options => options.Configuration = new OpenIdConnectConfiguration {
            Issuer = "https://accounts.google.com", AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth", TokenEndpoint = "https://oauth2.googleapis.com/token"
        });
        await using var app = builder.Build(); hosting.ConfigureIngress(app); app.UseAuthentication();
        app.MapGet("/signin", async context => {
            await context.SignInAsync("proxy-test-cookie", new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"synthetic-user")], "synthetic")));
            context.Response.StatusCode = 204;
        });
        app.MapGet("/csrf", (HttpContext context, IAntiforgery csrf) => Results.Ok(new { token=csrf.GetAndStoreTokens(context).RequestToken }));
        app.MapGet("/who", (HttpContext context) => Results.Ok(context.User.FindFirstValue(ClaimTypes.NameIdentifier)));
        app.MapPost("/mutation", async (HttpContext context, IAntiforgery csrf) => {
            try { await csrf.ValidateRequestAsync(context); return Results.NoContent(); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
        });
        app.MapGet("/google", context => context.ChallengeAsync(GoogleAccounts.Scheme));
        await app.StartAsync(); using var client = app.GetTestClient();
        using var signIn = Request("/signin"); using var signed = await client.SendAsync(signIn);
        var sessionCookie = Assert.Single(signed.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-proxy-test-session=", sessionCookie); Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        using var who = Request("/who"); who.Headers.Add("Cookie", sessionCookie.Split(';')[0]);
        using var authenticated = await client.SendAsync(who);
        Assert.Contains("synthetic-user", await authenticated.Content.ReadAsStringAsync());
        using var csrfRequest = Request("/csrf"); using var csrfResponse = await client.SendAsync(csrfRequest);
        var csrfCookie = Assert.Single(csrfResponse.Headers.GetValues("Set-Cookie"));
        using var json = System.Text.Json.JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync());
        using var mutation = Request("/mutation"); mutation.Method = HttpMethod.Post;
        mutation.Headers.Add("Cookie", csrfCookie.Split(';')[0]); mutation.Headers.Add("X-QP-CSRF", json.RootElement.GetProperty("token").GetString());
        using var mutated = await client.SendAsync(mutation); Assert.Equal(HttpStatusCode.NoContent, mutated.StatusCode);
        using var missingCsrf = Request("/mutation"); missingCsrf.Method = HttpMethod.Post;
        using var refused = await client.SendAsync(missingCsrf); Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using var googleRequest = Request("/google"); using var challenge = await client.SendAsync(googleRequest);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Equal(PublicOrigin + "/signin-google", QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["redirect_uri"]);
        Assert.All(challenge.Headers.GetValues("Set-Cookie"), cookie => {
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        });
        using var invalidCallback = Request("/signin-google?code=synthetic&state=invalid");
        invalidCallback.Headers.Remove("Origin"); invalidCallback.Headers.Add("Origin", "https://accounts.google.com");
        using var callbackRejected = await client.SendAsync(invalidCallback);
        Assert.Equal(HttpStatusCode.Unauthorized, callbackRejected.StatusCode);
    }

    private static Dictionary<string, string?> Settings(CertificateFiles files)
    {
        var settings = files.Settings(); settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        settings["QuickPrompt:Accounts:Origin"] = PublicOrigin; settings["QuickPrompt:Hosting:IngressProfile"] = "OriginProxy";
        settings["QuickPrompt:Hosting:IngressOrigin"] = "https://" + BackendHost; settings["QuickPrompt:Hosting:OriginSecret"] = SyntheticSecret;
        return settings;
    }
    private static AccountHosting Read(Dictionary<string, string?> settings) => AccountHosting.Read(
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new AccountHostingTests.EnvironmentStub("Production"));
    private static HttpRequestMessage Request(string path, bool includeSecret = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Host = BackendHost;
        request.Headers.Add("Origin", PublicOrigin);
        if (includeSecret) request.Headers.Add(AccountHosting.OriginSecretHeader, SyntheticSecret);
        return request;
    }
}
