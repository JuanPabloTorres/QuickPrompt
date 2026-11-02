using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Explicit opt-in HTTPS account mail. One recipient and one attempt; API acceptance does not prove inbox delivery.</summary>
public sealed class Smtp2GoAccountMail : IAccountMail
{
    public const string ClientName = "QuickPrompt.AccountMail.Smtp2Go";
    private static readonly Uri Endpoint = new("https://api.smtp2go.com/v3/email/send");
    private readonly IHttpClientFactory clients;
    private readonly Uri origin;
    private readonly AccountMailSettings settings;
    public bool Available => true;

    internal Smtp2GoAccountMail(IHttpClientFactory clients, Uri origin, AccountMailSettings settings)
    { AccountMailContract.RequireOrigin(origin); this.clients = clients; this.origin = origin; this.settings = settings; }

    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var link = AccountMailContract.RequireMessage(message, origin);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(AccountMailContract.SendTimeout);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("X-Smtp2go-Api-Key", settings.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(new {
            sender = new MailAddress(settings.SenderEmail, settings.SenderName).ToString(),
            to = new[] { message.Email },
            subject = AccountMailContract.Subject(message.Purpose),
            text_body = AccountMailContract.Text(message.Purpose, link)
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new AccountMailDeliveryException(response.StatusCode == HttpStatusCode.TooManyRequests ? "MAIL_RATE_LIMITED"
                    : (int)response.StatusCode >= 500 ? "MAIL_UNAVAILABLE" : "MAIL_REJECTED");
            using var receipt = await AccountMailContract.ReadReceipt(response.Content, deadline.Token);
            if (receipt.RootElement.ValueKind != JsonValueKind.Object
                || !receipt.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("succeeded", out var succeeded) || succeeded.ValueKind != JsonValueKind.Number || !succeeded.TryGetInt32(out var count) || count != 1
                || !data.TryGetProperty("failed", out var failed) || failed.ValueKind != JsonValueKind.Number || !failed.TryGetInt32(out var failedCount) || failedCount != 0
                || !data.TryGetProperty("failures", out var failures) || failures.ValueKind != JsonValueKind.Array || failures.GetArrayLength() != 0
                || !data.TryGetProperty("email_id", out var id) || id.ValueKind != JsonValueKind.String
                || id.GetString() is not { Length: > 0 and <= 512 } value || string.IsNullOrWhiteSpace(value))
                throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
            cancellation.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new AccountMailDeliveryException("MAIL_TIMEOUT"); }
        catch (HttpRequestException) { throw new AccountMailDeliveryException("MAIL_UNAVAILABLE"); }
        catch (JsonException) { throw new AccountMailDeliveryException("MAIL_UNCONFIRMED"); }
    }
}
