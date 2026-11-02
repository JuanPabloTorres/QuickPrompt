using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Server-only EmailJS transport using private authorization and fixed account templates. Acceptance is not inbox delivery.</summary>
public sealed class EmailJsAccountMail : IAccountMail
{
    public const string ClientName = "QuickPrompt.AccountMail.EmailJs";
    private static readonly Uri Endpoint = new("https://api.emailjs.com/api/v1.0/email/send");
    private readonly IHttpClientFactory clients;
    private readonly Uri origin;
    private readonly EmailJsMailSettings settings;
    public bool Available => true;

    internal EmailJsAccountMail(IHttpClientFactory clients, Uri origin, EmailJsMailSettings settings)
    {
        AccountMailContract.RequireOrigin(origin);
        this.clients = clients;
        this.origin = origin;
        this.settings = settings;
    }

    public async Task Send(AccountMail message, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var link = AccountMailContract.RequireMessage(message, origin);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(AccountMailContract.SendTimeout);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) {
            Content = JsonContent.Create(new {
                service_id = settings.ServiceId,
                template_id = message.Purpose == "confirm" ? settings.ConfirmTemplate : settings.ResetTemplate,
                user_id = settings.PublicKey,
                accessToken = settings.PrivateKey,
                template_params = new { to_email = message.Email, action_url = link }
            })
        };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new AccountMailDeliveryException(response.StatusCode == HttpStatusCode.TooManyRequests ? "MAIL_RATE_LIMITED"
                    : (int)response.StatusCode >= 500 ? "MAIL_UNAVAILABLE" : "MAIL_REJECTED");
            var receipt = Encoding.UTF8.GetString(await AccountMailContract.ReadReceiptBytes(response.Content, deadline.Token)).Trim();
            // The documented success is text OK (or a JSON string), never an arbitrary 2xx body.
            if (receipt is not ("OK" or "\"OK\"")) throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
            cancellation.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new AccountMailDeliveryException("MAIL_TIMEOUT"); }
        catch (HttpRequestException) { throw new AccountMailDeliveryException("MAIL_UNAVAILABLE"); }
    }
}
