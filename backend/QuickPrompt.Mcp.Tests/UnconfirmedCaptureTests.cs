using System.Net;
using System.Text;
using ModelContextProtocol;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

public sealed class UnconfirmedCaptureTests
{
    [Theory]
    [InlineData("disconnect")]
    [InlineData("truncated")]
    [InlineData("mismatched-receipt")]
    public async Task LostOrInvalidReceiptNeverReportsSuccessOrAutomaticallyRepeatsTheWrite(string mode)
    {
        using var handler = new BrokenReply(mode);
        using var http = new HttpClient(handler) { BaseAddress = new("http://127.0.0.1:5277") };
        var client = new QuickPromptApiClient(http);
        var error = await Assert.ThrowsAsync<McpException>(() => client.Capture(
            new(Guid.NewGuid(), new("Título", "Texto literal")), CancellationToken.None));
        Assert.StartsWith("CAPTURE_UNCONFIRMED:", error.Message);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class BrokenReply(string mode) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (mode == "disconnect") throw new HttpRequestException("Injected connection loss; the write may have completed.");
            var body = mode == "truncated" ? "{\"operationId\":" : "{\"operationId\":\"00000000-0000-0000-0000-000000000000\",\"prompt\":null}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
