using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Safe diagnostic only. An unconfirmed send may already have reached the provider; never retry it implicitly.</summary>
public sealed class AccountMailDeliveryException(string code) : Exception("No se pudo confirmar el envío del correo de cuentas. No se reintentará automáticamente.")
{
    public string Code { get; } = code;
}

/// <summary>One fixed HTTPS API, one recipient, only account confirmation/reset. API acceptance is not inbox delivery.</summary>
public sealed class BrevoAccountMail : IAccountMail
{
    public const string ClientName = "QuickPrompt.AccountMail.Brevo";
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    private static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");
    private readonly IHttpClientFactory clients;
    private readonly Uri origin;
    private readonly BrevoAccountMailSettings settings;
    public bool Available => true;

    internal BrevoAccountMail(IHttpClientFactory clients, Uri origin, BrevoAccountMailSettings settings)
    { RequireOrigin(origin); this.clients = clients; this.origin = origin; this.settings = settings; }

    internal static void RequireOrigin(Uri origin)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != "https" || origin.AbsolutePath != "/"
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new InvalidOperationException("External account mail requires the canonical HTTPS account origin.");
    }

    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var link = RequireMessage(message);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(SendTimeout);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("api-key", settings.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(new {
            sender = new { email = settings.SenderEmail, name = settings.SenderName },
            to = new[] { new { email = message.Email } },
            subject = message.Purpose == "confirm" ? "Confirma tu cuenta de QuickPrompt" : "Recupera tu acceso a QuickPrompt",
            textContent = (message.Purpose == "confirm" ? "Confirma que este correo te pertenece para activar tu cuenta de QuickPrompt.\n\n" : "Para elegir una nueva contraseña de QuickPrompt, abre este enlace.\n\n")
                + link + "\n\nSi no solicitaste esta acción, ignora este mensaje."
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.Created)
                throw new AccountMailDeliveryException(response.StatusCode == HttpStatusCode.TooManyRequests ? "MAIL_RATE_LIMITED"
                    : (int)response.StatusCode >= 500 ? "MAIL_UNAVAILABLE" : "MAIL_REJECTED");
            await RequireAcceptedMessage(response.Content, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new AccountMailDeliveryException("MAIL_TIMEOUT"); }
        catch (HttpRequestException) { throw new AccountMailDeliveryException("MAIL_UNAVAILABLE"); }
        catch (JsonException) { throw new AccountMailDeliveryException("MAIL_UNCONFIRMED"); }
    }

    private string RequireMessage(AccountMail message)
    {
        if (message is null || message.Purpose is not ("confirm" or "reset") || !BrevoAccountMailSettings.ValidAddress(message.Email)
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

    private static async Task RequireAcceptedMessage(HttpContent content, CancellationToken cancellation)
    {
        // A response receipt is tiny; bound it even if a faulty provider advertises no length.
        const int maximumBytes = 16384;
        if (content.Headers.ContentLength > maximumBytes) throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        await using var stream = await content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var block = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(block, cancellation)) != 0)
        {
            if (buffer.Length + read > maximumBytes) throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
            buffer.Write(block, 0, read);
        }
        using var receipt = JsonDocument.Parse(buffer.ToArray());
        if (receipt.RootElement.ValueKind != JsonValueKind.Object || !receipt.RootElement.TryGetProperty("messageId", out var messageId)
            || messageId.ValueKind != JsonValueKind.String || messageId.GetString() is not { Length: > 0 and <= 512 } id || string.IsNullOrWhiteSpace(id))
            throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        cancellation.ThrowIfCancellationRequested();
    }
}

// Ordinary class deliberately avoids a generated record ToString containing credentials.
internal sealed class BrevoAccountMailSettings(string apiKey, string senderEmail, string senderName)
{
    internal string ApiKey { get; } = apiKey;
    internal string SenderEmail { get; } = senderEmail;
    internal string SenderName { get; } = senderName;
    internal static BrevoAccountMailSettings? Read(IConfiguration configuration)
    {
        var key = configuration["QuickPrompt:Accounts:Brevo:ApiKey"]?.Trim();
        var email = configuration["QuickPrompt:Accounts:Brevo:SenderEmail"]?.Trim();
        var name = configuration["QuickPrompt:Accounts:Brevo:SenderName"]?.Trim();
        if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(name)) return null;
        if (key is not { Length: >= 16 and <= 1024 } || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)
            || !ValidAddress(email) || name is not { Length: > 0 and <= 100 } || name.Any(char.IsControl))
            throw new InvalidOperationException("Configure a valid Brevo ApiKey, SenderEmail and SenderName together through private configuration.");
        return new(key, email!, name);
    }
    internal static bool ValidAddress(string? value) => value is { Length: > 0 and <= 256 }
        && !value.Any(char.IsControl) && MailAddress.TryCreate(value, out var address)
        && string.Equals(value, address.Address, StringComparison.Ordinal);
}
