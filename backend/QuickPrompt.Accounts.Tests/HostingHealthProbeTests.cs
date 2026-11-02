using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using QuickPrompt.Cloud;
using Xunit;
using CertificateFiles = QuickPrompt.Accounts.Tests.AccountHostingTests.CertificateFiles;

namespace QuickPrompt.Accounts.Tests;

public sealed class HostingHealthProbeTests
{
    private const string ProbeHost = "healthcheck.railway.app";

    [Theory]
    [InlineData("GET", "/health", ProbeHost, true, null, true)]
    [InlineData("HEAD", "/health", ProbeHost, true, null, true)]
    [InlineData("GET", "/health", ProbeHost, false, null, false)]
    [InlineData("POST", "/health", ProbeHost, true, null, false)]
    [InlineData("OPTIONS", "/health", ProbeHost, true, null, false)]
    [InlineData("GET", "/health/", ProbeHost, true, null, false)]
    [InlineData("GET", "/health?extra=1", ProbeHost, true, null, false)]
    [InlineData("GET", "/api/test", ProbeHost, true, null, false)]
    [InlineData("GET", "/health", "attacker.example", true, null, false)]
    [InlineData("GET", "/health", "healthcheck.railway.app:443", true, null, false)]
    [InlineData("GET", "/health", ProbeHost, true, "https://attacker.example", false)]
    public async Task ExplicitProbeHostOnlyReachesTerminalHealth(string method, string path, string host,
        bool configured, string? origin, bool allowed)
    {
        using var files = new CertificateFiles();
        var settings = Settings(files);
        if (configured) settings["QuickPrompt:Hosting:HealthCheckHost"] = ProbeHost;
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var hosting = AccountHosting.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), builder.Environment);
        await using var app = builder.Build(); var applicationReached = false; var probeReached = false;
        hosting.ConfigureIngress(app, context => {
            probeReached = true;
            Assert.False(context.User.Identity?.IsAuthenticated ?? false);
            Assert.Equal(host, context.Request.Host.Value);
            Assert.Equal("http", context.Request.Scheme);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        app.Run(_ => { applicationReached = true; return Task.CompletedTask; });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new(method), "http://" + host + path);
        request.Headers.Add("X-Forwarded-Host", "quickprompt.example");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("Cookie", "__Host-synthetic=untrusted");
        if (origin is not null) request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(allowed, probeReached); Assert.False(applicationReached);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("*.railway.app")]
    [InlineData("https://healthcheck.railway.app")]
    [InlineData("healthcheck.railway.app:443")]
    [InlineData("healthcheck.railway.app/path")]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public void ProbeHostRequiresOneExplicitDnsName(string value)
    {
        using var files = new CertificateFiles(); var settings = Settings(files);
        settings["QuickPrompt:Hosting:HealthCheckHost"] = value;
        var builder = WebApplication.CreateBuilder();
        Assert.Throws<InvalidOperationException>(() => AccountHosting.Read(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), builder.Environment));
    }

    private static Dictionary<string, string?> Settings(CertificateFiles files)
    {
        var settings = files.Settings(); settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        settings["QuickPrompt:Hosting:IngressProfile"] = "OriginProxy";
        settings["QuickPrompt:Hosting:IngressOrigin"] = "https://physical.example";
        settings["QuickPrompt:Hosting:OriginSecret"] = new string('A', 64);
        return settings;
    }
}
