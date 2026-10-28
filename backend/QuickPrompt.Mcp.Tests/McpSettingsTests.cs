using Microsoft.Extensions.Configuration;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

public sealed class McpSettingsTests
{
    [Theory]
    [InlineData("https://remote.invalid")]
    [InlineData("http://127.0.0.1:5277/api")]
    [InlineData("http://user:password@127.0.0.1:5277")]
    [InlineData("http://127.0.0.1:5277/?library=other")]
    [InlineData("http://127.0.0.1:5277/#other")]
    public void ToolHostCannotTargetRemoteServersOrOtherLibraryPaths(string endpoint)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["QuickPromptMcp:ApiBaseUrl"] = endpoint }).Build();
        Assert.Throws<InvalidOperationException>(() => McpSettings.Read(config));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("a credential that contains spaces even when long")]
    public void HttpFailsClosedWithoutStrongCredential(string? token)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["QuickPromptMcp:Transport"] = "http", ["QuickPromptMcp:HttpToken"] = token }).Build();
        Assert.Throws<InvalidOperationException>(() => McpSettings.Read(config));
    }

    [Fact]
    public void DefaultIsPrivateStdioAndReadOnly()
    {
        var settings = McpSettings.Read(new ConfigurationBuilder().Build());
        Assert.Equal("stdio", settings.Transport); Assert.False(settings.AllowCreate);
    }
}
