using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

[assembly: InternalsVisibleTo("QuickPrompt.Recovery.Tests")]

namespace QuickPrompt.Recovery;

/// <summary>Local, physical paths only. Output permissions exist before the first byte; no overwrite, remote destination or cleanup.</summary>
public static class PrivateArchiveFiles
{
    public static string RequirePath(string path, string repositoryRoot, bool mustExist)
    {
        var full = NormalizeLocalPath(path);
        var root = Path.TrimEndingDirectorySeparator(NormalizeLocalPath(repositoryRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (full.Equals(root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Private recovery files must remain outside the repository.");
        if (!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("The chosen private directory must already exist.");
        if (mustExist && !File.Exists(full)) throw new FileNotFoundException("The chosen recovery input is missing.");
        return full;
    }

    public static string RequireRepositoryRoot(string path)
    {
        var root = NormalizeLocalPath(path);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The explicit repository root must exist.");
        return root;
    }

    private static string NormalizeLocalPath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidDataException("Use an absolute local file path, never a network share or device path.");
        var full = Path.GetFullPath(path);
        // A mapped network drive has neither a UNC prefix nor a reparse point. Apply
        // this same rule to every path, including the repository argument, before I/O.
        RequireLocalDrive(new DriveInfo(Path.GetPathRoot(full)!).DriveType);
        var ancestors = new Stack<string>();
        for (var cursor = full; !string.IsNullOrEmpty(cursor); cursor = Path.GetDirectoryName(cursor)) ancestors.Push(cursor);
        // Walk outward-to-inward without Exists (which can follow a junction). A parent
        // link is rejected before probing a child through it. This is not a filesystem sandbox.
        while (ancestors.TryPop(out var cursor))
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(cursor); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { continue; }
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Recovery paths cannot contain links or junctions.");
        }
        return full;
    }

    internal static void RequireLocalDrive(DriveType type)
    {
        if (type is not (DriveType.Fixed or DriveType.Removable or DriveType.Ram or DriveType.CDRom))
            throw new InvalidDataException("Network, unknown or unavailable volumes are not approved local recovery paths.");
    }

    public static byte[] ReadBounded(string path, string repositoryRoot, int maximumBytes)
    {
        using var stream = new FileStream(RequirePath(path, repositoryRoot, mustExist: true), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 || stream.Length > maximumBytes) throw new InvalidDataException("The input is empty or exceeds the supported size.");
        var bytes = new byte[checked((int)stream.Length)];
        try
        {
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw new InvalidDataException("The input changed during reading.");
            return bytes;
        }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    public static void WriteNew(string path, string repositoryRoot, ReadOnlySpan<byte> bytes)
    {
        var destination = RequirePath(path, repositoryRoot, mustExist: false);
        using var output = CreatePrivate(destination);
        output.Write(bytes);
        output.Flush(flushToDisk: true);
    }

    private static FileStream CreatePrivate(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var current = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("The current Windows identity is unavailable.");
            foreach (var sid in new[] { current, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            return FileSystemAclExtensions.Create(new FileInfo(path), FileMode.CreateNew, FileSystemRights.FullControl,
                FileShare.None, 4096, FileOptions.WriteThrough, security);
        }
        return new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, Options = FileOptions.WriteThrough, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
    }
}
