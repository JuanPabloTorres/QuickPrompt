using System.Net;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

public sealed class LocalEntrySecurityTests(LibraryProcess library) : IClassFixture<LibraryProcess>
{
    [Theory]
    [InlineData("/", "GET", "navigate", "document", 200)]
    [InlineData("/", "GET", "cors", "empty", 403)]
    [InlineData("/", "GET", "navigate", "iframe", 403)]
    [InlineData("/", "POST", "navigate", "document", 403)]
    [InlineData("/api/v1/prompts", "GET", "navigate", "document", 403)]
    [InlineData("/health", "GET", "navigate", "document", 403)]
    public async Task CrossSiteAllowsOnlyStaticTopLevelEntry(string path, string method, string mode, string destination, int status)
    {
        using var http = new HttpClient { BaseAddress = library.BaseUrl };
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        request.Headers.Add("Sec-Fetch-Mode", mode); request.Headers.Add("Sec-Fetch-Dest", destination);
        using var response = await http.SendAsync(request);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 200)
        {
            Assert.Contains("synthetic entry", await response.Content.ReadAsStringAsync());
            Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
        else Assert.Equal("CROSS_SITE_REQUEST", response.Headers.GetValues("X-QuickPrompt-Rejection").Single());
    }

    [Theory]
    [InlineData("origin", "ORIGIN_MISMATCH")]
    [InlineData("host", "HOST_MISMATCH")]
    [InlineData("write", "CLIENT_MARKER_REQUIRED")]
    public async Task NavigationExceptionPreservesOriginHostAndWriteGuards(string caseName, string reason)
    {
        using var http = new HttpClient { BaseAddress = library.BaseUrl };
        using var request = new HttpRequestMessage(caseName == "write" ? HttpMethod.Post : HttpMethod.Get, "/");
        request.Headers.Add("Sec-Fetch-Mode", "navigate"); request.Headers.Add("Sec-Fetch-Dest", "document");
        if (caseName == "origin") request.Headers.Add("Origin", "https://foreign.invalid");
        if (caseName == "host") request.Headers.Host = "foreign.invalid";
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(reason, response.Headers.GetValues("X-QuickPrompt-Rejection").Single());
    }
}
