using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Cloud.Authentication;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Theory]
    [InlineData("register")]
    [InlineData("recover")]
    [InlineData("resend")]
    public async Task ProviderFailureKeepsTheSamePublicResponseForExistingAndMissingAccounts(string operation)
    {
        var mail = new RejectableMail();
        await using var host = await Start(configureServices: services => services.AddSingleton<IAccountMail>(mail));
        using var client = Client(host.App);
        var existing = $"qa-{Guid.NewGuid():N}@example.invalid";
        var missing = $"qa-{Guid.NewGuid():N}@example.invalid";
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/register", new Registration(existing, Password))).StatusCode);
        if (operation == "recover")
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/confirm", Parse(mail.Messages.Single()))).StatusCode);
        mail.Reject = true;
        Task<HttpResponseMessage> Request(string email) => operation == "register"
            ? client.PostAsJsonAsync("/account/register", new Registration(email, Password))
            : client.PostAsJsonAsync("/account/" + operation, new RecoveryRequest(email));
        using var known = await Request(existing);
        using var unknown = await Request(missing);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.DoesNotContain(existing, await known.Content.ReadAsStringAsync());
        Assert.DoesNotContain(missing, await unknown.Content.ReadAsStringAsync());
    }

    private sealed class RejectableMail : IAccountMail
    {
        public bool Available => true;
        public bool Reject { get; set; }
        public ConcurrentBag<AccountMail> Messages { get; } = [];
        public Task Send(AccountMail message, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Reject) throw new AccountMailDeliveryException("MAIL_REJECTED");
            Messages.Add(message); return Task.CompletedTask;
        }
    }
}
