using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Explicit Mailjet Send API transport for one account message. A receipt proves API acceptance, never inbox delivery.</summary>
public sealed class MailjetAccountMail : IAccountMail
{
    public const string ClientName = "QuickPrompt.AccountMail.Mailjet";
    private static readonly Uri Endpoint = new("https://api.mailjet.com/v3.1/send");
    private readonly IHttpClientFactory clients;
    private readonly Uri origin;
    private readonly AccountMailSettings settings;
    public bool Available => true;

    internal MailjetAccountMail(IHttpClientFactory clients, Uri origin, AccountMailSettings settings)
    {
        AccountMailContract.RequireOrigin(origin);
        if (string.IsNullOrEmpty(settings.ApiSecret)) throw new InvalidOperationException("Mailjet requires its private API secret.");
        this.clients = clients; this.origin = origin; this.settings = settings;
    }

    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var link = AccountMailContract.RequireMessage(message, origin);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(AccountMailContract.SendTimeout);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.ApiKey + ":" + settings.ApiSecret)));
        request.Headers.Accept.ParseAdd("application/json");
        // Mailjet requires PascalCase wire names; the HTTP JSON web defaults
        // would silently turn Messages/From/To into camelCase.
        request.Content = JsonContent.Create(new {
            Messages = new[] { new {
                From = new { Email = settings.SenderEmail, Name = settings.SenderName },
                To = new[] { new { Email = message.Email } },
                Subject = AccountMailContract.Subject(message.Purpose),
                TextPart = AccountMailContract.Text(message.Purpose, link)
            } }
        }, options: JsonSerializerOptions.Default);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new AccountMailDeliveryException(response.StatusCode == HttpStatusCode.TooManyRequests ? "MAIL_RATE_LIMITED"
                    : (int)response.StatusCode >= 500 ? "MAIL_UNAVAILABLE" : "MAIL_REJECTED");
            using var receipt = await AccountMailContract.ReadReceipt(response.Content, deadline.Token);
            if (!HasSingleRecipientReceipt(receipt.RootElement, message.Email))
                throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
            cancellation.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new AccountMailDeliveryException("MAIL_TIMEOUT"); }
        catch (HttpRequestException) { throw new AccountMailDeliveryException("MAIL_UNAVAILABLE"); }
        catch (IOException)
        {
            cancellation.ThrowIfCancellationRequested();
            throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        }
        catch (JsonException) { throw new AccountMailDeliveryException("MAIL_UNCONFIRMED"); }
    }

    private static bool HasSingleRecipientReceipt(JsonElement root, string recipient)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("Messages", out var messages) || messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() != 1)
            return false;
        var message = messages[0];
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != "success"
            || !message.TryGetProperty("To", out var to) || to.ValueKind != JsonValueKind.Array || to.GetArrayLength() != 1)
            return false;
        // A successful one-recipient request must not be acknowledged as a batch,
        // a partial error or delivery to additional addresses.
        foreach (var name in new[] { "Cc", "Bcc", "Errors" })
            if (message.TryGetProperty(name, out var extra) && (extra.ValueKind != JsonValueKind.Array || extra.GetArrayLength() != 0))
                return false;
        var delivered = to[0];
        return delivered.ValueKind == JsonValueKind.Object
            && delivered.TryGetProperty("Email", out var email) && email.ValueKind == JsonValueKind.String
            && string.Equals(email.GetString(), recipient, StringComparison.OrdinalIgnoreCase)
            && delivered.TryGetProperty("MessageID", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetUInt64(out var number) && number > 0
            && delivered.TryGetProperty("MessageUUID", out var uuid) && uuid.ValueKind == JsonValueKind.String
            && uuid.GetString() is { Length: > 0 and <= 512 } value && !string.IsNullOrWhiteSpace(value);
    }
}
