using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real ASP.NET authentication and production budgets, without PostgreSQL or production traffic.</summary>
public sealed class AccountRateLimitingTests
{
    private const string CookieScheme = "RateTestCookie";
    private const string BearerScheme = "RateTestBearer";

    [Fact]
    public async Task SignedCookieOwnersBehindOneIngressHaveIndependentBudgets()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        var ownerA = Guid.NewGuid(); var ownerB = Guid.NewGuid();
        var cookieA = await Cookie(client, ownerA); var cookieB = await Cookie(client, ownerB);
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/cookie", cookieA));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/cookie", cookieA));
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/cookie", cookieB));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/cookie", cookieB));
        Assert.Equal(HttpStatusCode.Unauthorized, await Request(client, "/private/cookie"));
    }

    [Fact]
    public async Task BearerPolicyUsesItsValidatedOwnerAndNeverAnUnrelatedValidCookie()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        var ownerA = Guid.NewGuid(); var ownerB = Guid.NewGuid();
        var bearerA = await Token(client, ownerA); var bearerB = await Token(client, ownerB);
        var cookieB = await Cookie(client, ownerB);
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/bearer", cookieB, bearerA));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/bearer", cookieB, bearerA));
        Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/bearer", cookieB, bearerB));
        Assert.Equal(HttpStatusCode.Unauthorized, await Request(client, "/private/bearer", cookieB, "invalid-reference-token"));
        Assert.Equal(HttpStatusCode.Unauthorized, await Request(client, "/private/bearer", cookieB));
    }

    [Fact]
    public async Task CredentialEntryKeepsItsPhysicalIpBudgetEvenWithCookiesAndForgedHeaders()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        var cookie = await Cookie(client, Guid.NewGuid());
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.Accepted, await Request(client, "/credentials", cookie));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/credentials", cookie));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/credentials"));
    }

    [Fact]
    public async Task OneOwnerCannotMultiplyTheBudgetByChangingFromCookieToBearer()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        var owner = Guid.NewGuid();
        var cookie = await Cookie(client, owner); var bearer = await Token(client, owner);
        for (var i = 0; i < 30; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/cookie", cookie));
            Assert.Equal(HttpStatusCode.OK, await Request(client, "/private/bearer", bearer: bearer));
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/cookie", cookie));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/bearer", bearer: bearer));
    }

    [Fact]
    public async Task InvalidBearerCannotBorrowCookieIdentityAndAuthenticationWorkStillHasAnIngressCeiling()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        var cookie = await Cookie(client, Guid.NewGuid());
        for (var i = 0; i < 600; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await Request(client, "/private/bearer", cookie, "invalid-" + i));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/private/bearer", cookie, "invalid-next"));
    }

    [Fact]
    public async Task ProtocolBudgetPrecedesAuthenticationRequestHandlersAndCannotBeResetByForwardedHeaders()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.Accepted, await Request(client, "/connect/device"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client, "/connect/device"));
        Assert.Equal(HttpStatusCode.Accepted, await Request(client, "/credentials"));
    }

    private static async Task<WebApplication> Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddAuthentication(CookieScheme)
            .AddCookie(CookieScheme, options =>
            {
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            }).AddBearerToken(BearerScheme);
        builder.Services.AddAuthorization();
        builder.Services.AddAccountRateLimiting();
        var app = builder.Build();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            // All clients share a physical ingress. Client forwarding/actor headers are untrusted.
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            await next();
        });
        app.UseAccountIngressLimits();
        // Model the short-circuit shape of an OAuth authentication request handler.
        // This cannot prove OpenIddict itself; its real-PG boundary suite remains required.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/connect/device") { context.Response.StatusCode = 202; return; }
            await next();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/test/cookie/{owner:guid}", (Guid owner) => Results.SignIn(Principal(owner), authenticationScheme: CookieScheme));
        app.MapGet("/test/token/{owner:guid}", (Guid owner) => Results.SignIn(Principal(owner), authenticationScheme: BearerScheme));
        app.MapGet("/private/cookie", () => Results.Ok()).RequireAuthorization().RequireRateLimiting("credentials");
        app.MapGet("/private/bearer", () => Results.Ok())
            .RequireAuthorization(new AuthorizationPolicyBuilder(BearerScheme).RequireAuthenticatedUser().Build())
            .RequireRateLimiting("credentials");
        app.MapGet("/credentials", () => Results.Accepted()).RequireRateLimiting("credentials");
        await app.StartAsync();
        return app;
    }

    private static ClaimsPrincipal Principal(Guid owner) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "signed-test-identity"));

    private static async Task<string> Cookie(HttpClient client, Guid owner)
    {
        using var response = await client.GetAsync($"/test/cookie/{owner}");
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    private static async Task<string> Token(HttpClient client, Guid owner)
    {
        using var response = await client.GetAsync($"/test/token/{owner}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpStatusCode> Request(HttpClient client, string path, string? cookie = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (bearer is not null) request.Headers.Add("Authorization", "Bearer " + bearer);
        request.Headers.Add("X-Forwarded-For", $"192.0.2.{Random.Shared.Next(1, 255)}");
        request.Headers.Add("X-QP-Actor", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
