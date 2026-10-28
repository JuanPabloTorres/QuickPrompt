using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace QuickPrompt.Cloud.Authentication;

public sealed record AccountMail(string Email, string Purpose, string Link);
public interface IAccountMail
{
    bool Available { get; }
    Task Send(AccountMail message, CancellationToken cancellation);
}

public sealed class UnconfiguredAccountMail : IAccountMail
{
    public bool Available => false;
    public Task Send(AccountMail message, CancellationToken cancellation) => throw new InvalidOperationException("Configure account mail before registering users.");
}

/// <summary>Development-only private pickup; never delivers external email or prints confirmation/reset tokens.</summary>
public sealed class DevelopmentAccountMail : IAccountMail
{
    private readonly string directory;
    public bool Available => true;
    public DevelopmentAccountMail()
    {
        directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "saas-development", "mail");
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows())
        {
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!, new SecurityIdentifier("S-1-5-18") })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(acl);
        }
        else File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        // Recipient namespace lets an operator inspect only a synthetic QA mailbox,
        // without scanning unrelated confirmation/reset messages.
        var mailbox = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Email.Trim().ToUpperInvariant()))).ToLowerInvariant());
        Directory.CreateDirectory(mailbox);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(mailbox, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(mailbox, Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(message), cancellation);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
