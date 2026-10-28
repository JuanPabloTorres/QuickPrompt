using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Cloud;
using QuickPrompt.Recovery;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real synthetic dump, authenticated archive encryption, recovery and PostgreSQL restore. No production data.</summary>
public sealed class HostKeyRingRestorePostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Container = "quickprompt-saas-postgres";
    private const string SourceDatabase = "quickprompt_development";

    [Fact]
    public async Task RestoredEncryptedKeyRingDecryptsPreBackupPayloadOnlyWithItsCertificate()
    {
        await RequireDevelopmentContainer();
        var fixture = Guid.NewGuid().ToString("N");
        var restoredDatabase = "qp_keyring_restore_" + fixture;
        var archive = "/tmp/qp_keyring_restore_" + fixture + ".dump";
        var applicationName = "QuickPrompt.Restore.QA." + fixture;
        const string purpose = "qa.restored-cookie";
        const string plaintext = "Synthetic session protected before database backup";
        using var certificate = CreateCertificate("QuickPrompt QA restore");
        using var wrongCertificate = CreateCertificate("QuickPrompt QA unrelated certificate");
        using var backupRecipient = CreateBackupRecipient();
        byte[]? backupBytes = null, encryptedArchive = null, recoveredBytes = null;
        var created = false;
        try
        {
            string protectedPayload;
            Guid keyId;
            using (var beforeBackup = Provider(postgres.Application, applicationName, certificate))
            {
                // The shared synthetic ring can contain older QA certificates. Select a new
                // key explicitly; disabling generation prevents a reader from repairing a restore.
                var activation = DateTimeOffset.UtcNow;
                keyId = beforeBackup.GetRequiredService<IKeyManager>().CreateNewKey(activation, activation.AddDays(1)).KeyId;
                var protector = beforeBackup.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose);
                protectedPayload = protector.Protect(plaintext);
                Assert.Equal(plaintext, protector.Unprotect(protectedPayload));
            }

            await Docker("exec", Container, "pg_dump", "-U", "quickprompt_bootstrap", "-d", SourceDatabase, "-Fc", "-f", archive);
            // Read the real custom-format dump as binary, never a UTF-8 string. The archive
            // private key is distinct from the Data Protection key and exists only in memory.
            backupBytes = await DockerBinary(null, BackupArchive.MaximumPlaintextBytes, "exec", Container, "cat", archive);
            Assert.True(backupBytes.AsSpan().StartsWith("PGDMP"u8));
            var sourceHash = BackupArchive.Digest(backupBytes);
            var recipientPin = BackupRecipient.Fingerprint(backupRecipient);
            var protectedAt = DateTimeOffset.UtcNow;
            encryptedArchive = BackupArchive.Protect(backupBytes, sourceHash, backupRecipient, recipientPin, protectedAt, protectedAt.AddDays(1));
            recoveredBytes = BackupArchive.Recover(encryptedArchive, BackupArchive.Digest(encryptedArchive), sourceHash,
                backupRecipient, recipientPin, DateTimeOffset.UtcNow);
            Assert.Equal(backupBytes, recoveredBytes);
            await Docker("exec", Container, "createdb", "-U", "quickprompt_bootstrap", "-O", "quickprompt_migrator", restoredDatabase);
            created = true;
            // pg_restore receives only the authenticated recovery result through binary stdin.
            _ = await DockerBinary(recoveredBytes, 64 * 1024, "exec", "-i", Container, "pg_restore", "-U", "quickprompt_bootstrap",
                "--role=quickprompt_migrator", "-d", restoredDatabase, "--exit-on-error");

            var connection = new NpgsqlConnectionStringBuilder(postgres.ApplicationConnection) { Database = restoredDatabase };
            Assert.NotEqual(SourceDatabase, connection.Database);
            await using var restored = NpgsqlDataSource.Create(connection.ConnectionString);
            await new HostKeyRingGuard(restored).StartAsync(default);
            using (var afterRestore = Provider(restored, applicationName, certificate))
            {
                var key = Assert.Single(afterRestore.GetRequiredService<IKeyManager>().GetAllKeys(), entry => entry.KeyId == keyId);
                Assert.False(key.IsRevoked);
                Assert.Equal(plaintext, afterRestore.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose).Unprotect(protectedPayload));
            }
            using (var missingCertificate = Provider(restored, applicationName, wrongCertificate))
                Assert.ThrowsAny<CryptographicException>(() => missingCertificate.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector(purpose).Unprotect(protectedPayload));
            using (var otherApplication = Provider(restored, applicationName + ".other", certificate))
                Assert.ThrowsAny<CryptographicException>(() => otherApplication.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector(purpose).Unprotect(protectedPayload));
        }
        finally
        {
            if (backupBytes is not null) CryptographicOperations.ZeroMemory(backupBytes);
            if (encryptedArchive is not null) CryptographicOperations.ZeroMemory(encryptedArchive);
            if (recoveredBytes is not null) CryptographicOperations.ZeroMemory(recoveredBytes);
            // Names contain only the fixed fixture prefix and this method's generated UUID.
            // No FORCE, recursive file removal, role changes or production destination is accepted.
            try
            {
                if (created) await Docker("exec", Container, "dropdb", "-U", "quickprompt_bootstrap", "--if-exists", restoredDatabase);
            }
            finally { await Docker("exec", Container, "rm", "-f", "--", archive); }
        }
    }

    private static ServiceProvider Provider(NpgsqlDataSource data, string applicationName, X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AccountDatabase>(options => options.UseNpgsql(data));
        services.AddDataProtection().SetApplicationName(applicationName).PersistKeysToDbContext<AccountDatabase>()
            .ProtectKeysWithCertificate(certificate).DisableAutomaticKeyGeneration();
        return services.BuildServiceProvider();
    }

    private static X509Certificate2 CreateCertificate(string name)
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static X509Certificate2 CreateBackupRecipient()
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=QuickPrompt synthetic dedicated backup recipient", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(BackupRecipient.DocumentEncryptionOid) }, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
    }

    private static async Task<byte[]> DockerBinary(byte[]? input, int maximumOutputBytes, params string[] arguments)
    {
        using var process = new Process { StartInfo = new("docker") { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = input is not null, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "The isolated binary PostgreSQL command could not start.");
        var diagnostics = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var output = ReadBounded(process.StandardOutput.BaseStream, maximumOutputBytes, deadline.Token);
            if (input is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(input, deadline.Token);
                process.StandardInput.Close();
            }
            var bytes = await output;
            await process.WaitForExitAsync(deadline.Token);
            _ = await diagnostics;
            Assert.True(process.ExitCode == 0, "The isolated binary PostgreSQL command failed; database diagnostics were not printed.");
            return bytes;
        }
        catch
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            throw;
        }
    }

    private static async Task<byte[]> ReadBounded(Stream source, int maximumBytes, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellation)) != 0)
        {
            if (output.Length + count > maximumBytes) throw new InvalidDataException("The synthetic archive exceeds the supported recovery limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
        }
        return output.ToArray();
    }

    private static async Task RequireDevelopmentContainer()
    {
        var project = await Docker("inspect", "--format", "{{index .Config.Labels \"com.docker.compose.project\"}}", Container);
        var scope = await Docker("inspect", "--format", "{{index .Config.Labels \"io.quickprompt.scope\"}}", Container);
        Assert.Equal("quickprompt-saas-development", project);
        Assert.Equal("saas-development", scope);
    }

    private static async Task<string> Docker(params string[] arguments)
    {
        using var process = new Process { StartInfo = new("docker") {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "The isolated PostgreSQL command could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var diagnostics = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("The isolated PostgreSQL command exceeded its deadline.");
        }
        var result = await output;
        _ = await diagnostics;
        Assert.True(process.ExitCode == 0, "The isolated PostgreSQL command failed; database diagnostics were not printed.");
        return result.Trim();
    }
}
