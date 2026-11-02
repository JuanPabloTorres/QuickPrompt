using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Theory]
    [InlineData("login")]
    [InlineData("register")]
    [InlineData("recover")]
    [InlineData("resend")]
    public async Task DirectAccountEntryRoutesServeTheAccountUi(string route)
    {
        var root = Path.Combine(Path.GetTempPath(), "quickprompt-account-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "<html><head></head><body>synthetic-account-ui</body></html>");
            await using var host = await Start(webRoot: root);
            using var client = Client(host.App);
            var response = await client.GetAsync("/account/" + route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("<meta name=\"quickprompt-mode\" content=\"accounts\" />", await response.Content.ReadAsStringAsync());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ResentConfirmationActivatesOnlyAnUnconfirmedAccountWithoutRevealingExistence()
    {
        await using var host = await Start();
        using var client = Client(host.App);
        var email = $"qa-{Guid.NewGuid():N}@example.invalid";
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        var beforeMessages = host.Mail.Messages.ToArray();
        var before = beforeMessages.Length;
        var resent = await client.PostAsJsonAsync("/account/resend", new RecoveryRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
        Assert.Equal(before + 1, host.Mail.Messages.Count);
        var token = Parse(host.Mail.Messages.Single(message => !beforeMessages.Any(previous => ReferenceEquals(previous, message))));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/confirm", token)).StatusCode);
        var confirmed = await client.PostAsJsonAsync("/account/resend", new RecoveryRequest(email));
        var missing = await client.PostAsJsonAsync("/account/resend", new RecoveryRequest($"qa-{Guid.NewGuid():N}@example.invalid"));
        Assert.Equal(HttpStatusCode.Accepted, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, missing.StatusCode);
        Assert.Equal(await resent.Content.ReadAsStringAsync(), await confirmed.Content.ReadAsStringAsync());
        Assert.Equal(await resent.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        Assert.Equal(before + 1, host.Mail.Messages.Count);
        await Login(client, email);
    }
}
