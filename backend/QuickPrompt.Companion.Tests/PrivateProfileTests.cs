using System.Security.Cryptography;
using QuickPrompt.Companion;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Companion.Tests;

public sealed class PrivateProfileTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5301/")]
    [InlineData("https://name:credential@example.invalid/")]
    [InlineData("https://example.invalid/another-path")]
    public void ServerOriginCannotDisableHttpsOrContainCredentialsOrPaths(string origin) =>
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(["status", "--server", origin]));

    [Theory]
    [InlineData("http://192.0.2.1:11434/")]
    [InlineData("https://127.0.0.1:11434/")]
    [InlineData("http://127.0.0.1:11434/api/chat")]
    public void ModelEndpointRemainsLocalAndCannotSpecifyAProviderPath(string endpoint) =>
        Assert.Throws<ValidationException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--endpoint", endpoint]));

    [Fact]
    public void PrivateProfileHasExclusiveOwnershipAndCannotSubstituteAnotherOrigin()
    {
        var options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N")]);
        var secrets = new CompanionSecrets(options);
        using var held = secrets.Acquire();
        Assert.Throws<IOException>(() => secrets.Acquire());
        var expected = new CompanionSession(options.Origin.AbsoluteUri, Guid.NewGuid(), "synthetic-refresh-only");
        try
        {
            secrets.Save(expected); Assert.Equal(expected, secrets.Load(options.Origin.AbsoluteUri));
            Assert.Throws<CryptographicException>(() => secrets.Load("https://different.example.invalid/"));
        }
        finally { secrets.Forget(); }
        Assert.False(secrets.Exists);
    }
}
