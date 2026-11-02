using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Explicit machine credentials for the operator's Brevo account, never a user's login or a refresh-token grant.</summary>
internal sealed class BrevoClientCredentials(string clientId, string clientSecret, string senderEmail, string senderName)
{
    internal const string Scope = "transactional.email:write";
    private static readonly Uri TokenEndpoint = new("https://oauth.brevo.com/realms/partner/oauth/token");
    internal string SenderEmail { get; } = senderEmail;
    internal string SenderName { get; } = senderName;

    // A class rather than a record keeps secrets out of generated ToString output.
    internal static (bool Selected, BrevoClientCredentials? Settings) Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("QuickPrompt:Accounts:Brevo");
        var mode = section["AuthMode"]?.Trim();
        var id = section["ClientId"]?.Trim();
        var secret = section["ClientSecret"];
        var apiKey = section["ApiKey"]?.Trim();
        if (mode is not (null or "" or "ApiKey" or "ClientCredentials")) throw InvalidConfiguration();
        if (mode != "ClientCredentials")
        {
            if (!string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(secret)) throw InvalidConfiguration();
            return (false, null);
        }
        if (!string.IsNullOrEmpty(apiKey)) throw InvalidConfiguration();
        var email = section["SenderEmail"]?.Trim();
        var name = section["SenderName"]?.Trim();
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(secret) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(name))
            return (true, null);
        if (id is not { Length: > 0 and <= 1024 } || id.Any(char.IsControl) || id.Any(char.IsWhiteSpace)
            || secret is not { Length: > 0 and <= 4096 } || secret.Any(char.IsControl)
            || !AccountMailContract.ValidSender(email, name)) throw InvalidConfiguration();
        return (true, new(id, secret, email!, name!));
    }

    /// <summary>One exchange per send, with the caller's shared deadline. Tokens stay in memory and are never retried or cached globally.</summary>
    internal async Task<string> Token(HttpClient client, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "client_credentials", ["client_id"] = clientId,
                ["client_secret"] = clientSecret, ["scope"] = Scope
            })
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new AccountMailDeliveryException(response.StatusCode == HttpStatusCode.TooManyRequests ? "MAIL_RATE_LIMITED"
                : (int)response.StatusCode >= 500 ? "MAIL_UNAVAILABLE" : "MAIL_REJECTED");
        using var receipt = await AccountMailContract.ReadReceipt(response.Content, cancellation);
        var body = receipt.RootElement;
        if (body.ValueKind != JsonValueKind.Object
            || body.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != body.EnumerateObject().Count()
            || !body.TryGetProperty("token_type", out var type) || type.ValueKind != JsonValueKind.String
            || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)
            // RFC 6749 section 5.1 allows omission only when scope equals
            // the explicit request. A different or broader returned scope fails.
            || body.TryGetProperty("scope", out var scope) && (scope.ValueKind != JsonValueKind.String || scope.GetString() != Scope)
            || !body.TryGetProperty("expires_in", out var expires) || expires.ValueKind != JsonValueKind.Number
            || !expires.TryGetInt32(out var seconds) || seconds is < 1 or > 3600
            || !body.TryGetProperty("access_token", out var value) || value.ValueKind != JsonValueKind.String
            || value.GetString() is not { Length: > 0 and <= 8192 } token
            || token.Any(character => !char.IsAsciiLetterOrDigit(character) && !"-._~+/=".Contains(character)))
            throw new AccountMailDeliveryException("MAIL_UNCONFIRMED");
        cancellation.ThrowIfCancellationRequested();
        return token;
    }

    private static InvalidOperationException InvalidConfiguration() =>
        new("Choose one explicit Brevo authentication mode and configure its complete private credentials and sender.");
}
