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

public sealed class RenderEdgeIngressTests
{
    private const string Host = "quickprompt-synthetic.onrender.com";

    [Theory]
    [InlineData("RENDER", null)]
    [InlineData("RENDER", "false")]
    [InlineData("RENDER_SERVICE_ID", null)]
    [InlineData("RENDER_SERVICE_ID", "not-a-service")]
    [InlineData("RENDER_EXTERNAL_HOSTNAME", "attacker.example")]
    [InlineData("RENDER_EXTERNAL_HOSTNAME", "other.onrender.com")]
    [InlineData("RENDER_EXTERNAL_HOSTNAME", "quickprompt-synthetic.onrender.com.attacker.example")]
    [InlineData("QuickPrompt:Accounts:Origin", "https://other.onrender.com")]
    [InlineData("QuickPrompt:Hosting:Mode", "Local")]
    [InlineData("QuickPrompt:Hosting:ListenUrl", "http://127.0.0.1:10000")]
    [InlineData("QuickPrompt:Hosting:ListenUrl", "http://0.0.0.0:10001")]
    [InlineData("PORT", null)]
    [InlineData("PORT", "not-a-port")]
    [InlineData("QuickPrompt:Hosting:TrustedProxies:0", "10.32.0.7")]
    [InlineData("QuickPrompt:Hosting:TrustedNetworks:0", "10.32.0.0/16")]
    [InlineData("QuickPrompt:Hosting:OriginSecret", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("QuickPrompt:Hosting:IngressOrigin", "https://other.onrender.com")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void RequiresExplicitConsistentRenderBoundary(string key, string? value)
    {
        using var files = new CertificateFiles(); var settings = Settings(files); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    [Fact]
    public void ExplicitProfileNeverCreatesForwardedHeaderTrust()
    {
        using var files = new CertificateFiles(); var hosting = Read(Settings(files));
        Assert.Empty(hosting.Proxies); Assert.Empty(hosting.Networks); Assert.False(hosting.OriginProxy);
        Assert.Throws<InvalidOperationException>(() => hosting.Forwarding());
    }

    [Theory]
    [InlineData(Host, null, true)]
    [InlineData(Host, "https://" + Host, true)]
    [InlineData(Host, "https://attacker.example", false)]
    [InlineData(Host, "null", false)]
    [InlineData("other.onrender.com", null, false)]
    [InlineData(Host + ":443", null, false)]
    public async Task ExactHostAndOriginAreRequiredRegardlessOfForwardedHeaders(string host, string? origin, bool allowed)
    {
        using var files = new CertificateFiles(); var settings = Settings(files);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var hosting = Read(settings);
        await using var app = builder.Build(); var reached = false;
        hosting.ConfigureIngress(app);
        app.Run(context => {
            reached = true;
            Assert.Equal("https", context.Request.Scheme); Assert.Equal(Host, context.Request.Host.Value);
            Assert.True(hosting.Accepts(context.Request));
            Assert.False(context.Request.Headers.ContainsKey("Forwarded"));
            Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-Host"));
            Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-Proto"));
            Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-For"));
            Assert.False(context.Request.Headers.ContainsKey(AccountHosting.OriginSecretHeader));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://" + host + "/account/login");
        request.Headers.Host = host;
        request.Headers.Add("Forwarded", "for=203.0.113.1;host=attacker.example;proto=http");
        request.Headers.Add("X-Forwarded-Host", "attacker.example");
        request.Headers.Add("X-Forwarded-Proto", "http"); request.Headers.Add("X-Forwarded-For", "203.0.113.1");
        request.Headers.Add(AccountHosting.OriginSecretHeader, "untrusted");
        if (origin is not null) request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(allowed, reached);
    }

    private static AccountHosting Read(Dictionary<string, string?> settings) => AccountHosting.Read(
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new AccountHostingTests.EnvironmentStub("Production"));

    private static Dictionary<string, string?> Settings(CertificateFiles files)
    {
        var settings = files.Settings(); settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        settings["QuickPrompt:Hosting:IngressProfile"] = "RenderEdge";
        settings["QuickPrompt:Accounts:Origin"] = "https://" + Host;
        settings["QuickPrompt:Hosting:ListenUrl"] = "http://0.0.0.0:10000";
        settings["RENDER"] = "true"; settings["RENDER_SERVICE_ID"] = "srv-synthetic123";
        settings["RENDER_EXTERNAL_HOSTNAME"] = Host; settings["PORT"] = "10000";
        return settings;
    }
}
