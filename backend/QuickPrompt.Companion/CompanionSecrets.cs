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
    private string FilePath => Path.Combine(directory, "session.dpapi");
    public CompanionSecrets(CompanionOptions options)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("El almacenamiento privado del companion requiere Windows.");
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
        var temporary = Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            var encrypted = ProtectedData.Protect(clear, purpose, DataProtectionScope.CurrentUser);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(encrypted); file.Flush(flushToDisk: true); }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(clear); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Forget() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
