using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuickPrompt.Cloud;
using Npgsql;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountHostingTests
{
    [Theory]
    [InlineData("https://127.0.0.1:5299", "Production")]
    [InlineData("http://127.0.0.1:5299", "Development")]
    public void DefaultHostingRemainsLoopbackWithoutProxyOrKeyRingOverrides(string origin, string environment)
    {
        var hosting = Read(new() { ["QuickPrompt:Accounts:Origin"] = origin }, environment);
        Assert.False(hosting.Public); Assert.Equal(hosting.Origin, hosting.Listen);
        Assert.Empty(hosting.Proxies); Assert.Empty(hosting.Networks); Assert.Null(hosting.CertificatePath);
    }

    [Theory]
    [InlineData("https://public.example", "Production")]
    [InlineData("http://127.0.0.1:5299", "Production")]
    [InlineData("https://user:secret@127.0.0.1:5299", "Development")]
    [InlineData("https://127.0.0.1:5299/path", "Development")]
    public void DefaultHostingCannotExposeAHostOrAcceptAmbiguousOrigins(string origin, string environment) =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["QuickPrompt:Accounts:Origin"] = origin }, environment));

    [Theory]
    [InlineData("QuickPrompt:Hosting:TrustedProxies:0", "0.0.0.0")]
    [InlineData("QuickPrompt:Hosting:TrustedProxies:0", "*")]
    [InlineData("QuickPrompt:Hosting:TrustedProxies:0", "https://proxy.example")]
    [InlineData("QuickPrompt:Hosting:TrustedNetworks:0", "0.0.0.0/0")]
    [InlineData("QuickPrompt:Hosting:TrustedNetworks:0", "::/0")]
    [InlineData("QuickPrompt:Hosting:ListenUrl", "http://listener.example:8080")]
    [InlineData("QuickPrompt:Hosting:ListenUrl", "https://0.0.0.0:8080")]
    [InlineData("QuickPrompt:Accounts:Origin", "http://public.example")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("FORWARDEDHEADERS_ENABLED", "true")]
    public void PublicHostingRejectsUnknownOrCatchAllTrust(string key, string value)
    {
        using var files = new CertificateFiles();
        var settings = files.Settings();
        if (key.Contains("TrustedNetworks")) settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    [Fact]
    public void PublicHostingRequiresExplicitProxyAndSecretAndCannotUseDevelopmentMailMode()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        Assert.Throws<InvalidOperationException>(() => Read(settings, "Development"));
        settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        Assert.Throws<InvalidOperationException>(() => Read(settings));
        settings["QuickPrompt:Hosting:TrustedNetworks:0"] = "10.32.0.0/16";
        Assert.Single(Read(settings).Networks);
        settings.Remove("QuickPrompt:Hosting:KeyRingCertificatePath");
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    [Fact]
    public void PublicSettingsDoNotExposeTheCertificatePasswordInDiagnostics()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        settings["QuickPrompt:Hosting:PreviousKeyRingCertificates:0:Path"] = files.AddCertificate("previous");
        settings["QuickPrompt:Hosting:PreviousKeyRingCertificates:0:Password"] = "Private!PreviousSentinel";
        var hosting = Read(settings);
        Assert.DoesNotContain("Synthetic!KeyRing79", hosting.ToString());
        Assert.DoesNotContain("Private!PreviousSentinel", hosting.ToString());
        // IPAddress itself has no default JSON converter; retain private state in the record copy while testing its public projection.
        var json = System.Text.Json.JsonSerializer.Serialize(hosting with { Proxies = [], Networks = [] });
        Assert.DoesNotContain("Synthetic!KeyRing79", json);
        Assert.DoesNotContain("Private!PreviousSentinel", json);
    }

    [Fact]
    public void PublicKeyRingLoadsTheSuppliedPrivateCertificateWithoutChangingTrust()
    {
        using var files = new CertificateFiles(); var hosting = Read(files.Settings());
        var services = new ServiceCollection(); services.AddLogging();
        using var certificates = hosting.ConfigureKeyRing(services.AddDataProtection());
        Assert.NotNull(certificates); Assert.True(certificates.Current.HasPrivateKey); Assert.Equal(1, certificates.Count);
        using var rsa = certificates.Current.GetRSAPrivateKey(); Assert.Equal(3072, rsa!.KeySize);
    }

    [Fact]
    public void InvalidCertificatePasswordFailsWithoutFallbackOrLeakingTheSecret()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        settings["QuickPrompt:Hosting:KeyRingCertificatePassword"] = "WrongPrivate!Password";
        var hosting = Read(settings); var services = new ServiceCollection(); services.AddLogging();
        var error = Assert.Throws<InvalidOperationException>(() => hosting.ConfigureKeyRing(services.AddDataProtection()));
        Assert.DoesNotContain("WrongPrivate!Password", error.Message);
        Assert.Contains("no fallback", error.Message);
    }

    [Theory]
    [InlineData(SslMode.Prefer, SslMode.VerifyFull)]
    [InlineData(SslMode.Require, SslMode.VerifyFull)]
    [InlineData(SslMode.Disable, SslMode.VerifyFull)]
    [InlineData(SslMode.VerifyCA, SslMode.VerifyFull)]
    [InlineData(SslMode.VerifyFull, SslMode.Prefer)]
    [InlineData(SslMode.VerifyFull, SslMode.Require)]
    [InlineData(SslMode.VerifyFull, SslMode.Disable)]
    public void PublicDatabaseConnectionsRequireFullServerIdentityValidation(SslMode accounts, SslMode execution)
    {
        using var files = new CertificateFiles(); var hosting = Read(files.Settings());
        var error = Assert.Throws<InvalidOperationException>(() => hosting.RequireDatabaseTls(
            new() { Host = "pg.example", SslMode = accounts, Password = "Private!Sentinel" },
            new() { Host = "pg.example", SslMode = execution, Password = "Private!Sentinel" }));
        Assert.DoesNotContain("Private!Sentinel", error.Message);
        Assert.Contains("VerifyFull", error.Message);
    }

    [Fact]
    public void VerifiedPublicDatabaseConnectionsAndExplicitLocalQaSettingsRemainAccepted()
    {
        using var files = new CertificateFiles(); var hosting = Read(files.Settings());
        hosting.RequireDatabaseTls(new() { SslMode = SslMode.VerifyFull }, new() { SslMode = SslMode.VerifyFull });
        Read(new(), "Development").RequireDatabaseTls(new() { SslMode = SslMode.Disable }, new() { SslMode = SslMode.Prefer });
    }

    [Fact]
    public void PreviousCertificatesAreBoundedAndCanDecryptAfterTheirEncryptionValidityEnds()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        for (var index = 0; index < 4; index++) {
            settings[$"QuickPrompt:Hosting:PreviousKeyRingCertificates:{index}:Path"] = files.AddCertificate("previous-" + index, expired: true);
            settings[$"QuickPrompt:Hosting:PreviousKeyRingCertificates:{index}:Password"] = "Synthetic!KeyRing79";
        }
        var services = new ServiceCollection(); services.AddLogging();
        using var certificates = Read(settings).ConfigureKeyRing(services.AddDataProtection());
        Assert.Equal(5, certificates!.Count);
        settings["QuickPrompt:Hosting:PreviousKeyRingCertificates:4:Path"] = files.AddCertificate("fifth");
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    [Fact]
    public void CertificateSecretPathsCannotFollowAReparsePoint()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        // A directory symlink exercises the same reparse-point boundary as a Windows junction.
        var original = Path.GetDirectoryName(files.CertificatePath)!;
        var linked = original + "-link";
        Directory.CreateSymbolicLink(linked, original);
        try {
            settings["QuickPrompt:Hosting:KeyRingCertificatePath"] = Path.Combine(linked, "test.pfx");
            Assert.Throws<InvalidOperationException>(() => Read(settings));
        } finally { Directory.Delete(linked); }
    }

    [Fact]
    public void CertificateOwnerReleasesItsCurrentCertificate()
    {
        using var files = new CertificateFiles(); var services = new ServiceCollection(); services.AddLogging();
        var certificates = Read(files.Settings()).ConfigureKeyRing(services.AddDataProtection())!;
        certificates.Dispose();
        Assert.Throws<CryptographicException>(() => certificates.Current.RawData);
    }

    [Fact]
    public void AutomaticRollbackCannotDropTheHostKeyRing()
    {
        var error = Assert.Throws<NotSupportedException>(() => new QuickPrompt.Accounts.Persistence.Migrations.PersistHostDataProtection().DownOperations);
        Assert.Contains("rollback is blocked", error.Message);
    }

    [Theory]
    [InlineData("10.32.0.7", "https", "public.example", true)]
    [InlineData("10.32.0.8", "https", "public.example", false)]
    [InlineData("10.32.0.7", "http", "public.example", false)]
    [InlineData("10.32.0.7", "https", "attacker.example", false)]
    public async Task ActualForwardingMiddlewareTrustsOnlyTheConfiguredProxyBeforeTheCanonicalOriginGate(string remote, string forwardedScheme, string host, bool permitted)
    {
        using var files = new CertificateFiles(); var hosting = Read(files.Settings());
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(remote); return next(context); });
        hosting.ConfigureIngress(app);
        app.Run(async context => { context.Response.StatusCode = hosting.Accepts(context.Request) ? 200 : 403; await context.Response.WriteAsync("checked"); });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/"); request.Headers.Host = host;
        request.Headers.Add("X-Forwarded-Proto", forwardedScheme); request.Headers.Add("X-Forwarded-For", "203.0.113.4");
        using var response = await client.SendAsync(request);
        Assert.Equal(permitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void CanonicalHostNeverComesFromForwardedHostAndCrossOriginPostsRemainRejected()
    {
        using var files = new CertificateFiles(); var hosting = Read(files.Settings());
        Assert.False(hosting.Forwarding().ForwardedHeaders.HasFlag(Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost));
        var context = new DefaultHttpContext(); context.Request.Scheme = "https"; context.Request.Host = new("public.example");
        context.Request.Method = "POST"; context.Request.Headers.Origin = "https://attacker.example";
        Assert.False(hosting.Accepts(context.Request));
        context.Request.Headers.Origin = "https://public.example"; Assert.True(hosting.Accepts(context.Request));
        context.Request.Headers.Origin = "null"; Assert.False(hosting.Accepts(context.Request));
    }

    [Fact]
    public void RetiredRenderSpecificProfileIsRejected()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        settings["QuickPrompt:Hosting:IngressProfile"] = "RenderFreeEdgeOnly";
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    [Fact]
    public void CloudEnvironmentNeverAutomaticallySelectsOriginProxy()
    {
        using var files = new CertificateFiles(); var settings = files.Settings();
        settings["RENDER"] = "true"; settings["VERCEL"] = "1";
        Assert.False(Read(settings).OriginProxy);
        settings.Remove("QuickPrompt:Hosting:TrustedProxies:0");
        Assert.Throws<InvalidOperationException>(() => Read(settings));
    }

    private static AccountHosting Read(Dictionary<string, string?> settings, string environment = "Production") =>
        AccountHosting.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new EnvironmentStub(environment));

    internal sealed class EnvironmentStub(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "QuickPrompt.Hosting.QA";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal sealed class CertificateFiles : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "quickprompt-host-qa-" + Guid.NewGuid().ToString("N"));
        public string CertificatePath => Path.Combine(directory, "test.pfx");
        public CertificateFiles()
        {
            Directory.CreateDirectory(directory); _ = AddCertificate("test");
        }
        public string AddCertificate(string name, bool expired = false)
        {
            using var key = RSA.Create(3072);
            var request = new CertificateRequest("CN=QuickPrompt QA key ring", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(expired ? DateTimeOffset.UtcNow.AddDays(-2) : DateTimeOffset.UtcNow.AddMinutes(-5),
                expired ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddDays(1));
            var path = Path.Combine(directory, name + ".pfx");
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "Synthetic!KeyRing79"));
            return path;
        }
        public Dictionary<string, string?> Settings() => new() {
            ["QuickPrompt:Hosting:Mode"] = "Public", ["QuickPrompt:Accounts:Origin"] = "https://public.example",
            ["QuickPrompt:Hosting:ListenUrl"] = "http://0.0.0.0:8080", ["QuickPrompt:Hosting:TrustedProxies:0"] = "10.32.0.7",
            ["QuickPrompt:Hosting:KeyRingCertificatePath"] = CertificatePath, ["QuickPrompt:Hosting:KeyRingCertificatePassword"] = "Synthetic!KeyRing79"
        };
        public void Dispose() { Directory.Delete(directory, true); }
    }
}
