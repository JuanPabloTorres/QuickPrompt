using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace QuickPrompt.Recovery;

/// <summary>
/// Bounded, versioned archive encryption using platform primitives. No files, network, credential generation,
/// database permissions or restore authority. Authentication and the externally expected hashes precede output.
/// </summary>
public static class BackupArchive
{
    public const int MaximumPlaintextBytes = 64 * 1024 * 1024;
    public const int MaximumArchiveBytes = MaximumPlaintextBytes + 8192;
    private static ReadOnlySpan<byte> Magic => "QPBKUP1\0"u8;

    public static byte[] Protect(ReadOnlySpan<byte> plaintext, string expectedPlaintextSha256, X509Certificate2 recipient,
        string expectedRecipientSha256, DateTimeOffset createdUtc, DateTimeOffset retainUntilUtc)
    {
        if (plaintext.Length is < 1 or > MaximumPlaintextBytes) throw new InvalidDataException("The backup exceeds its supported size or is empty.");
        RequireDigest(plaintext, expectedPlaintextSha256);
        BackupRecipient.RequireDedicated(recipient, expectedRecipientSha256);
        if (createdUtc.UtcDateTime < recipient.NotBefore.ToUniversalTime() || createdUtc.UtcDateTime > recipient.NotAfter.ToUniversalTime())
            throw new CryptographicException("The encryption recipient is outside its validity period.");
        var metadata = new ArchiveMetadata(expectedRecipientSha256, expectedPlaintextSha256, plaintext.Length, createdUtc, retainUntilUtc);
        var header = metadata.Encode();
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var rsa = recipient.GetRSAPublicKey()!;
            var wrappedKey = rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            var prefixLength = Magic.Length + sizeof(int) + header.Length + sizeof(ushort) + wrappedKey.Length;
            var archive = new byte[prefixLength + 12 + 16 + plaintext.Length];
            Magic.CopyTo(archive);
            BinaryPrimitives.WriteInt32LittleEndian(archive.AsSpan(Magic.Length), header.Length);
            header.CopyTo(archive.AsSpan(Magic.Length + sizeof(int)));
            var keyOffset = Magic.Length + sizeof(int) + header.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(archive.AsSpan(keyOffset), checked((ushort)wrappedKey.Length));
            wrappedKey.CopyTo(archive.AsSpan(keyOffset + sizeof(ushort)));
            var nonce = archive.AsSpan(prefixLength, 12);
            RandomNumberGenerator.Fill(nonce);
            using var cipher = new AesGcm(key, 16);
            cipher.Encrypt(nonce, plaintext, archive.AsSpan(prefixLength + 28), archive.AsSpan(prefixLength + 12, 16), archive.AsSpan(0, prefixLength));
            return archive;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public static byte[] Recover(ReadOnlySpan<byte> archive, string expectedArchiveSha256, string expectedPlaintextSha256,
        X509Certificate2 recipient, string expectedRecipientSha256, DateTimeOffset nowUtc)
    {
        if (archive.Length is < 64 or > MaximumArchiveBytes) throw new InvalidDataException("Invalid archive size.");
        RequireDigest(archive, expectedArchiveSha256);
        BackupRecipient.RequireDedicated(recipient, expectedRecipientSha256);
        if (!archive.StartsWith(Magic)) throw new InvalidDataException("Unsupported backup format.");
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(archive[Magic.Length..]);
        if (headerLength is < 1 or > ArchiveMetadata.MaximumHeaderBytes || headerLength > archive.Length - Magic.Length - 34)
            throw new InvalidDataException("Invalid archive header length.");
        var metadata = ArchiveMetadata.Decode(archive.Slice(Magic.Length + sizeof(int), headerLength));
        RequireHash(expectedPlaintextSha256);
        if (metadata.RecipientSha256 != expectedRecipientSha256 || metadata.PlaintextSha256 != expectedPlaintextSha256
            || nowUtc.Offset != TimeSpan.Zero || nowUtc < metadata.CreatedUtc || nowUtc > metadata.RetainUntilUtc)
            throw new InvalidDataException("The recipient, plaintext hash or explicit retention window differs.");
        var keyOffset = Magic.Length + sizeof(int) + headerLength;
        var keyLength = BinaryPrimitives.ReadUInt16LittleEndian(archive[keyOffset..]);
        using var rsa = recipient.GetRSAPrivateKey() ?? throw new CryptographicException("The approved backup private key is required.");
        if (keyLength != rsa.KeySize / 8) throw new InvalidDataException("Invalid wrapped-key size.");
        var prefixLength = keyOffset + sizeof(ushort) + keyLength;
        if ((long)prefixLength + 28 + metadata.PlaintextBytes != archive.Length) throw new InvalidDataException("Truncated archive or trailing bytes.");
        var key = rsa.Decrypt(archive.Slice(keyOffset + sizeof(ushort), keyLength), RSAEncryptionPadding.OaepSHA256);
        var plaintext = new byte[metadata.PlaintextBytes];
        try
        {
            if (key.Length != 32) throw new CryptographicException("Invalid content-encryption key.");
            using var cipher = new AesGcm(key, 16);
            cipher.Decrypt(archive.Slice(prefixLength, 12), archive[(prefixLength + 28)..], archive.Slice(prefixLength + 12, 16), plaintext, archive[..prefixLength]);
            RequireDigest(plaintext, expectedPlaintextSha256);
            return plaintext;
        }
        catch { CryptographicOperations.ZeroMemory(plaintext); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static void RequireHash(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new InvalidDataException("An explicit uppercase SHA-256 fingerprint is required.");
    }

    private static void RequireDigest(ReadOnlySpan<byte> bytes, string expected)
    {
        RequireHash(expected);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(expected)))
            throw new CryptographicException("The expected SHA-256 hash differs.");
    }
}
