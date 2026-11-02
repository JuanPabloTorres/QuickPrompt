using System.Globalization;
using System.Text.Json;

namespace QuickPrompt.Recovery;

/// <summary>Authenticated operator choices; they do not authorize a database restore or certify an erasure ledger.</summary>
public sealed record ArchiveMetadata(string RecipientSha256, string PlaintextSha256, int PlaintextBytes,
    DateTimeOffset CreatedUtc, DateTimeOffset RetainUntilUtc)
{
    internal const string Algorithm = "AES-256-GCM/RSA-OAEP-SHA256";
    internal const int MaximumHeaderBytes = 4096;

    internal byte[] Encode()
    {
        Validate();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("algorithm", Algorithm);
            writer.WriteString("recipientSha256", RecipientSha256);
            writer.WriteString("plaintextSha256", PlaintextSha256);
            writer.WriteNumber("plaintextBytes", PlaintextBytes);
            writer.WriteString("createdUtc", CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("retainUntilUtc", RetainUntilUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static ArchiveMetadata Decode(ReadOnlySpan<byte> header)
    {
        if (header.Length is 0 or > MaximumHeaderBytes) throw new InvalidDataException("Invalid archive metadata size.");
        using var document = JsonDocument.Parse(header.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Archive metadata must be an object.");
        var properties = root.EnumerateObject().ToArray();
        var expected = new[] { "version", "algorithm", "recipientSha256", "plaintextSha256", "plaintextBytes", "createdUtc", "retainUntilUtc" };
        if (properties.Length != expected.Length || !properties.Select(property => property.Name).SequenceEqual(expected)
            || root.GetProperty("version").GetInt32() != 1 || root.GetProperty("algorithm").GetString() != Algorithm)
            throw new InvalidDataException("Unsupported or noncanonical archive metadata.");
        var result = new ArchiveMetadata(root.GetProperty("recipientSha256").GetString()!, root.GetProperty("plaintextSha256").GetString()!,
            root.GetProperty("plaintextBytes").GetInt32(),
            DateTimeOffset.ParseExact(root.GetProperty("createdUtc").GetString()!, "O", CultureInfo.InvariantCulture),
            DateTimeOffset.ParseExact(root.GetProperty("retainUntilUtc").GetString()!, "O", CultureInfo.InvariantCulture));
        // Requiring the exact canonical bytes also rejects duplicates, alternate encodings and whitespace.
        if (!header.SequenceEqual(result.Encode())) throw new InvalidDataException("Noncanonical archive metadata.");
        return result;
    }

    private void Validate()
    {
        BackupArchive.RequireHash(RecipientSha256);
        BackupArchive.RequireHash(PlaintextSha256);
        if (PlaintextBytes is < 1 or > BackupArchive.MaximumPlaintextBytes || CreatedUtc.Offset != TimeSpan.Zero
            || RetainUntilUtc.Offset != TimeSpan.Zero || RetainUntilUtc <= CreatedUtc)
            throw new InvalidDataException("Invalid archive size, UTC timestamps or explicit retention deadline.");
    }
}
