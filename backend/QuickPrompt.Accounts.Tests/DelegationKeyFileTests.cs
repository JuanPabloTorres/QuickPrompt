using System.Security.Cryptography;
using QuickPrompt.Cloud.Delegation;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class DelegationKeyFileTests
{
    [Fact]
    public void OperatorSuppliedPkcs8FilesRetainSigningEncryptionAndStableIdentity()
    {
        using var files = new KeyFiles();
        using var first = DelegationKeys.Load(files.Directory);
        using var second = DelegationKeys.Load(files.Directory);
        Assert.Equal(first.Signing.KeyId, second.Signing.KeyId);
        Assert.Equal(first.Encryption.KeyId, second.Encryption.KeyId);
        Assert.NotEqual(first.Signing.KeyId, first.Encryption.KeyId);
        byte[] message = [1, 4, 9, 16];
        var signature = first.Signing.Rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Assert.True(files.Signing.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var encrypted = first.Encryption.Rsa.Encrypt(message, RSAEncryptionPadding.OaepSHA256);
        Assert.Equal(message, files.Encryption.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256));
    }

    [Fact]
    public void PublicOnlyFilesCannotBecomeDelegatedPrivateCredentials()
    {
        using var files = new KeyFiles();
        File.WriteAllText(Path.Combine(files.Directory, "signing.pem"), files.Signing.ExportSubjectPublicKeyInfoPem());
        Assert.Throws<CryptographicException>(() => DelegationKeys.Load(files.Directory));
    }

    [Fact]
    public void WeakPrivateFilesAreRejectedByExistingCryptographicOwner()
    {
        using var files = new KeyFiles(); using var weak = RSA.Create(2048);
        File.WriteAllText(Path.Combine(files.Directory, "encryption.pem"), weak.ExportPkcs8PrivateKeyPem());
        Assert.Throws<InvalidOperationException>(() => DelegationKeys.Load(files.Directory));
    }

    [Fact]
    public void MissingFileDoesNotGenerateReplacementCredentials()
    {
        using var files = new KeyFiles(); var missing = Path.Combine(files.Directory, "encryption.pem");
        File.Delete(missing);
        Assert.Throws<FileNotFoundException>(() => DelegationKeys.Load(files.Directory));
        Assert.False(File.Exists(missing));
    }

    private sealed class KeyFiles : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "QuickPromptSyntheticDelegation-" + Guid.NewGuid().ToString("N"));
        public RSA Signing { get; } = RSA.Create(3072);
        public RSA Encryption { get; } = RSA.Create(3072);
        public KeyFiles()
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, "signing.pem"), Signing.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(Path.Combine(Directory, "encryption.pem"), Encryption.ExportPkcs8PrivateKeyPem());
        }
        public void Dispose()
        {
            Signing.Dispose(); Encryption.Dispose(); System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
