using Microsoft.Extensions.Configuration;

namespace QuickPrompt.Cloud.Authentication;

// A normal class deliberately avoids a generated record ToString containing credentials.
internal sealed class AccountMailSettings(string apiKey, string senderEmail, string senderName)
{
    internal string ApiKey { get; } = apiKey;
    internal string SenderEmail { get; } = senderEmail;
    internal string SenderName { get; } = senderName;

    internal static AccountMailSettings? Read(IConfiguration configuration, string provider)
    {
        var section = configuration.GetSection("QuickPrompt:Accounts:" + provider);
        var key = section["ApiKey"]?.Trim();
        var email = section["SenderEmail"]?.Trim();
        var name = section["SenderName"]?.Trim();
        if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(name)) return null;
        if (key is not { Length: >= 16 and <= 1024 } || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)
            || !AccountMailContract.ValidAddress(email) || name is not { Length: > 0 and <= 100 } || name.Any(char.IsControl))
            throw new InvalidOperationException("Configure a valid account-mail ApiKey, SenderEmail and SenderName together through private configuration.");
        return new(key, email!, name);
    }
}
