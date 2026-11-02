using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Controlled HTTP only; these checks never use credentials, create accounts or prove actual inbox delivery.</summary>
public sealed class MailjetAccountMailTests
{
    private static readonly Uri Origin = new("https://mail-qa.example.invalid/");
    private const string Key = "Synthetic-mailjet-public-key";
    private const string Secret = "Synthetic-mailjet-private-secret";
    private const string Link = "https://mail-qa.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code";
    private static readonly AccountMail Confirmation = new("recipient@example.invalid", "confirm", Link);
    private const string Receipt = """{"Messages":[{"Status":"success","To":[{"Email":"recipient@example.invalid","MessageID":456,"MessageUUID":"opaque-mailjet-id"}]}]}""";

    private static Dictionary<string, string?> Settings() => new() {
        ["QuickPrompt:Accounts:MailProvider"] = "Mailjet",
        ["QuickPrompt:Accounts:Mailjet:ApiKey"] = Key,
        ["QuickPrompt:Accounts:Mailjet:ApiSecret"] = Secret,
        ["QuickPrompt:Accounts:Mailjet:SenderEmail"] = "sender@example.invalid",
        ["QuickPrompt:Accounts:Mailjet:SenderName"] = "QuickPrompt Cuentas"
    };

    private static ServiceProvider Services(CaptureHandler? handler = null, Dictionary<string, string?>? settings = null, SafeLog? log = null)
    {
        var services = new ServiceCollection();
        if (log is not null) services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(), new Production(), Origin);
        if (handler is not null) services.AddHttpClient(MailjetAccountMail.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("reset")]
    public async Task CanonicalMessageUsesOnlyOneRecipientAndTheFixedAuthenticatedApi(string purpose)
    {
        var handler = new CaptureHandler(); var log = new SafeLog(); using var services = Services(handler, log: log);
        var mail = Assert.IsType<MailjetAccountMail>(services.GetRequiredService<IAccountMail>());
        var message = Confirmation with { Purpose = purpose, Link = Link.Replace("/confirm?", "/" + purpose + "?", StringComparison.Ordinal) };
        await mail.Send(message, default);
        Assert.True(mail.Available); Assert.Equal(1, handler.Calls);
        Assert.Equal(new Uri("https://api.mailjet.com/v3.1/send"), handler.Uri); Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("Basic", handler.Scheme); Assert.Equal(Key + ":" + Secret, handler.DecodedCredentials);
        Assert.Equal(new[] { "Messages" }, handler.Body.EnumerateObject().Select(property => property.Name));
        var sent = Assert.Single(handler.Body.GetProperty("Messages").EnumerateArray());
        Assert.Equal(new[] { "From", "Subject", "TextPart", "To" }, sent.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("sender@example.invalid", sent.GetProperty("From").GetProperty("Email").GetString());
        Assert.Equal("QuickPrompt Cuentas", sent.GetProperty("From").GetProperty("Name").GetString());
        Assert.Equal(message.Email, Assert.Single(sent.GetProperty("To").EnumerateArray()).GetProperty("Email").GetString());
        Assert.Contains(message.Link, sent.GetProperty("TextPart").GetString());
        Assert.Equal(purpose == "confirm" ? "Confirma tu cuenta de QuickPrompt" : "Recupera tu acceso a QuickPrompt", sent.GetProperty("Subject").GetString());
        Assert.DoesNotContain(log.Messages, value => value.Contains(Key) || value.Contains(Secret) || value.Contains("synthetic_confirm_code") || value.Contains(handler.EncodedCredentials!));
    }

    [Fact]
    public void NamedTransportDisablesRedirectsCookiesAndBoundsTheEntireSend()
    {
        using var services = Services();
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(MailjetAccountMail.ClientName);
        while (handler is DelegatingHandler wrapper) handler = wrapper.InnerHandler!;
        var transport = Assert.IsType<SocketsHttpHandler>(handler); Assert.False(transport.AllowAutoRedirect); Assert.False(transport.UseCookies);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(MailjetAccountMail.ClientName);
        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Theory]
    [InlineData(401, "MAIL_REJECTED")]
    [InlineData(429, "MAIL_RATE_LIMITED")]
    [InlineData(503, "MAIL_UNAVAILABLE")]
    [InlineData(302, "MAIL_REJECTED")]
    [InlineData(201, "MAIL_REJECTED")]
    public async Task RejectionCannotLeakProviderContentOrCauseAnotherSend(int status, string code)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) {
            Content = new StringContent(Secret + " synthetic_confirm_code"), Headers = { Location = new Uri("https://untrusted.example.invalid/") }
        }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal(code, failure.Code); Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(Secret, failure.ToString()); Assert.DoesNotContain("synthetic_confirm_code", failure.ToString());
    }

    [Theory]
    [InlineData("status")]
    [InlineData("batch")]
    [InlineData("recipient")]
    [InlineData("extra-recipient")]
    [InlineData("no-recipient")]
    [InlineData("missing-id")]
    [InlineData("zero-id")]
    [InlineData("string-id")]
    [InlineData("empty-uuid")]
    [InlineData("cc")]
    [InlineData("bcc")]
    [InlineData("errors")]
    public async Task SuccessStatusRequiresAnUnambiguousReceiptForTheRequestedAddress(string mutation)
    {
        var root = JsonNode.Parse(Receipt)!; var results = root["Messages"]!.AsArray(); var message = results[0]!; var to = message["To"]!.AsArray();
        switch (mutation) {
            case "status": message["Status"] = "error"; break;
            case "batch": results.Add(message.DeepClone()); break;
            case "recipient": to[0]!["Email"] = "other@example.invalid"; break;
            case "extra-recipient": to.Add(to[0]!.DeepClone()); break;
            case "no-recipient": to.Clear(); break;
            case "missing-id": to[0]!.AsObject().Remove("MessageID"); break;
            case "zero-id": to[0]!["MessageID"] = 0; break;
            case "string-id": to[0]!["MessageID"] = "456"; break;
            case "empty-uuid": to[0]!["MessageUUID"] = " "; break;
            case "cc": message["Cc"] = JsonNode.Parse("""[{"Email":"other@example.invalid"}]"""); break;
            case "bcc": message["Bcc"] = JsonNode.Parse("""[{"Email":"other@example.invalid"}]"""); break;
            case "errors": message["Errors"] = JsonNode.Parse("""[{"ErrorMessage":"private detail"}]"""); break;
        }
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(Ok(root.ToJsonString())) }; using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Messages\":{}}")]
    [InlineData("{\"Messages\":[]}")]
    [InlineData("{\"Messages\":[null]}")]
    public async Task MalformedOrMissingReceiptsNeverClaimAcceptance(string body)
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(Ok(body)) }; using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OversizedReceiptIsUnconfirmed()
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(Ok(new string('x', 16385))) }; using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task InterruptedResponseBodyStaysUnconfirmedWithoutLeakingOrRetrying()
    {
        var handler = new CaptureHandler { Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new InterruptedReceipt())
        }) };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code); Assert.Equal(1, handler.Calls);
        Assert.Null(failure.InnerException); Assert.DoesNotContain(Secret, failure.ToString());
    }

    [Fact]
    public async Task CancellationAndNetworkFailuresNeverScheduleAnotherSend()
    {
        var handler = new CaptureHandler { Reply = async stop => { await Task.Delay(Timeout.InfiniteTimeSpan, stop); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler); using var cancelled = new CancellationTokenSource();
        var send = services.GetRequiredService<IAccountMail>().Send(Confirmation, cancelled.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send); Assert.Equal(1, handler.Calls);
        handler.Reply = _ => throw new HttpRequestException(Secret);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default));
        Assert.Equal("MAIL_UNAVAILABLE", failure.Code); Assert.Null(failure.InnerException); Assert.Equal(2, handler.Calls);
        Assert.DoesNotContain(Secret, failure.ToString());
    }

    [Fact]
    public async Task AStalledProviderTerminatesAtTheConfiguredDeadline()
    {
        var handler = new CaptureHandler { Reply = async stop => { await Task.Delay(Timeout.InfiniteTimeSpan, stop); throw new InvalidOperationException("Unreachable"); } };
        using var services = Services(handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation, default).WaitAsync(TimeSpan.FromSeconds(25)));
        Assert.Equal("MAIL_TIMEOUT", failure.Code); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("confirm", "https://foreign.example.invalid/account/confirm?userId=10000000-0000-4000-8000-000000000001&code=synthetic_confirm_code")]
    [InlineData("campaign", Link)]
    [InlineData("confirm", Link + "&bcc=other@example.invalid")]
    public async Task UntrustedAccountAuthorityFailsBeforeHttp(string purpose, string link)
    {
        var handler = new CaptureHandler(); using var services = Services(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<IAccountMail>().Send(Confirmation with { Purpose = purpose, Link = link }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("QuickPrompt:Accounts:Mailjet:ApiKey", "")]
    [InlineData("QuickPrompt:Accounts:Mailjet:ApiKey", "invalid:basic-user-name")]
    [InlineData("QuickPrompt:Accounts:Mailjet:ApiSecret", "")]
    [InlineData("QuickPrompt:Accounts:Mailjet:ApiSecret", "not valid secret value")]
    [InlineData("QuickPrompt:Accounts:Mailjet:SenderEmail", "")]
    [InlineData("QuickPrompt:Accounts:Mailjet:SenderName", "Name\nInjected")]
    [InlineData("QuickPrompt:Accounts:MailProvider", "Brevo")]
    [InlineData("QuickPrompt:Accounts:MailProvider", "Smtp2Go")]
    [InlineData("QuickPrompt:Accounts:MailProvider", "mailjet")]
    [InlineData("QuickPrompt:Accounts:MailProvider", "")]
    [InlineData("QuickPrompt:Accounts:DevelopmentMail", "true")]
    public void IncompleteInvalidOrImplicitConfigurationFailsBeforeCreatingAClient(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Theory]
    [InlineData("Brevo")]
    [InlineData("Smtp2Go")]
    public void AnotherCompleteProviderCannotBeKeptAsASilentFallback(string other)
    {
        var settings = Settings(); settings[$"QuickPrompt:Accounts:{other}:ApiKey"] = Key;
        settings[$"QuickPrompt:Accounts:{other}:SenderEmail"] = "sender@example.invalid"; settings[$"QuickPrompt:Accounts:{other}:SenderName"] = "QuickPrompt";
        Assert.Throws<InvalidOperationException>(() => Services(settings: settings));
    }

    [Fact]
    public void SelectorWithoutCredentialsLeavesAccountMailUnavailable()
    {
        using var services = Services(settings: new() { ["QuickPrompt:Accounts:MailProvider"] = "Mailjet" });
        Assert.False(services.GetRequiredService<IAccountMail>().Available); Assert.IsType<UnconfiguredAccountMail>(services.GetRequiredService<IAccountMail>());
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    private sealed class InterruptedReceipt : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException(Secret);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(new IOException(Secret));
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Calls; internal Uri? Uri; internal HttpMethod? Method; internal string? Scheme; internal string? EncodedCredentials;
        internal string? DecodedCredentials; internal JsonElement Body;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<CancellationToken, Task<HttpResponseMessage>> Reply = _ => Task.FromResult(Ok(Receipt));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; Method = request.Method; Scheme = request.Headers.Authorization?.Scheme;
            EncodedCredentials = request.Headers.Authorization?.Parameter; DecodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedCredentials!));
            Body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken)); Started.TrySetResult();
            return await Reply(cancellationToken);
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
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        public void Dispose() { }
    }
}
