using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using QuickPrompt.Recovery;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace QuickPrompt.Recovery.Tests;

/// <summary>Keys and backup contents stay in memory: no PFX/password files, certificate store, database or network.</summary>
public sealed class BackupArchiveTests : IDisposable
{
    private readonly X509Certificate2 certificate = Recipient();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private readonly byte[] plaintext = "PGDMP\0Synthetic backup data, not a valid database or any personal content."u8.ToArray();
    private string Pin => BackupRecipient.Fingerprint(certificate);
    private byte[] Protect() => BackupArchive.Protect(plaintext, BackupArchive.Digest(plaintext), certificate, Pin, now, now.AddDays(1));
    private byte[] Recover(byte[] archive, X509Certificate2? key = null, DateTimeOffset? clock = null) =>
        BackupArchive.Recover(archive, BackupArchive.Digest(archive), BackupArchive.Digest(plaintext), key ?? certificate,
            key is null ? Pin : BackupRecipient.Fingerprint(key), clock ?? now);

    [Fact]
    public void ExactBinaryRoundTripWithPublicRecipientAndFreshReader()
    {
        using var publicRecipient = X509CertificateLoader.LoadCertificate(certificate.RawData);
        Assert.False(publicRecipient.HasPrivateKey);
        var bytes = RandomNumberGenerator.GetBytes(323100);
        var hash = BackupArchive.Digest(bytes);
        var archive = BackupArchive.Protect(bytes, hash, publicRecipient, Pin, now, now.AddDays(1));
        var recovered = BackupArchive.Recover(archive, BackupArchive.Digest(archive), hash, certificate, Pin, now);
        Assert.Equal(bytes, recovered);
        Assert.DoesNotContain(Convert.ToHexString(bytes.AsSpan(0, 64)), Convert.ToHexString(archive));
    }

    [Fact]
    public void EachProtectionUsesIndependentCiphertext()
    {
        var first = Protect(); var second = Protect();
        Assert.NotEqual(BackupArchive.Digest(first), BackupArchive.Digest(second));
        Assert.Equal(plaintext, Recover(first)); Assert.Equal(plaintext, Recover(second));
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("nonce")]
    [InlineData("content")]
    [InlineData("wrapped-key")]
    public void TamperingFailsEvenWithRecomputedOuterHash(string component)
    {
        var archive = Protect();
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(archive.AsSpan(8));
        var keyOffset = 12 + headerLength;
        var prefix = keyOffset + 2 + BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(keyOffset));
        var offset = component switch { "tag" => prefix + 12, "nonce" => prefix, "wrapped-key" => keyOffset + 2, _ => prefix + 28 };
        archive[offset] ^= 0x40;
        Assert.ThrowsAny<CryptographicException>(() => Recover(archive));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("whitespace")]
    [InlineData("unknown")]
    public void NoncanonicalOrUnsupportedMetadataFailsBeforeRecovery(string change)
    {
        var archive = Protect();
        var length = BinaryPrimitives.ReadInt32LittleEndian(archive.AsSpan(8));
        var header = Encoding.UTF8.GetString(archive, 12, length);
        var changed = change switch {
            "duplicate" => header.Replace("\"version\":1,", "\"version\":1,\"version\":1,"),
            "version" => header.Replace("\"version\":1", "\"version\":2"),
            "whitespace" => header.Replace("{", "{ "),
            _ => header.Replace("{", "{\"unknown\":true,")
        };
        var encoded = Encoding.UTF8.GetBytes(changed);
        var modified = new byte[archive.Length - length + encoded.Length];
        archive.AsSpan(0, 8).CopyTo(modified);
        BinaryPrimitives.WriteInt32LittleEndian(modified.AsSpan(8), encoded.Length);
        encoded.CopyTo(modified.AsSpan(12));
        archive.AsSpan(12 + length).CopyTo(modified.AsSpan(12 + encoded.Length));
        Assert.Throws<InvalidDataException>(() => Recover(modified));
    }

    [Fact]
    public void ExpectedHashesAndRecipientPinAreRequired()
    {
        var archive = Protect(); var other = new string('0', 64);
        Assert.Throws<CryptographicException>(() => BackupArchive.Protect(plaintext, other, certificate, Pin, now, now.AddDays(1)));
        Assert.Throws<CryptographicException>(() => BackupArchive.Recover(archive, other, BackupArchive.Digest(plaintext), certificate, Pin, now));
        Assert.Throws<CryptographicException>(() => BackupArchive.Recover(archive, BackupArchive.Digest(archive), BackupArchive.Digest(plaintext), certificate, other, now));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Recover(archive, BackupArchive.Digest(archive), other, certificate, Pin, now));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Protect(plaintext, BackupArchive.Digest(plaintext).ToLowerInvariant(), certificate, Pin, now, now.AddDays(1)));
    }

    [Fact]
    public void CanonicalMetadataStillRequiresItsOriginalAuthenticationTag()
    {
        var archive = Protect();
        var length = BinaryPrimitives.ReadInt32LittleEndian(archive.AsSpan(8));
        var header = Encoding.UTF8.GetString(archive, 12, length);
        // Utf8JsonWriter escapes '+' in UTC offsets. Replace the serialized value,
        // not its unescaped date string, so this probe actually changes authenticated bytes.
        var changed = header.Replace(JsonSerializer.Serialize(now.AddDays(1).ToString("O")), JsonSerializer.Serialize(now.AddDays(2).ToString("O")));
        Assert.NotEqual(header, changed);
        Encoding.UTF8.GetBytes(changed).CopyTo(archive.AsSpan(12, length));
        Assert.ThrowsAny<CryptographicException>(() => Recover(archive));
    }

    [Fact]
    public void MaliciousLengthAndUnknownFormatAreRejected()
    {
        var archive = Protect();
        BinaryPrimitives.WriteInt32LittleEndian(archive.AsSpan(8), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => Recover(archive));
        var unknown = Protect(); unknown[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => Recover(unknown));
    }

    [Fact]
    public void WrongCertificateAndPublicOnlyKeyCannotRecover()
    {
        var archive = Protect();
        using var wrong = Recipient();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        Assert.Throws<InvalidDataException>(() => Recover(archive, wrong));
        Assert.Throws<CryptographicException>(() => Recover(archive, publicOnly));
    }

    [Fact]
    public void UnrelatedHostOrTlsCertificateIsRejected()
    {
        using var unrelated = Recipient(documentEncryption: false);
        var pin = BackupRecipient.Fingerprint(unrelated);
        Assert.Throws<CryptographicException>(() => BackupArchive.Protect(plaintext, BackupArchive.Digest(plaintext), unrelated, pin, now, now.AddDays(1)));
    }

    [Fact]
    public void RsaMinimumIsExplicitAndSmallKeysAreRejected()
    {
        using var minimum = Recipient(keyBits: 2048);
        var pin = BackupRecipient.Fingerprint(minimum);
        var hash = BackupArchive.Digest(plaintext);
        var archive = BackupArchive.Protect(plaintext, hash, minimum, pin, now, now.AddDays(1));
        Assert.Equal(plaintext, BackupArchive.Recover(archive, BackupArchive.Digest(archive), hash, minimum, pin, now));
        using var weak = Recipient(keyBits: 1024);
        Assert.Throws<CryptographicException>(() => BackupArchive.Protect(plaintext, hash, weak, BackupRecipient.Fingerprint(weak), now, now.AddDays(1)));
    }

    [Fact]
    public void BoundsTruncationTrailingDataAndRetentionAreEnforced()
    {
        var oversized = new byte[BackupArchive.MaximumPlaintextBytes + 1];
        Assert.Throws<InvalidDataException>(() => BackupArchive.Protect(oversized, new string('0', 64), certificate, Pin, now, now.AddDays(1)));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Protect([], BackupArchive.Digest([]), certificate, Pin, now, now.AddDays(1)));
        var archive = Protect();
        Assert.Throws<InvalidDataException>(() => Recover(archive[..^1]));
        Assert.Throws<InvalidDataException>(() => Recover([.. archive, 0]));
        Assert.Throws<InvalidDataException>(() => Recover(archive, clock: now.AddDays(2)));
        Assert.Throws<InvalidDataException>(() => Recover(archive, clock: now.AddMinutes(-1)));
        Assert.Throws<InvalidDataException>(() => BackupArchive.Protect(plaintext, BackupArchive.Digest(plaintext), certificate, Pin, now, now));
    }

    private static X509Certificate2 Recipient(bool documentEncryption = true, int keyBits = 3072)
    {
        using var rsa = RSA.Create(keyBits);
        var request = new CertificateRequest("CN=QuickPrompt synthetic backup recipient", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
        if (documentEncryption) request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(BackupRecipient.DocumentEncryptionOid) }, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
    }

    public void Dispose() => certificate.Dispose();
}
