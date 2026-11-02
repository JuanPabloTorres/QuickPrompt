using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace QuickPrompt.Recovery;

/// <summary>Verify an archive in memory; no file path, writer, database or plaintext result escapes this boundary.</summary>
internal static class RecoveryVerification
{
    internal static int Verify(ReadOnlySpan<byte> archive, string expectedArchiveSha256, string expectedPlaintextSha256,
        X509Certificate2 recipient, string expectedRecipientSha256, DateTimeOffset nowUtc)
    {
        var plaintext = BackupArchive.Recover(archive, expectedArchiveSha256, expectedPlaintextSha256, recipient, expectedRecipientSha256, nowUtc);
        try
        {
            if (!plaintext.AsSpan().StartsWith("PGDMP"u8)) throw new InvalidDataException("The recovered content is not a PostgreSQL custom-format backup.");
            return plaintext.Length;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}
