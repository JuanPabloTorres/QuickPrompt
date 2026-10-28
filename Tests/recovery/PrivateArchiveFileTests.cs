using System.Security.AccessControl;
using System.Security.Principal;
using QuickPrompt.Recovery;
using Xunit;

namespace QuickPrompt.Recovery.Tests;

/// <summary>Only fixed synthetic bytes in an owned temporary directory. No certificate/private-key/password files.</summary>
public sealed class PrivateArchiveFileTests
{
    [Theory]
    [InlineData(DriveType.Network)]
    [InlineData(DriveType.Unknown)]
    [InlineData(DriveType.NoRootDirectory)]
    public void RemoteAndUnclassifiedVolumesAreRejectedWithoutOpeningThem(DriveType type) =>
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequireLocalDrive(type));

    [Theory]
    [InlineData(DriveType.Fixed)]
    [InlineData(DriveType.Removable)]
    [InlineData(DriveType.Ram)]
    [InlineData(DriveType.CDRom)]
    public void KnownLocalVolumeKindsPassClassification(DriveType type) => PrivateArchiveFiles.RequireLocalDrive(type);

    [Fact]
    public void NewOutputHasPrivatePermissionsAndNeverOverwrites()
    {
        var fixture = Path.Combine(Path.GetTempPath(), "quickprompt-recovery-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var file = Path.Combine(fixture, "synthetic.bin");
        try
        {
            var repository = Path.Combine(fixture, "repository");
            PrivateArchiveFiles.WriteNew(file, repository, "Synthetic nonsecret file-permission probe"u8);
            Assert.Throws<IOException>(() => PrivateArchiveFiles.WriteNew(file, repository, "Replacement"u8));
            Assert.Equal("Synthetic nonsecret file-permission probe"u8.ToArray(), PrivateArchiveFiles.ReadBounded(file, repository, 128));
            Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.ReadBounded(file, repository, 2));
            if (OperatingSystem.IsWindows())
            {
                var security = new FileInfo(file).GetAccessControl();
                Assert.True(security.AreAccessRulesProtected);
                var current = WindowsIdentity.GetCurrent().User!;
                var allowed = new[] { current.Value, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value };
                var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
                Assert.Equal(2, rules.Length);
                foreach (var rule in rules)
                {
                    Assert.Contains(rule.IdentityReference.Value, allowed);
                    Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                }
            }
            else Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
        finally
        {
            // Exact owned leaf only; Directory.Delete is nonrecursive and refuses leftover files.
            if (File.Exists(file)) File.Delete(file);
            Directory.Delete(fixture);
        }
    }

    [Fact]
    public void RepositoryRelativeAndNetworkPathsAreRejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "synthetic-repository"));
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequirePath(Path.Combine(root, "file.dump"), root, false));
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequirePath("relative.dump", root, false));
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequirePath(@"\\server\share\file.dump", root, false));
    }

    [Fact]
    public void RepositoryArgumentUsesTheSameLocalPathBoundary()
    {
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequireRepositoryRoot(@"\\server\share\repository"));
        Assert.Throws<InvalidDataException>(() => PrivateArchiveFiles.RequireRepositoryRoot("relative-repository"));
        Assert.True(Directory.Exists(PrivateArchiveFiles.RequireRepositoryRoot(Path.GetFullPath(Path.GetTempPath()))));
    }
}
