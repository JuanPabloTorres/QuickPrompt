using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Actual official EF/PostgreSQL provider and two independent process-equivalent service containers.</summary>
public sealed class HostKeyRingPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void CertificateRotationPreservesOldPayloadsAndEncryptsNewKeysWithTheNewCertificate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quickprompt-key-rotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string Certificate(string name)
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=QuickPrompt QA rotation", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            var path = Path.Combine(directory, name + ".pfx");
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "Synthetic!Rotation79"));
            return path;
        }
        var previous = Certificate("previous"); var current = Certificate("current");
        var application = "QuickPrompt.Rotation.QA." + Guid.NewGuid().ToString("N");
        ServiceProvider Provider(string certificate, string? prior = null)
        {
            var settings = new Dictionary<string, string?> {
                ["QuickPrompt:Hosting:Mode"] = "Public", ["QuickPrompt:Accounts:Origin"] = "https://public.example",
                ["QuickPrompt:Hosting:ListenUrl"] = "http://0.0.0.0:8080", ["QuickPrompt:Hosting:TrustedProxies:0"] = "10.32.0.7",
                ["QuickPrompt:Hosting:KeyRingCertificatePath"] = certificate, ["QuickPrompt:Hosting:KeyRingCertificatePassword"] = "Synthetic!Rotation79"
            };
            if (prior is not null) {
                settings["QuickPrompt:Hosting:PreviousKeyRingCertificates:0:Path"] = prior;
                settings["QuickPrompt:Hosting:PreviousKeyRingCertificates:0:Password"] = "Synthetic!Rotation79";
            }
            var hosting = AccountHosting.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new RotationEnvironment());
            var services = new ServiceCollection(); services.AddLogging();
            services.AddDbContext<AccountDatabase>(options => options.UseNpgsql(postgres.Application));
            var protection = services.AddDataProtection().SetApplicationName(application).DisableAutomaticKeyGeneration();
            var certificates = hosting.ConfigureKeyRing(protection)!;
            services.AddSingleton(_ => certificates);
            var provider = services.BuildServiceProvider(); _ = provider.GetRequiredService<AccountKeyRingCertificates>(); return provider;
        }
        try {
            string oldPayload, newPayload;
            using (var first = Provider(previous)) {
                // Keys are host-wide. Activate after earlier QA keys so this certificate's key is selected explicitly.
                var activation = DateTimeOffset.UtcNow;
                first.GetRequiredService<IKeyManager>().CreateNewKey(activation, activation.AddDays(90));
                oldPayload = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.rotation").Protect("Synthetic old session");
            }
            using (var rotating = Provider(current, previous)) {
                Assert.Equal("Synthetic old session", rotating.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.rotation").Unprotect(oldPayload));
                var activation = DateTimeOffset.UtcNow;
                rotating.GetRequiredService<IKeyManager>().CreateNewKey(activation, activation.AddDays(90));
            }
            // A reader can still serve its cached ring while the official provider refreshes asynchronously.
            // Restart the writer after propagation; do not mistake a successful CreateNewKey for cache convergence.
            using (var restarted = Provider(current, previous)) {
                newPayload = restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.rotation").Protect("Synthetic new session");
            }
            using (var replaced = Provider(current)) {
                var protector = replaced.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.rotation");
                Assert.Equal("Synthetic new session", protector.Unprotect(newPayload));
                Assert.Throws<CryptographicException>(() => protector.Unprotect(oldPayload));
            }
        } finally { Directory.Delete(directory, true); }
    }

    private sealed class RotationEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "QuickPrompt.QA";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task EncryptedHostKeysSurviveProviderReplacementWithAppendOnlyApplicationPermissions()
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=QuickPrompt QA host keys", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        var name = "QuickPrompt.Hosting.QA." + Guid.NewGuid().ToString("N");
        ServiceProvider Provider(string application)
        {
            var services = new ServiceCollection(); services.AddLogging();
            services.AddDbContext<AccountDatabase>(options => options.UseNpgsql(postgres.Application));
            services.AddDataProtection().SetApplicationName(application).PersistKeysToDbContext<AccountDatabase>().ProtectKeysWithCertificate(certificate);
            return services.BuildServiceProvider();
        }
        string protectedText;
        using (var first = Provider(name))
            protectedText = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.cookie").Protect("Synthetic session across restart");
        using (var second = Provider(name))
            Assert.Equal("Synthetic session across restart", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.cookie").Unprotect(protectedText));
        using (var other = Provider(name + ".different-host"))
            Assert.Throws<CryptographicException>(() => other.GetRequiredService<IDataProtectionProvider>().CreateProtector("qa.cookie").Unprotect(protectedText));

        await new HostKeyRingGuard(postgres.Application).StartAsync(default);
        await using var scope = postgres.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountDatabase>();
        var keys = await database.DataProtectionKeys.AsNoTracking().ToArrayAsync();
        Assert.NotEmpty(keys); Assert.All(keys, key => Assert.Contains("encryptedSecret", key.Xml));
        Assert.All(keys, key => Assert.DoesNotContain("Synthetic session across restart", key.Xml));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var erase = new NpgsqlCommand("DELETE FROM qp_host.data_protection_keys WHERE false", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => erase.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await using var dispatcher = new NpgsqlConnection(postgres.ExecutionConnection); await dispatcher.OpenAsync();
        await using var visible = new NpgsqlCommand("SELECT has_schema_privilege(current_user,'qp_host','USAGE')", dispatcher);
        Assert.False((bool)(await visible.ExecuteScalarAsync())!);
    }
}
