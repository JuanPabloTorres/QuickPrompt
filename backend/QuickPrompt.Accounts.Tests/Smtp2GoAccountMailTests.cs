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

/// <summary>Synthetic HTTP only: no account creation, credential lookup, DNS or external mail.</summary>
public sealed class Smtp2GoAccountMailTests
{
    private static readonly Uri Origin = new("https://mail-qa.example.invalid/");
    private const string Key = "Synthetic-smtp2go-key-not-a-secret";
    private const string Link = "https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code";
    private static readonly AccountMail Confirmation = new("recipient@example.invalid", "confirm", Link);
    private static Dictionary<string, string?> Settings() => new() {
        ["QuickPrompt:Accounts:MailProvider"] = "Smtp2Go",
        ["QuickPrompt:Accounts:Smtp2Go:ApiKey"] = Key,
        ["QuickPrompt:Accounts:Smtp2Go:SenderEmail"] = "sender@example.invalid",
        ["QuickPrompt:Accounts:Smtp2Go:SenderName"] = "QuickPrompt QA"
    };

    private static ServiceProvider Services(CaptureHandler? handler = null, Dictionary<string, string?>? settings = null, Uri? origin = null, SafeMailLog? log = null)
    {
        var services = new ServiceCollection();
        if (log is not null) services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(), new Environment(), origin ?? Origin);
        if (handler is not null) services.AddHttpClient(Smtp2GoAccountMail.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("reset")]
    public async Task OneCanonicalAccountMessageRequiresAnExplicitSingleRecipientReceipt(string purpose)
    {
        var handler = new CaptureHandler(); var log = new SafeMailLog(); using var services = Services(handler, log: log);
        var mail = Assert.IsType<Smtp2GoAccountMail>(services.GetRequiredService<IAccountMail>()); Assert.True(mail.Available);
        var message = Confirmation with { Purpose = purpose, Link = Link.Replace("/confirm?", "/" + purpose + "?", StringComparison.Ordinal) };
        await mail.Send(message, default);
        Assert.Equal(1, handler.Calls); Assert.Equal(new Uri("https://api.smtp2go.com/v3/email/send"), handler.Uri); Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.True(handler.ApiKey == Key, "Only the fixed API may receive the synthetic credential.");
        Assert.Equal(new[] { "sender", "subject", "text_body", "to" }, handler.Body.EnumerateObject().Select(property => property.Name).Order());
        var sender = new System.Net.Mail.MailAddress(handler.Body.GetProperty("sender").GetString()!);
        Assert.Equal("sender@example.invalid", sender.Address); Assert.Equal("QuickPrompt QA", sender.DisplayName);
        Assert.Equal("recipient@example.invalid", Assert.Single(handler.Body.GetProperty("to").EnumerateArray()).GetString());
        Assert.Contains(message.Link, handler.Body.GetProperty("text_body").GetString());
        Assert.DoesNotContain(log.Messages, text => text.Contains(Key, StringComparison.Ordinal) || text.Contains("synthetic_confirm_code", StringComparison.Ordinal));
    }

    [Fact]
    public void NamedTransportHasNoRedirectsOrSharedCookiesAndIsBounded()
    {
        using var services = Services();
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(Smtp2GoAccountMail.ClientName);
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var transport = Assert.IsType<SocketsHttpHandler>(handler); Assert.False(transport.AllowAutoRedirect); Assert.False(transport.UseCookies);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(Smtp2GoAccountMail.ClientName);
        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Theory]
    [InlineData(400, "MAIL_REJECTED")]
    [InlineData(401, "MAIL_REJECTED")]
    [InlineData(429, "MAIL_RATE_LIMITED")]
    [InlineData(503, "MAIL_UNAVAILABLE")]
    [InlineData(302, "MAIL_REJECTED")]
    [InlineData(201, "MAIL_REJECTED")]
    public async Task FailureOrAmbiguousStatusDoesNotClaimAcceptanceOrRetry(int status, string code)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) {
            Content = new StringContent(Key + " synthetic_confirm_code private-provider-diagnostic"), Headers = { Location = new Uri("https://untrusted.example.invalid/") }
        }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal(code, failure.Code); Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(Key, failure.ToString()); Assert.DoesNotContain("private-provider-diagnostic", failure.ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":{\"succeeded\":1,\"failed\":0,\"failures\":[],\"email_id\":null}}")]
    [InlineData("{\"data\":{\"succeeded\":1,\"failed\":0,\"failures\":[],\"email_id\":\" \"}}")]
    [InlineData("{\"data\":{\"succeeded\":\"1\",\"failed\":0,\"failures\":[],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":1,\"failed\":\"0\",\"failures\":[],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":0,\"failed\":1,\"failures\":[\"private\"],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":2,\"failed\":0,\"failures\":[],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":1,\"failed\":0,\"failures\":[\"private\"],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":1,\"failed\":0,\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":1.5,\"failed\":0,\"failures\":[],\"email_id\":\"id\"}}")]
    [InlineData("{\"data\":{\"succeeded\":2147483648,\"failed\":0,\"failures\":[],\"email_id\":\"id\"}}")]
    public async Task AnOkStatusWithoutAnUnambiguousSingleRecipientReceiptRemainsUnconfirmed(string body)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OversizedReceiptsAreNotAccepted()
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 16385)) }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellationIsPreservedWithoutSchedulingAnotherSend()
    {
        var handler = new CaptureHandler { Reply = async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler); using var stop = new CancellationTokenSource();
        var send = services.GetRequiredService<IAccountMail>().Send(Confirmation, stop.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AStalledProviderTerminatesAtTheConfiguredBoundWithoutRetry()
    {
        var handler = new CaptureHandler { Reply = async cancellation => { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default).WaitAsync(TimeSpan.FromSeconds(25)));
        Assert.Equal("MAIL_TIMEOUT", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NetworkFailureIsSafeAndNotRetried()
    {
        var handler = new CaptureHandler { Reply = _ => throw new HttpRequestException(Key + " synthetic_confirm_code") }; using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNAVAILABLE", failure.Code); Assert.Equal(1, handler.Calls); Assert.Null(failure.InnerException); Assert.DoesNotContain(Key, failure.ToString());
    }

    [Theory]
    [InlineData("https://untrusted.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code", "confirm")]
    [InlineData(Link, "newsletter")]
    [InlineData(Link + "&bcc=other@example.invalid", "confirm")]
    public async Task InvalidAccountAuthorityIsRejectedBeforeHttp(string link, string purpose)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation with { Link = link, Purpose = purpose }, default)); Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Brevo")]
    [InlineData("smtp2go")]
    [InlineData("Unknown")]
    public void AlternativeTransportRequiresItsExplicitExactSelector(string? selection)
    {
        var settings = Settings(); settings["QuickPrompt:Accounts:MailProvider"] = selection;
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Theory]
    [InlineData("Brevo")]
    [InlineData("Smtp2Go")]
    public void SelectingAProviderWithoutCredentialsDoesNotPretendDeliveryIsAvailable(string selection)
    {
        using var services = Services(settings: new() { ["QuickPrompt:Accounts:MailProvider"] = selection });
        Assert.IsType<UnconfiguredAccountMail>(services.GetRequiredService<IAccountMail>()); Assert.False(services.GetRequiredService<IAccountMail>().Available);
    }

    [Theory]
    [InlineData("QuickPrompt:Accounts:Smtp2Go:ApiKey", "")]
    [InlineData("QuickPrompt:Accounts:Smtp2Go:SenderEmail", "")]
    [InlineData("QuickPrompt:Accounts:Smtp2Go:SenderName", "Name\nInjected")]
    [InlineData("QuickPrompt:Accounts:Brevo:ApiKey", "Synthetic-brevo-key-not-a-secret")]
    [InlineData("QuickPrompt:Accounts:DevelopmentMail", "true")]
    public void PartialInvalidOrConflictingSettingsFailBeforeCreatingAClient(string name, string value)
    {
        var settings = Settings(); settings[name] = value; Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Fact]
    public void CompleteProvidersCannotBeMixedOrSilentlySubstituted()
    {
        var settings = Settings(); settings["QuickPrompt:Accounts:Brevo:ApiKey"] = "Synthetic-brevo-key-not-a-secret";
        settings["QuickPrompt:Accounts:Brevo:SenderEmail"] = "sender@example.invalid"; settings["QuickPrompt:Accounts:Brevo:SenderName"] = "QuickPrompt QA";
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
        settings.Remove("QuickPrompt:Accounts:Smtp2Go:ApiKey"); settings.Remove("QuickPrompt:Accounts:Smtp2Go:SenderEmail"); settings.Remove("QuickPrompt:Accounts:Smtp2Go:SenderName");
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Calls; internal Uri? Uri; internal HttpMethod? Method; internal string? ApiKey; internal JsonElement Body;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<CancellationToken, Task<HttpResponseMessage>> Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"data\":{\"succeeded\":1,\"failed\":0,\"failures\":[],\"email_id\":\"synthetic-accepted-message\"}}") });
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; Method = request.Method; ApiKey = request.Headers.GetValues("X-Smtp2go-Api-Key").Single();
            Body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken)); Started.TrySetResult(); return await Reply(cancellationToken);
        }
    }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
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
