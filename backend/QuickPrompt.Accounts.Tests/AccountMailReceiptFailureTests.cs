using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Interrupted provider receipts are ambiguous, not permission to resend or expose transport diagnostics.</summary>
public sealed class AccountMailReceiptFailureTests
{
    private const string Canary = "Synthetic-private-body-diagnostic-not-a-real-secret";

    [Theory]
    [InlineData("Brevo", "confirm")]
    [InlineData("Brevo", "reset")]
    [InlineData("Smtp2Go", "confirm")]
    [InlineData("Smtp2Go", "reset")]
    [InlineData("Mailjet", "confirm")]
    [InlineData("Mailjet", "reset")]
    [InlineData("BrevoMachine", "confirm")]
    [InlineData("BrevoMachine", "reset")]
    public async Task InterruptedReceiptIsSanitizedWithoutRepeatingTheSend(string provider, string purpose)
    {
        using var handler = new InterruptedHandler(provider);
        using var services = Services(provider, handler);
        var failure = await Assert.ThrowsAsync<AccountMailDeliveryException>(() =>
            services.GetRequiredService<IAccountMail>().Send(Message(purpose), default));
        Assert.Equal("MAIL_UNCONFIRMED", failure.Code);
        Assert.DoesNotContain(Canary, failure.ToString());
        Assert.Null(failure.InnerException);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Brevo")]
    [InlineData("Smtp2Go")]
    [InlineData("Mailjet")]
    [InlineData("BrevoMachine")]
    public async Task CancellationDuringAnInterruptedReceiptKeepsCallerCancellation(string provider)
    {
        using var stop = new CancellationTokenSource();
        using var handler = new InterruptedHandler(provider, stop.Cancel);
        using var services = Services(provider, handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            services.GetRequiredService<IAccountMail>().Send(Message("confirm"), stop.Token));
        Assert.Equal(1, handler.Calls);
    }

    private static AccountMail Message(string purpose) => new("recipient@example.invalid", purpose,
        $"https://receipt.example.invalid/account/{purpose}?userId=10000000-0000-4000-8000-000000000001&code=synthetic_code");

    private static ServiceProvider Services(string provider, HttpMessageHandler handler)
    {
        var configuredProvider = provider == "BrevoMachine" ? "Brevo" : provider;
        var settings = new Dictionary<string, string?> {
            ["QuickPrompt:Accounts:MailProvider"] = configuredProvider,
            [$"QuickPrompt:Accounts:{configuredProvider}:ApiKey"] = "Synthetic-key-not-a-real-secret",
            [$"QuickPrompt:Accounts:{configuredProvider}:SenderEmail"] = "sender@example.invalid",
            [$"QuickPrompt:Accounts:{configuredProvider}:SenderName"] = "QuickPrompt"
        };
        if (provider == "BrevoMachine")
        {
            settings.Remove("QuickPrompt:Accounts:Brevo:ApiKey");
            settings["QuickPrompt:Accounts:Brevo:AuthMode"] = "ClientCredentials";
            settings["QuickPrompt:Accounts:Brevo:ClientId"] = "synthetic-machine-client";
            settings["QuickPrompt:Accounts:Brevo:ClientSecret"] = "synthetic-machine-secret";
        }
        if (provider == "Mailjet") settings["QuickPrompt:Accounts:Mailjet:ApiSecret"] = "Synthetic-secret-not-a-real-secret";
        var services = new ServiceCollection();
        services.AddAccountMail(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new Production(), new("https://receipt.example.invalid/"));
        var name = configuredProvider switch {
            "Brevo" => BrevoAccountMail.ClientName,
            "Smtp2Go" => Smtp2GoAccountMail.ClientName,
            _ => MailjetAccountMail.ClientName
        };
        services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private sealed class InterruptedHandler(string provider, Action? onRead = null) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(provider == "Brevo" ? HttpStatusCode.Created : HttpStatusCode.OK) {
                Content = new StreamContent(new InterruptedStream(onRead))
            });
        }
    }

    private sealed class InterruptedStream(Action? onRead) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { onRead?.Invoke(); throw new IOException(Canary); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { onRead?.Invoke(); return ValueTask.FromException<int>(new IOException(Canary)); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Production : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "QuickPrompt.Mail.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
