using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
    public static readonly TimeSpan SendTimeout = AccountMailContract.SendTimeout;
    private static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");
    private readonly IHttpClientFactory clients;
    private readonly Uri origin;
    private readonly AccountMailSettings settings;
    public bool Available => true;

    internal BrevoAccountMail(IHttpClientFactory clients, Uri origin, AccountMailSettings settings)
    { RequireOrigin(origin); this.clients = clients; this.origin = origin; this.settings = settings; }

    internal static void RequireOrigin(Uri origin) => AccountMailContract.RequireOrigin(origin);

    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var link = AccountMailContract.RequireMessage(message, origin);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(SendTimeout);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("api-key", settings.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(new {
            sender = new { email = settings.SenderEmail, name = settings.SenderName },
            to = new[] { new { email = message.Email } },
            subject = AccountMailContract.Subject(message.Purpose),
            textContent = AccountMailContract.Text(message.Purpose, link)
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

    private static async Task RequireAcceptedMessage(HttpContent content, CancellationToken cancellation)
    {
        using var receipt = await AccountMailContract.ReadReceipt(content, cancellation);
        if (receipt.RootElement.ValueKind != JsonValueKind.Object || !receipt.RootElement.TryGetProperty("messageId", out var messageId)
            || messageId.ValueKind != JsonValueKind.String || messageId.GetString() is not { Length: > 0 and <= 512 } id || string.IsNullOrWhiteSpace(id))
            throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        cancellation.ThrowIfCancellationRequested();
    }
}
