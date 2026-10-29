using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Provider-independent account-link and bounded-receipt rules; never generates tokens or grants account authority.</summary>
internal static class AccountMailContract
{
    internal static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    private const int MaximumReceiptBytes = 16384;

    internal static void RequireOrigin(Uri origin)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != "https" || origin.AbsolutePath != "/"
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new InvalidOperationException("External account mail requires the canonical HTTPS account origin.");
    }

    internal static string RequireMessage(AccountMail message, Uri origin)
    {
        if (message is null || message.Purpose is not ("confirm" or "reset") || !ValidAddress(message.Email)
            || message.Link is not { Length: > 0 and <= 8192 } || !Uri.TryCreate(message.Link, UriKind.Absolute, out var link)
            || link.Scheme != origin.Scheme || !string.Equals(link.Authority, origin.Authority, StringComparison.OrdinalIgnoreCase)
            || link.AbsolutePath != "/account/" + message.Purpose || link.UserInfo.Length != 0 || link.Fragment.Length != 0)
            throw new ArgumentException("El correo no corresponde a una confirmación o recuperación del origen configurado.");
        var query = QueryHelpers.ParseQuery(link.Query);
        if (query.Count != 2 || query.Keys.Any(key => key is not ("userId" or "code"))
            || !query.TryGetValue("userId", out var user) || user.Count != 1 || !Guid.TryParse(user[0], out var id) || id == Guid.Empty
            || !query.TryGetValue("code", out var code) || code.Count != 1 || code[0] is not { Length: > 0 and <= 4096 } value
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            throw new ArgumentException("El enlace del correo no tiene el contrato de cuentas esperado.");
        return link.AbsoluteUri;
    }

    internal static bool ValidAddress(string? value) => value is { Length: > 0 and <= 256 }
        && !value.Any(char.IsControl) && MailAddress.TryCreate(value, out var address)
        && string.Equals(value, address.Address, StringComparison.Ordinal);

    internal static string Subject(string purpose) => purpose == "confirm" ? "Confirma tu cuenta de QuickPrompt" : "Recupera tu acceso a QuickPrompt";

    internal static string Text(string purpose, string link) =>
        (purpose == "confirm" ? "Confirma que este correo te pertenece para activar tu cuenta de QuickPrompt.\n\n"
            : "Para elegir una nueva contraseña de QuickPrompt, abre este enlace.\n\n")
        + link + "\n\nSi no solicitaste esta acción, ignora este mensaje.";

    internal static async Task<JsonDocument> ReadReceipt(HttpContent content, CancellationToken cancellation)
    {
        if (content.Headers.ContentLength > MaximumReceiptBytes) throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        await using var stream = await content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var block = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(block, cancellation)) != 0)
        {
            if (buffer.Length + read > MaximumReceiptBytes) throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
            buffer.Write(block, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}
