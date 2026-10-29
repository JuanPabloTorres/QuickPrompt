using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Companion;

public sealed record CompanionSession(string Origin, Guid ConnectionId, string RefreshToken, string ProfileId = LocalWorkProtocol.Profile);

/// <summary>DPAPI CurrentUser, origin/slot purpose, private directory and atomic replacement. No plaintext credentials on disk.</summary>
public sealed class CompanionSecrets
{
    private readonly string directory;
    private readonly byte[] purpose;
    private readonly string profile;
    private string FilePath => Path.Combine(directory, "session.dpapi");
    private string DiagnosticPath => Path.Combine(directory, "last-run.json");
    public CompanionSecrets(CompanionOptions options)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("El almacenamiento privado del companion requiere Windows.");
        profile = options.Profile;
        purpose = SHA256.HashData(Encoding.UTF8.GetBytes("QuickPrompt.Companion/1|" + options.Origin.AbsoluteUri + "|" + options.Slot));
        directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion", Convert.ToHexString(purpose));
        Directory.CreateDirectory(directory);
        if (new DirectoryInfo(directory).LinkTarget is not null) throw new IOException("El directorio de credenciales no admite enlaces.");
        var identity = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
        var permissions = new DirectorySecurity(); permissions.SetAccessRuleProtection(true, false); permissions.SetOwner(identity);
        permissions.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(permissions);
    }
    public IDisposable Acquire() => new FileStream(Path.Combine(directory, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public bool Exists => File.Exists(FilePath);
    public CompanionSession Load(string origin, string profile = LocalWorkProtocol.Profile)
    {
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), purpose, DataProtectionScope.CurrentUser);
        try
        {
            var session = JsonSerializer.Deserialize<CompanionSession>(clear) ?? throw new CryptographicException();
            if (session.Origin != origin || session.ConnectionId == Guid.Empty || string.IsNullOrWhiteSpace(session.RefreshToken)) throw new CryptographicException();
            if (session.ProfileId != profile) throw new InvalidOperationException("El perfil guardado tiene otra capacidad. Usa su capacidad original para desconectarlo o elige otro slot y apruébalo.");
            return session;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Save(CompanionSession session)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(session);
        try
        {
            var encrypted = ProtectedData.Protect(clear, purpose, DataProtectionScope.CurrentUser);
            WritePrivate(FilePath, encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public void WriteRunDiagnostic(CompanionRunDiagnostic diagnostic)
    {
        if (!CompanionRunDiagnostics.Valid(diagnostic) || diagnostic.ProfileId != profile) throw new ArgumentException("Diagnóstico local inválido.");
        WritePrivate(DiagnosticPath, JsonSerializer.SerializeToUtf8Bytes(diagnostic, CompanionRunDiagnostics.Json));
    }

    public CompanionRunDiagnostic? ReadRunDiagnostic()
    {
        try
        {
            var file = new FileInfo(DiagnosticPath);
            if (!file.Exists || file.Length is < 1 or > 8192 || file.LinkTarget is not null) return null;
            var value = JsonSerializer.Deserialize<CompanionRunDiagnostic>(File.ReadAllBytes(DiagnosticPath), CompanionRunDiagnostics.Json);
            return value is not null && CompanionRunDiagnostics.Valid(value) && value.ProfileId == profile ? value : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private void WritePrivate(string destination, byte[] content)
    {
        var temporary = Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(content); file.Flush(flushToDisk: true); }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Forget() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
