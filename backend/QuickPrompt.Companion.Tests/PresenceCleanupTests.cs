using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Companion;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Companion.Tests;

public sealed class PresenceCleanupTests
{
    [Theory]
    [InlineData("io", "completed")]
    [InlineData("access", "completed")]
    [InlineData("crypto", "completed")]
    [InlineData("io", "cancelled")]
    [InlineData("access", "cancelled")]
    [InlineData("crypto", "cancelled")]
    [InlineData("io", "failed")]
    [InlineData("access", "failed")]
    [InlineData("crypto", "failed")]
    public async Task ExpectedLocalWithdrawalFailureCannotReplaceThePrimaryRunOutcome(string failure, string outcome)
    {
        await using var fixture = new CleanupFixture(failure);
        var model = new UnusedModel();
        var runner = new CompanionRunner(fixture.Connection, model);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") cancellation.Cancel();
        if (outcome == "completed") Assert.Equal(CompanionRunEnd.JobLimit, await runner.Run(0, cancellation.Token));
        else if (outcome == "cancelled")
        {
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.Run(1, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        else Assert.Same(fixture.PrimaryFailure, await Assert.ThrowsAsync<InvalidDataException>(() => runner.Run(1, cancellation.Token)));
        Assert.Equal(1, fixture.Withdrawals);
        Assert.Equal(outcome == "failed" ? 1 : 0, fixture.Claims);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task UnexpectedWithdrawalFailuresRemainVisible()
    {
        await using var fixture = new CleanupFixture("unexpected");
        await Assert.ThrowsAsync<NotSupportedException>(() => new CompanionRunner(fixture.Connection, new UnusedModel()).Run(0, default));
        Assert.Equal(1, fixture.Withdrawals);
    }

    private sealed class UnusedModel : IAgentModelTransport
    {
        public int Calls { get; private set; }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        { Calls++; throw new InvalidOperationException("No model call is allowed in this synthetic cleanup test."); }
    }

    /// <summary>Only replaces the sealed client's HTTP transport, token and clock fields; never pairs, refreshes or stores a credential.</summary>
    private sealed class CleanupFixture : HttpMessageHandler, IAsyncDisposable
    {
        private readonly string directory;
        private readonly string failure;
        public CompanionConnection Connection { get; }
        public InvalidDataException PrimaryFailure { get; } = new("Synthetic primary failure.");
        public int Withdrawals { get; private set; }
        public int Claims { get; private set; }
        public CleanupFixture(string failure)
        {
            this.failure = failure;
            var options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N")]);
            var purpose = SHA256.HashData(Encoding.UTF8.GetBytes("QuickPrompt.Companion/1|" + options.Origin.AbsoluteUri + "|" + options.Slot));
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion", Convert.ToHexString(purpose));
            Connection = new(options, new CompanionSecrets(options));
            if (Field("http").GetValue(Connection) is not HttpClient originalClient) throw new InvalidOperationException("The owned client must exist.");
            originalClient.Dispose();
            Field("http").SetValue(Connection, new HttpClient(this, disposeHandler: false) { BaseAddress = options.Origin });
            Field("accessToken").SetValue(Connection, "synthetic-not-a-credential");
            Field("expiresAt").SetValue(Connection, DateTimeOffset.MaxValue);
            Field("lastPresenceObservation").SetValue(Connection, Guid.NewGuid().ToString());
        }
        private static FieldInfo Field(string name) => typeof(CompanionConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The synthetic transport seam changed.");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("example.invalid", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath.EndsWith("/claim", StringComparison.Ordinal)) { Claims++; throw PrimaryFailure; }
            Assert.Equal(CompanionPresenceProtocol.StopPath, request.RequestUri.AbsolutePath);
            Withdrawals++;
            throw failure switch
            {
                "io" => new IOException("Synthetic local persistence failure."),
                "access" => new UnauthorizedAccessException("Synthetic local persistence failure."),
                "crypto" => new CryptographicException("Synthetic local persistence failure."),
                _ => new NotSupportedException("Synthetic unexpected failure.")
            };
        }
        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            var parent = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "companion"));
            var target = Path.GetFullPath(directory);
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || new DirectoryInfo(target).LinkTarget is not null) throw new IOException("Synthetic cleanup escaped its owned directory.");
            Directory.Delete(target, recursive: true);
            Dispose();
        }
    }
}
