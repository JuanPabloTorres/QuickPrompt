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

/// <summary>Transport/configuration tests over synthetic HTTP. Never creates a Brevo account or sends external mail.</summary>
public sealed class BrevoAccountMailTests
{
    private static readonly Uri Origin = new("https://mail-qa.example.invalid/");
    private const string Key = "Synthetic-brevo-key-not-a-secret";
    private const string Link = "https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code";
    private static readonly AccountMail Confirmation = new("recipient@example.invalid", "confirm", Link);
    private static Dictionary<string, string?> Settings() => new() {
        ["QuickPrompt:Accounts:Brevo:ApiKey"] = Key,
        ["QuickPrompt:Accounts:Brevo:SenderEmail"] = "sender@example.invalid",
        ["QuickPrompt:Accounts:Brevo:SenderName"] = "QuickPrompt QA"
    };
    private static ServiceProvider Services(CaptureHandler? handler = null, Dictionary<string, string?>? settings = null, string environment = "Production", Uri? origin = null, SafeMailLog? log = null)
    {
        var services = new ServiceCollection();
        if (log is not null) services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(), new Environment(environment), origin ?? Origin);
        if (handler is not null) services.AddHttpClient(BrevoAccountMail.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("reset")]
    public async Task OnlyAConfirmedProviderReceiptCompletesOneCanonicalAccountMessage(string purpose)
    {
        var handler = new CaptureHandler(); var log = new SafeMailLog();
        using var services = Services(handler, log: log);
        var mail = services.GetRequiredService<IAccountMail>();
        Assert.True(mail.Available); Assert.IsType<BrevoAccountMail>(mail);
        var message = Confirmation with { Purpose = purpose, Link = Link.Replace("/confirm?", "/" + purpose + "?", StringComparison.Ordinal) };
        await mail.Send(message, default);
        Assert.Equal(1, handler.Calls); Assert.Equal(new Uri("https://api.brevo.com/v3/smtp/email"), handler.Uri); Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.True(handler.ApiKey == Key, "The configured synthetic credential must be sent only to the fixed API.");
        var body = handler.Body;
        Assert.Equal(new[] { "sender", "subject", "textContent", "to" }, body.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("sender@example.invalid", body.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("QuickPrompt QA", body.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("recipient@example.invalid", Assert.Single(body.GetProperty("to").EnumerateArray()).GetProperty("email").GetString());
        Assert.Contains(message.Link, body.GetProperty("textContent").GetString());
        Assert.False(log.Messages.Any(text => text.Contains(Key, StringComparison.Ordinal) || text.Contains("synthetic_confirm_code", StringComparison.Ordinal)), "Mail logs must omit private credential and account-link values.");
    }

    [Fact]
    public void NamedTransportCannotFollowRedirectsOrShareCookiesAndHasAFifteenSecondTimeout()
    {
        using var services = Services();
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(BrevoAccountMail.ClientName);
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var transport = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(transport.AllowAutoRedirect); Assert.False(transport.UseCookies);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(BrevoAccountMail.ClientName);
        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Theory]
    [InlineData(400, "MAIL_REJECTED")]
    [InlineData(401, "MAIL_REJECTED")]
    [InlineData(429, "MAIL_RATE_LIMITED")]
    [InlineData(503, "MAIL_UNAVAILABLE")]
    [InlineData(302, "MAIL_REJECTED")]
    [InlineData(200, "MAIL_REJECTED")]
    public async Task RejectionOrAmbiguousStatusDoesNotClaimDeliveryOrRetry(int status, string code)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) {
            Content = new StringContent(Key + " synthetic_confirm_code private-provider-diagnostic"), Headers = { Location = new Uri("https://untrusted.example.invalid/") }
        }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal(code, failure.Code); Assert.Equal(1, handler.Calls);
        Assert.False(failure.ToString().Contains(Key, StringComparison.Ordinal) || failure.ToString().Contains("private-provider-diagnostic", StringComparison.Ordinal), "Provider errors must not expose credentials or raw response text.");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"messageId\":null}")]
    [InlineData("{\"messageId\":\" \"}")]
    public async Task ACreatedStatusWithoutAUsableReceiptRemainsUnconfirmed(string body)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(body) }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AnOversizedProviderReceiptDoesNotBecomeDeliveryConfirmation()
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(new string('x', 16385)) }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AStalledSendTerminatesAtTheConfiguredBoundWithoutRetry()
    {
        var handler = new CaptureHandler { Reply = async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default).WaitAsync(TimeSpan.FromSeconds(25)));
        Assert.Equal("MAIL_TIMEOUT", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellationIsPreservedAndDoesNotScheduleAnotherSend()
    {
        var handler = new CaptureHandler { Reply = async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler); using var stop = new CancellationTokenSource();
        var send = services.GetRequiredService<IAccountMail>().Send(Confirmation, stop.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("https://untrusted.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    [InlineData("http://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    [InlineData("https://mail-qa.example.invalid/account/reset?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    [InlineData("https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code&redirect=https://untrusted.example.invalid", "confirm")]
    [InlineData("https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code#fragment", "confirm")]
    [InlineData("https://name@mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    [InlineData(Link, "newsletter")]
    [InlineData("https://mail-qa.example.invalid/account/confirm?userId=00000000-0000-0000-0000-000000000000&code=synthetic_confirm_code", "confirm")]
    [InlineData("https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=bad%0Acode", "confirm")]
    [InlineData("https://mail-qa.example.invalid/account/confirm?userid=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    public async Task AnUntrustedLinkOrPurposeIsRejectedBeforeAnyHttp(string link, string purpose)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation with { Link = link, Purpose = purpose }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("QuickPrompt:Accounts:Brevo:ApiKey", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderEmail", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderName", "")]
    [InlineData("QuickPrompt:Accounts:Brevo:ApiKey", "Synthetic-Header\nInjection")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderEmail", "Display Name <sender@example.invalid>")]
    [InlineData("QuickPrompt:Accounts:Brevo:SenderName", "Name\nInjected")]
    [InlineData("QuickPrompt:Accounts:DevelopmentMail", "true")]
    public void PartialInvalidOrConflictingConfigurationFailsBeforeCreatingAClient(string name, string value)
    {
        var settings = Settings(); settings[name] = value;
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Theory]
    [InlineData("http://mail-qa.example.invalid/")]
    [InlineData("https://mail-qa.example.invalid/account/")]
    [InlineData("https://user@mail-qa.example.invalid/")]
    public void ExternalMailRequiresTheCanonicalHttpsOrigin(string origin) =>
        Assert.Throws<InvalidOperationException>(() => Services(origin: new Uri(origin)));

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", false)]
    [InlineData("Production", true)]
    public void MissingExternalSettingsNeverEnableDeliveryOutsideTheExplicitDevelopmentChoice(string environment, bool pickup)
    {
        using var services = Services(settings: new() { ["QuickPrompt:Accounts:DevelopmentMail"] = pickup.ToString() }, environment: environment);
        var mail = services.GetRequiredService<IAccountMail>(); Assert.IsType<UnconfiguredAccountMail>(mail); Assert.False(mail.Available);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Calls; internal Uri? Uri; internal HttpMethod? Method; internal string? ApiKey; internal JsonElement Body;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<CancellationToken, Task<HttpResponseMessage>> Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"messageId\":\"synthetic-accepted-message\"}") });
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; Method = request.Method; ApiKey = request.Headers.GetValues("api-key").Single();
            Body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken)); Started.TrySetResult();
            return await Reply(cancellationToken);
        }
    }
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "QuickPrompt.Mail.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class SafeMailLog : ILoggerProvider, ILogger
    {
        internal List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        public void Dispose() { }
    }
}
