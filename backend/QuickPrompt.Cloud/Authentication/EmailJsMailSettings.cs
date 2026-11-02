using Microsoft.Extensions.Configuration;

namespace QuickPrompt.Cloud.Authentication;

// Avoid record-generated diagnostics that would include either API credential.
internal sealed class EmailJsMailSettings(string publicKey, string privateKey, string serviceId, string confirmTemplate, string resetTemplate)
{
    internal string PublicKey { get; } = publicKey;
    internal string PrivateKey { get; } = privateKey;
    internal string ServiceId { get; } = serviceId;
    internal string ConfirmTemplate { get; } = confirmTemplate;
    internal string ResetTemplate { get; } = resetTemplate;

    internal static EmailJsMailSettings? Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("QuickPrompt:Accounts:EmailJs");
        var publicKey = section["PublicKey"]?.Trim();
        var privateKey = section["PrivateKey"]?.Trim();
        var service = section["ServiceId"]?.Trim();
        var confirm = section["ConfirmTemplate"]?.Trim();
        var reset = section["ResetTemplate"]?.Trim();
        if (new[] { publicKey, privateKey, service, confirm, reset }.All(string.IsNullOrEmpty)) return null;
        if (!ValidKey(publicKey) || !ValidKey(privateKey) || !ValidId(service) || !ValidId(confirm) || !ValidId(reset) || confirm == reset)
            throw new InvalidOperationException("Configure EmailJS private credentials, service and distinct account templates together through private configuration.");
        return new(publicKey!, privateKey!, service!, confirm!, reset!);
    }

    private static bool ValidKey(string? value) => value is { Length: >= 16 and <= 1024 }
        && !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));

    private static bool ValidId(string? value) => value is { Length: > 0 and <= 100 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
