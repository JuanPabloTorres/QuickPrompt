using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using QuickPrompt.Recovery;

// Operator-only executable. No HTTP endpoint, database connection, upload, scheduler, key generation or implicit fallback.
if (args.Length == 1 && args[0] == "--help")
{
    Console.WriteLine("protect --repository-root ABS --input ABS.dump --output NEW_ABS.qpb --plaintext-sha256 HEX --recipient-cert ABS_PUBLIC_CERT --recipient-sha256 HEX --retain-until UTC");
    Console.WriteLine("recover --repository-root ABS --input ABS.qpb --output NEW_ABS.dump --archive-sha256 HEX --plaintext-sha256 HEX --recipient-pfx ABS_EXISTING_PFX --recipient-sha256 HEX");
    Console.WriteLine("verify --repository-root ABS --input ABS.qpb --archive-sha256 HEX --plaintext-sha256 HEX --recipient-pfx ABS_EXISTING_PFX --recipient-sha256 HEX");
    Console.WriteLine("64 MiB pilot limit. Dedicated document-encryption recipient only. Recovery requires an interactive password without echo; no password argument, environment variable or temporary secret file.");
    return 0;
}
byte[]? input = null, output = null;
try
{
    var command = RecoveryCommand.Parse(args);
    command.RequireInteractiveInput(Console.IsInputRedirected);
    var mode = command.Mode;
    var options = command.Options;
    var root = PrivateArchiveFiles.RequireRepositoryRoot(options["--repository-root"]);
    string? destination = null;
    if (mode != "verify")
    {
        destination = PrivateArchiveFiles.RequirePath(options["--output"], root, mustExist: false);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("The output already exists; it will not be overwritten.");
    }
    input = PrivateArchiveFiles.ReadBounded(options["--input"], root, mode == "protect" ? BackupArchive.MaximumPlaintextBytes : BackupArchive.MaximumArchiveBytes);
    if (mode == "protect")
    {
        if (!input.AsSpan().StartsWith("PGDMP"u8)) throw new InvalidDataException("Use a PostgreSQL custom-format backup.");
        var publicPath = PrivateArchiveFiles.RequirePath(options["--recipient-cert"], root, mustExist: true);
        using var certificate = X509CertificateLoader.LoadCertificate(PrivateArchiveFiles.ReadBounded(publicPath, root, 64 * 1024));
        var until = DateTimeOffset.Parse(options["--retain-until"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        output = BackupArchive.Protect(input, options["--plaintext-sha256"], certificate, options["--recipient-sha256"], DateTimeOffset.UtcNow, until);
    }
    else
    {
        var privatePath = PrivateArchiveFiles.RequirePath(options["--recipient-pfx"], root, mustExist: true);
        var password = ReadPassword();
        byte[]? encryptedPfx = null;
        try
        {
            // Revalidate and read once after the human pause. Loading the same bounded
            // bytes prevents a second, unbounded file open after an earlier size check.
            encryptedPfx = PrivateArchiveFiles.ReadBounded(privatePath, root, 64 * 1024);
            using var certificate = X509CertificateLoader.LoadPkcs12(encryptedPfx, password.AsSpan(), X509KeyStorageFlags.EphemeralKeySet);
            if (mode == "verify")
            {
                var verifiedBytes = RecoveryVerification.Verify(input, options["--archive-sha256"], options["--plaintext-sha256"], certificate,
                    options["--recipient-sha256"], DateTimeOffset.UtcNow);
                Console.WriteLine(JsonSerializer.Serialize(new { status = "completed", operation = mode, inputSha256 = BackupArchive.Digest(input),
                    plaintextSha256 = options["--plaintext-sha256"], plaintextBytes = verifiedBytes, recipientSha256 = options["--recipient-sha256"],
                    outputWritten = false, databaseRestored = false, published = false, retentionDeletionPerformed = false }));
                return 0;
            }
            output = BackupArchive.Recover(input, options["--archive-sha256"], options["--plaintext-sha256"], certificate,
                options["--recipient-sha256"], DateTimeOffset.UtcNow);
        }
        finally
        {
            if (encryptedPfx is not null) CryptographicOperations.ZeroMemory(encryptedPfx);
            Array.Clear(password);
        }
        if (!output.AsSpan().StartsWith("PGDMP"u8)) throw new InvalidDataException("The recovered content is not a PostgreSQL custom-format backup.");
    }
    PrivateArchiveFiles.WriteNew(destination!, root, output);
    Console.WriteLine(JsonSerializer.Serialize(new { status = "completed", operation = mode, inputSha256 = BackupArchive.Digest(input),
        outputSha256 = BackupArchive.Digest(output), outputBytes = output.Length, databaseRestored = false, published = false,
        recipientSha256 = options["--recipient-sha256"], retentionDeadlineUtc = mode == "protect" ? options["--retain-until"] : null,
        erasureAndRevocationReconciliation = "REQUIRED_BEFORE_DATABASE_REOPEN", retentionDeletionPerformed = false }));
    return 0;
}
catch (Exception error) when (error is InvalidDataException or CryptographicException or IOException or UnauthorizedAccessException
    or FormatException or InvalidOperationException or JsonException or ArgumentException)
{
    // Fixed diagnostics only. Do not project paths, passwords, certificate contents or backup data.
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "failed", errorType = error.GetType().Name, databaseRestored = false, published = false }));
    return 1;
}
finally
{
    if (input is not null) CryptographicOperations.ZeroMemory(input);
    if (output is not null) CryptographicOperations.ZeroMemory(output);
}

static char[] ReadPassword()
{
    Console.Error.Write("Existing dedicated backup PFX password: ");
    var buffer = new char[4096];
    var length = 0;
    try
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (length > 0) buffer[--length] = '\0'; continue; }
            if (char.IsControl(key.KeyChar)) continue;
            if (length == buffer.Length) throw new InvalidDataException("The password exceeds the supported input size.");
            buffer[length++] = key.KeyChar;
        }
        Console.Error.WriteLine();
        return buffer[..length];
    }
    finally { Array.Clear(buffer); }
}
