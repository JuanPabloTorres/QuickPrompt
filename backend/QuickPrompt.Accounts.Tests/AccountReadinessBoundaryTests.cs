using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Controlled cookie-store failure proves pipeline framing, not the PostgreSQL cause of an earlier CI failure.</summary>
public sealed class AccountReadinessBoundaryTests
{
    private const string Origin = "https://127.0.0.1:5299";
    private const string Scheme = "readiness-cookie";

    [Fact]
    public async Task OriginalFramingMakesAuthenticatedHealthFailBeforeTheReadinessProbe()
    {
        await using var fixture = await Start(useReadinessBoundary: false);
        using var authenticated = await fixture.Client(true); using var anonymous = await fixture.Client(false);
        Assert.Equal(HttpStatusCode.InternalServerError, (await authenticated.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await anonymous.GetAsync("/health")).StatusCode);
        Assert.Equal(1, fixture.Validations()); Assert.Equal(1, fixture.Probes());
    }

    [Fact]
    public async Task PublicReadinessGetAndHeadReachTheProbeRegardlessOfAnUnrelatedCookie()
    {
        await using var fixture = await Start(useReadinessBoundary: true);
        using var authenticated = await fixture.Client(true); using var anonymous = await fixture.Client(false);
        foreach (var client in new[] { authenticated, anonymous })
            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Head })
                using (var request = new HttpRequestMessage(method, "/health"))
                    Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(0, fixture.Validations()); Assert.Equal(4, fixture.Probes());
    }

    [Fact]
    public async Task PrivateRoutesStillAuthenticateAndDoNotUseThePublicProbe()
    {
        await using var fixture = await Start(useReadinessBoundary: true);
        using var authenticated = await fixture.Client(true); using var anonymous = await fixture.Client(false);
        Assert.Equal(HttpStatusCode.InternalServerError, (await authenticated.GetAsync("/private")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/private")).StatusCode);
        Assert.Equal(1, fixture.Validations()); Assert.Equal(0, fixture.Probes());
    }

    [Fact]
    public async Task ExactOriginGuardRejectsForgedHealthRequestsBeforeProbeAndAuthentication()
    {
        await using var fixture = await Start(useReadinessBoundary: true);
        using var authenticated = await fixture.Client(true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://synthetic-untrusted.example.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await authenticated.SendAsync(request)).StatusCode);
        Assert.Equal(0, fixture.Validations()); Assert.Equal(0, fixture.Probes());
    }

    private static async Task<ReadinessFixture> Start(bool useReadinessBoundary)
    {
        var builder = WebApplication.CreateBuilder(); builder.Environment.EnvironmentName = "Development";
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["QuickPrompt:Accounts:Origin"] = Origin });
        var hosting = AccountHosting.Read(builder.Configuration, builder.Environment);
        var validations = 0; var probes = 0;
        builder.Services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Events.OnValidatePrincipal = _ => { Interlocked.Increment(ref validations); throw new TimeoutException("Controlled unavailable cookie store"); };
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(); builder.Services.AddAccountRateLimiting();
        var app = builder.Build(); app.UseRouting();
        app.Use(async (context, next) =>
        {
            if (!hosting.Accepts(context.Request)) { context.Response.StatusCode = 403; return; }
            try { await next(); } catch (TimeoutException) { context.Response.StatusCode = 500; }
        });
        app.UseAccountIngressLimits();
        Task Probe(HttpContext context) { Interlocked.Increment(ref probes); context.Response.StatusCode = 503; return Task.CompletedTask; }
        if (useReadinessBoundary) app.UsePublicAccountReadiness(Probe);
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.MapGet("/synthetic-signin", () => Results.SignIn(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], Scheme)), authenticationScheme: Scheme));
        app.MapMethods("/health", ["GET", "HEAD"], Probe);
        app.MapGet("/private", () => Results.Ok()).RequireAuthorization();
        await app.StartAsync();
        return new(app, () => Volatile.Read(ref validations), () => Volatile.Read(ref probes));
    }

    private sealed record ReadinessFixture(WebApplication App, Func<int> Validations, Func<int> Probes) : IAsyncDisposable
    {
        public async Task<HttpClient> Client(bool authenticated)
        {
            var client = App.GetTestClient(); client.BaseAddress = new Uri(Origin);
            if (authenticated)
            {
                using var response = await client.GetAsync("/synthetic-signin"); response.EnsureSuccessStatusCode();
                client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
            }
            return client;
        }
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }
}
