using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Controlled HTTP contracts only; no Gmail permission, real credential or external send.</summary>
public sealed class EmailJsAccountMailTests
{
    private const string PublicKey = "Synthetic-public-key-not-real";
    private const string PrivateKey = "Synthetic-private-key-not-real";
    private const string Link = "https://accounts.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code";
    private static readonly AccountMail Message = new("recipient@example.invalid", "confirm", Link);
    private static Dictionary<string, string?> Settings() => new() {
        ["QuickPrompt:Accounts:MailProvider"] = "EmailJs",
        ["QuickPrompt:Accounts:EmailJs:PublicKey"] = PublicKey,
        ["QuickPrompt:Accounts:EmailJs:PrivateKey"] = PrivateKey,
        ["QuickPrompt:Accounts:EmailJs:ServiceId"] = "quickprompt_accounts",
        ["QuickPrompt:Accounts:EmailJs:ConfirmTemplate"] = "quickprompt_confirm",
        ["QuickPrompt:Accounts:EmailJs:ResetTemplate"] = "quickprompt_reset"
    };

    private static ServiceProvider Services(CaptureHandler? handler = null, Dictionary<string, string?>? settings = null, SafeLog? log = null)
    {
        var services = new ServiceCollection();
        if (log is not null) services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(),
            new Production(), new("https://accounts.example.invalid/"));
        if (handler is not null) services.AddHttpClient(EmailJsAccountMail.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("confirm", "quickprompt_confirm")]
    [InlineData("reset", "quickprompt_reset")]
    public async Task PrivateServerRequestHasOnlyTheIntendedRecipientAndCanonicalLink(string purpose, string template)
    {
        var handler = new CaptureHandler(); var log = new SafeLog(); using var services = Services(handler, log: log);
        var mail = Assert.IsType<EmailJsAccountMail>(services.GetRequiredService<IAccountMail>());
        var message = Message with { Purpose = purpose, Link = Link.Replace("/confirm?", "/" + purpose + "?", StringComparison.Ordinal) };
        await mail.Send(message, default);
        Assert.True(mail.Available); Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("https://api.emailjs.com/api/v1.0/email/send"), handler.Endpoint);
        Assert.Equal(new[] { "accessToken", "service_id", "template_id", "template_params", "user_id" },
            handler.Body.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(PublicKey, handler.Body.GetProperty("user_id").GetString());
        Assert.Equal(PrivateKey, handler.Body.GetProperty("accessToken").GetString());
        Assert.Equal("quickprompt_accounts", handler.Body.GetProperty("service_id").GetString());
        Assert.Equal(template, handler.Body.GetProperty("template_id").GetString());
        var parameters = handler.Body.GetProperty("template_params");
        Assert.Equal(new[] { "action_url", "to_email" }, parameters.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(message.Email, parameters.GetProperty("to_email").GetString());
        Assert.Equal(message.Link, parameters.GetProperty("action_url").GetString());
        Assert.DoesNotContain(log.Messages, value => value.Contains(PrivateKey, StringComparison.Ordinal)
            || value.Contains(PublicKey, StringComparison.Ordinal) || value.Contains("synthetic_confirm_code", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OK")]
    [InlineData("\"OK\"")]
    [InlineData(" OK\n")]
    public async Task OnlyDocumentedSuccessReceiptsAreAccepted(string receipt)
    {
        var handler = new CaptureHandler { Receipt = receipt }; using var services = Services(handler);
        await services.GetRequiredService<IAccountMail>().Send(Message, default); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(400, "MAIL_REJECTED")]
    [InlineData(401, "MAIL_REJECTED")]
    [InlineData(429, "MAIL_RATE_LIMITED")]
    [InlineData(503, "MAIL_UNAVAILABLE")]
    [InlineData(302, "MAIL_REJECTED")]
    [InlineData(202, "MAIL_REJECTED")]
    public async Task ProviderFailuresAreSafeAndNeverRetried(int status, string code)
    {
        var handler = new CaptureHandler { Status = (HttpStatusCode)status, Receipt = PrivateKey + " private-diagnostic" };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message, default));
        Assert.Equal(code, failure.Code); Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(PrivateKey, failure.ToString()); Assert.DoesNotContain("private-diagnostic", failure.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ok")]
    [InlineData("OK private-diagnostic")]
    [InlineData("{}")]
    [InlineData("{\"status\":\"OK\"}")]
    public async Task AmbiguousReceiptsRemainUnconfirmed(string receipt)
    {
        using var services = Services(new CaptureHandler { Receipt = receipt });
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code);
    }

    [Fact]
    public async Task OversizedReceiptCannotClaimAcceptance()
    {
        var handler = new CaptureHandler { Receipt = new string(' ', 16384) + "OK" }; using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Message, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("https://elsewhere.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code", "confirm")]
    [InlineData(Link, "newsletter")]
    [InlineData(Link + "&to=other@example.invalid", "confirm")]
    public async Task InvalidAuthorityIsRejectedBeforeHttp(string link, string purpose)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<IAccountMail>().Send(Message with { Link = link, Purpose = purpose }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("PublicKey", "")]
    [InlineData("PrivateKey", "")]
    [InlineData("ServiceId", "unsafe/service")]
    [InlineData("ConfirmTemplate", "quickprompt_reset")]
    [InlineData("ResetTemplate", "")]
    public void IncompleteOrUnsafeConfigurationFailsClosed(string name, string value)
    {
        var settings = Settings(); settings["QuickPrompt:Accounts:EmailJs:" + name] = value;
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Fact]
    public void DeliveryNeedsExplicitSelectionAndCannotMixDevelopmentOrOtherProviders()
    {
        var settings = Settings(); settings.Remove("QuickPrompt:Accounts:MailProvider");
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
        settings = Settings(); settings["QuickPrompt:Accounts:DevelopmentMail"] = "true";
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
        settings = Settings(); settings["QuickPrompt:Accounts:Mailjet:ApiKey"] = PublicKey;
        settings["QuickPrompt:Accounts:Mailjet:ApiSecret"] = PrivateKey;
        settings["QuickPrompt:Accounts:Mailjet:SenderEmail"] = "sender@example.invalid";
        settings["QuickPrompt:Accounts:Mailjet:SenderName"] = "QA";
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
        using var unconfigured = Services(settings: new() { ["QuickPrompt:Accounts:MailProvider"] = "EmailJs" });
        Assert.IsType<UnconfiguredAccountMail>(unconfigured.GetRequiredService<IAccountMail>());
    }

    [Fact]
    public void TransportIsBoundedAndCannotRedirectCredentialsOrShareCookies()
    {
        using var services = Services();
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(EmailJsAccountMail.ClientName);
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var transport = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(transport.AllowAutoRedirect); Assert.False(transport.UseCookies);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(EmailJsAccountMail.ClientName);
        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Calls;
        internal Uri? Endpoint;
        internal HttpMethod? Method;
        internal JsonElement Body;
        internal string Receipt = "OK";
        internal HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Endpoint = request.RequestUri; Method = request.Method;
            Body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new(Status) { Content = new StringContent(Receipt) };
        }
    }

    private sealed class Production : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "QuickPrompt.Mail.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class SafeLog : ILoggerProvider, ILogger
    {
        internal List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        public void Dispose() { }
    }
}
