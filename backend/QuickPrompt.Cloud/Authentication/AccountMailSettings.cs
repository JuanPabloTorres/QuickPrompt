using Microsoft.Extensions.Configuration;

namespace QuickPrompt.Cloud.Authentication;

// A normal class deliberately avoids a generated record ToString containing credentials.
internal sealed class AccountMailSettings(string apiKey, string senderEmail, string senderName, string? apiSecret = null)
{
    internal string ApiKey { get; } = apiKey;
    internal string SenderEmail { get; } = senderEmail;
    internal string SenderName { get; } = senderName;
    internal string? ApiSecret { get; } = apiSecret;

    internal static AccountMailSettings? Read(IConfiguration configuration, string provider, bool requireSecret = false)
    {
        var section = configuration.GetSection("QuickPrompt:Accounts:" + provider);
        var key = section["ApiKey"]?.Trim();
        var email = section["SenderEmail"]?.Trim();
        var name = section["SenderName"]?.Trim();
        var secret = requireSecret ? section["ApiSecret"]?.Trim() : null;
        if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(secret)) return null;
        if (key is not { Length: >= 16 and <= 1024 } || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)
            || !AccountMailContract.ValidAddress(email) || name is not { Length: > 0 and <= 100 } || name.Any(char.IsControl)
            || (requireSecret && (key.Contains(':') || secret is not { Length: >= 16 and <= 1024 }
                || secret.Any(char.IsWhiteSpace) || secret.Any(char.IsControl))))
            throw new InvalidOperationException("Configure valid account-mail credentials and sender settings together through private configuration.");
        return new(key, email!, name, secret);
    }
}
