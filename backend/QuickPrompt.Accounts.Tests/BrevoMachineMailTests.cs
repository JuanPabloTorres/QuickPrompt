using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Controlled OAuth and mail HTTP, without account credentials or external delivery.</summary>
public sealed class BrevoMachineMailTests
{
    private const string ClientId = "synthetic-client-id";
    private const string Secret = "synthetic+secret / not-real=";
    private const string Token = "synthetic.machine.token";
    private const string Scope = "transactional.email:write";
    private const string TokenUri = "https://oauth.brevo.com/realms/partner/oauth/token";
    private const string MailUri = "https://api.brevo.com/v3/smtp/email";
    private const string Receipt = "{\"access_token\":\"synthetic.machine.token\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"transactional.email:write\"}";

    private static Dictionary<string, string?> Settings() => new() {
        ["QuickPrompt:Accounts:MailProvider"] = "Brevo",
        ["QuickPrompt:Accounts:Brevo:AuthMode"] = "ClientCredentials",
        ["QuickPrompt:Accounts:Brevo:ClientId"] = ClientId,
        ["QuickPrompt:Accounts:Brevo:ClientSecret"] = Secret,
        ["QuickPrompt:Accounts:Brevo:SenderEmail"] = "sender@example.invalid",
        ["QuickPrompt:Accounts:Brevo:SenderName"] = "QuickPrompt"
    };

    [Theory]
    [InlineData("confirm")]
    [InlineData("reset")]
    public async Task AScopedTokenAuthenticatesOneCanonicalSendWithoutApiKeys(string purpose)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        var mail = services.GetRequiredService<IAccountMail>();
        Assert.True(mail.Available); Assert.IsType<BrevoAccountMail>(mail);
        await mail.Send(Message(purpose), default);
        var requests = handler.Requests.ToArray(); Assert.Equal(2, requests.Length);
        var token = requests[0]; Assert.Equal(TokenUri, token.Uri); Assert.Equal("POST", token.Method);
        Assert.Equal("application/x-www-form-urlencoded", token.ContentType); Assert.Null(token.Authorization); Assert.False(token.ApiKeyPresent);
        var form = QueryHelpers.ParseQuery(token.Body);
        Assert.Equal(new[] { "client_id", "client_secret", "grant_type", "scope" }, form.Keys.Order());
        Assert.Equal(ClientId, form["client_id"]); Assert.Equal(Secret, form["client_secret"]);
        Assert.Equal("client_credentials", form["grant_type"]); Assert.Equal(Scope, form["scope"]);
        var send = requests[1]; Assert.Equal(MailUri, send.Uri); Assert.Equal("POST", send.Method); Assert.Equal("application/json", send.ContentType);
        Assert.Equal("Bearer " + Token, send.Authorization); Assert.False(send.ApiKeyPresent);
        Assert.DoesNotContain(Secret, send.Body);
        using var body = JsonDocument.Parse(send.Body);
        Assert.Equal("recipient@example.invalid", Assert.Single(body.RootElement.GetProperty("to").EnumerateArray()).GetProperty("email").GetString());
        Assert.Contains(Message(purpose).Link, body.RootElement.GetProperty("textContent").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Basic\",\"expires_in\":3600,\"scope\":\"transactional.email:write\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":\"3600\",\"scope\":\"transactional.email:write\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":0,\"scope\":\"transactional.email:write\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":3601,\"scope\":\"transactional.email:write\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"all\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"transactional.email:write contacts:write\"}")]
    [InlineData("{\"access_token\":\"bad\\r\\nheader\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"transactional.email:write\"}")]
    [InlineData("{\"access_token\":\"ok\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"all\",\"scope\":\"transactional.email:write\"}")]
    public async Task AnAmbiguousOrBroaderTokenStopsBeforeSendingMail(string receipt)
    {
        var handler = new CaptureHandler { TokenReply = _ => Task.FromResult(Reply(HttpStatusCode.OK, receipt)) };
        using var services = Services(handler);
        var error = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message("confirm"), default));
        Assert.Equal("MAIL_UNCONFIRMED", error.Code); Assert.Null(error.InnerException);
        Assert.Single(handler.Requests); Assert.DoesNotContain(Secret, error.ToString());
    }

    [Theory]
    [InlineData(400, "MAIL_REJECTED")]
    [InlineData(401, "MAIL_REJECTED")]
    [InlineData(302, "MAIL_REJECTED")]
    [InlineData(429, "MAIL_RATE_LIMITED")]
    [InlineData(503, "MAIL_UNAVAILABLE")]
    public async Task TokenFailureIsSanitizedAndDoesNotFallBackOrRetry(int status, string code)
    {
        var handler = new CaptureHandler { TokenReply = _ => Task.FromResult(Reply((HttpStatusCode)status, Secret)) };
        using var services = Services(handler);
        var error = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message("confirm"), default));
        Assert.Equal(code, error.Code); Assert.Single(handler.Requests); Assert.DoesNotContain(Secret, error.ToString());
    }

    [Fact]
    public async Task AnOmittedScopeMeansTheExplicitlyRequestedScopeUnderOAuth()
    {
        var handler = new CaptureHandler { TokenReply = _ => Task.FromResult(Reply(HttpStatusCode.OK,
            "{\"access_token\":\"synthetic.machine.token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")) };
        using var services = Services(handler);
        await services.GetRequiredService<IAccountMail>().Send(Message("confirm"), default);
        Assert.Equal(2, handler.Requests.Count);
        var requested = QueryHelpers.ParseQuery(handler.Requests.First().Body);
        Assert.Equal(Scope, requested["scope"]);
    }

    [Theory]
    [InlineData("QuickPrompt:Accounts:Brevo:AuthMode", "unknown")]
    [InlineData("QuickPrompt:Accounts:Brevo:AuthMode", "ApiKey")]
    [InlineData("QuickPrompt:Accounts:Brevo:AuthMode", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:ClientId", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:ClientId", "invalid\nheader")]
    [InlineData("QuickPrompt:Accounts:Brevo:ClientSecret", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:ClientSecret", "invalid\nsecret")]
    [InlineData("QuickPrompt:Accounts:Brevo:ApiKey", "Synthetic-api-key-must-not-be-mixed")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderEmail", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderName", "invalid\nname")]
    [InlineData("QuickPrompt:Accounts:MailProvider", "Mailjet")]
    [InlineData("QuickPrompt:Accounts:DevelopmentMail", "true")]
    public void IncompleteMixedOrImplicitCredentialsFailBeforeHttp(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => Services(new CaptureHandler(), settings));
    }

    [Fact]
    public void AnExplicitModeWithoutCredentialsKeepsMailUnavailable()
    {
        using var services = Services(new CaptureHandler(), new() { ["QuickPrompt:Accounts:Brevo:AuthMode"] = "ClientCredentials" });
        Assert.False(services.GetRequiredService<IAccountMail>().Available);
    }

    [Theory]
    [InlineData("https://foreign.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code", "confirm")]
    [InlineData("https://machine.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code", "campaign")]
    [InlineData("https://machine.example.invalid/account/reset?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code", "confirm")]
    public async Task UntrustedAccountAuthorityCannotEvenObtainAToken(string link, string purpose)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<IAccountMail>().Send(new("recipient@example.invalid", purpose, link), default));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CancellationAfterTokenAcquisitionCannotSendTheEmail()
    {
        using var stop = new CancellationTokenSource();
        var handler = new CaptureHandler { TokenReply = _ => { stop.Cancel(); return Task.FromResult(Reply(HttpStatusCode.OK, Receipt)); } };
        using var services = Services(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<IAccountMail>().Send(Message("reset"), stop.Token));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AStalledTokenRequestUsesTheSameTotalDeadlineAndCannotSendEmail()
    {
        var handler = new CaptureHandler { TokenReply = async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler);
        var error = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message("confirm"), default).WaitAsync(TimeSpan.FromSeconds(25)));
        Assert.Equal("MAIL_TIMEOUT", error.Code); Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TwoConcurrentRecipientsGetIndependentExchangesAndOneSendEach()
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        var mail = services.GetRequiredService<IAccountMail>();
        await Task.WhenAll(mail.Send(Message("confirm") with { Email = "one@example.invalid" }, default),
            mail.Send(Message("reset") with { Email = "two@example.invalid" }, default));
        var requests = handler.Requests.ToArray(); Assert.Equal(4, requests.Length);
        Assert.Equal(2, requests.Count(item => item.Uri == TokenUri));
        var sends = requests.Where(item => item.Uri == MailUri).ToArray(); Assert.Equal(2, sends.Length);
        var recipients = sends.Select(item => { using var json = JsonDocument.Parse(item.Body); return json.RootElement.GetProperty("to")[0].GetProperty("email").GetString(); }).Order();
        Assert.Equal(new[] { "one@example.invalid", "two@example.invalid" }, recipients);
    }

    private static AccountMail Message(string purpose) => new("recipient@example.invalid", purpose,
        $"https://machine.example.invalid/account/{purpose}?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code");
    private static HttpResponseMessage Reply(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };
    private static ServiceProvider Services(CaptureHandler handler, Dictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(), new Production(), new("https://machine.example.invalid/"));
        services.AddHttpClient(BrevoAccountMail.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private sealed record Request(string Uri, string Method, string? ContentType, string Body, string? Authorization, bool ApiKeyPresent);
    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal ConcurrentQueue<Request> Requests { get; } = new();
        internal Func<CancellationToken, Task<HttpResponseMessage>> TokenReply = _ => Task.FromResult(Reply(HttpStatusCode.OK, Receipt));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(new(request.RequestUri!.AbsoluteUri, request.Method.Method, request.Content!.Headers.ContentType?.MediaType,
                await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString(), request.Headers.Contains("api-key")));
            return request.RequestUri.AbsoluteUri == TokenUri ? await TokenReply(cancellationToken)
                : Reply(HttpStatusCode.Created, "{\"messageId\":\"synthetic-accepted-message\"}");
        }
    }
    private sealed class Production : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "QuickPrompt.Mail.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
