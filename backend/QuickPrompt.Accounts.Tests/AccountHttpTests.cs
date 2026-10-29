using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Cloud.Authentication;
using QuickPrompt.Cloud.Delegation;
using System.Security.Cryptography;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Synthetic-Account!79";

    [Fact]
    public async Task ConfirmedCookieAccountsReachOnlyTheirOwnLibraryOverHttp()
    {
        await using var host = await Start();
        using var a = Client(host.App);
        using var b = Client(host.App);
        using var anonymous = Client(host.App);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/accounts/v1/libraries/")).StatusCode);
        var emailA = await Register(a, host.Mail);
        var emailB = await Register(b, host.Mail);
        await Login(a, emailA);
        await Login(b, emailB);
        var libraryA = await CreateLibrary(a);
        var libraryB = await CreateLibrary(b);
        Assert.NotEqual(libraryA, libraryB);
        var created = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{libraryA}/prompts", new PromptContent("Conceptos", "Explica <tema> sin inventar.", 0));
        created.EnsureSuccessStatusCode();
        var prompt = (await created.Content.ReadFromJsonAsync<AccountPrompt>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{libraryA}/prompts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{libraryA}/prompts/{prompt.Id}/versions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/accounts/v1/libraries/{libraryA}/prompts/{prompt.Id}", new PromptContent("Ataque", "Otro", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/accounts/v1/libraries/{libraryB}/prompts/{prompt.Id}", new PromptContent("Ataque", "Otro", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/accounts/v1/libraries/", new { name = "Inyección de actor", ownerId = Guid.NewGuid() })).StatusCode);
        var mine = (await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryA}/prompts"))!;
        Assert.Equal(prompt, Assert.Single(mine));
        Assert.Empty((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryB}/prompts"))!);
    }

    [Fact]
    public async Task CsrfTokensAreBoundToClientAndUnconfirmedAccountsCannotLogin()
    {
        await using var host = await Start();
        using var a = Client(host.App);
        using var b = Client(host.App);
        var email = $"qa-{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        Assert.Empty(host.Mail.Messages);
        await Csrf(a);
        var options = await a.GetFromJsonAsync<JsonElement>("/account/options");
        Assert.False(options.GetProperty("google").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.PostAsJsonAsync("/account/google", new { })).StatusCode);
        b.DefaultRequestHeaders.Add("X-QP-CSRF", a.DefaultRequestHeaders.GetValues("X-QP-CSRF").Single());
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.PostAsJsonAsync("/account/login", new Login(email, Password))).StatusCode);
        using var badOrigin = new HttpRequestMessage(HttpMethod.Get, "/account/csrf");
        badOrigin.Headers.Add("Origin", "https://another.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await a.SendAsync(badOrigin)).StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("https://another.example")]
    public async Task NativeFormsKeepAnOriginPreservingReferrerPolicyAndRejectUntrustedOrigins(string origin)
    {
        await using var host = await Start();
        using var client = Client(host.App);
        var response = await client.GetAsync("/account/options");
        Assert.Equal("strict-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        await Csrf(client);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/account/google");
        invalid.Headers.Add("Origin", origin);
        invalid.Content = new FormUrlEncodedContent([]);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(invalid)).StatusCode);
    }

    [Fact]
    public async Task RepeatedWrongPasswordsLockTheRealIdentityAccount()
    {
        await using var host = await Start();
        using var client = Client(host.App);
        var email = await Register(client, host.Mail);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/account/login", new Login(email, "Wrong-Synthetic!79"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/account/login", new Login(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/account/me")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountHtmlAndIndexCarryAccountModeWithoutTurningUnknownApiRoutesIntoHtml(bool alreadyAccounts)
    {
        var root = Path.Combine(Path.GetTempPath(), "quickprompt-static-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        const string accountMode = "<meta name=\"quickprompt-mode\" content=\"accounts\" />";
        await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "<html><head>" + (alreadyAccounts ? accountMode : "") + "<title>QA</title></head><body>QA</body></html>");
        await File.WriteAllTextAsync(Path.Combine(root, "assets", "test.js"), "/* synthetic */");
        try
        {
            await using var host = await Start(root);
            using var client = Client(host.App);
            foreach (var path in new[] { "/", "/index.html", "/account/confirm", "/account/reset", "/connect/verify" })
            {
                var page = await client.GetStringAsync(path);
                Assert.Contains(accountMode, page);
                Assert.Equal(2, page.Split(accountMode, StringSplitOptions.None).Length);
            }
            Assert.Equal("/* synthetic */", await client.GetStringAsync("/assets/test.js"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/prompts")).StatusCode);
        }
        finally
        {
            File.Delete(Path.Combine(root, "index.html"));
            File.Delete(Path.Combine(root, "assets", "test.js"));
            Directory.Delete(Path.Combine(root, "assets"));
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task AnotherLocalOriginCannotReuseASessionEvenIfItsCookieIsRenamed()
    {
        await using var first = await Start();
        await using var second = await Start(origin: "https://127.0.0.1:5300");
        using var a = Client(first.App);
        using var b = Client(second.App, "https://127.0.0.1:5300");
        using var foreign = Client(second.App, "https://127.0.0.1:5300");
        var email = await Register(a, first.Mail);
        var cookieA = await Login(a, email);
        await Csrf(b);
        var cookieB = await Login(b, email);
        var pairA = cookieA.Split(';')[0];
        var nameB = cookieB.Split('=')[0];
        Assert.NotEqual(pairA.Split('=')[0], nameB);
        using var original = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        original.Headers.Add("Cookie", pairA);
        Assert.Equal(HttpStatusCode.Unauthorized, (await foreign.SendAsync(original)).StatusCode);
        using var renamed = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        renamed.Headers.Add("Cookie", nameB + pairA[pairA.IndexOf('=')..]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await foreign.SendAsync(renamed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/account/me")).StatusCode);
    }

    [Fact]
    public async Task ResetIsSingleUseAndRevokesExistingSessionsThenLogoutAllRevokesOtherSession()
    {
        await using var host = await Start();
        using var a = Client(host.App);
        using var second = Client(host.App);
        using var resetClient = Client(host.App);
        var email = await Register(a, host.Mail);
        await Login(a, email);
        await Csrf(second);
        await Login(second, email);
        await Csrf(resetClient);
        Assert.Equal(HttpStatusCode.Accepted, (await resetClient.PostAsJsonAsync("/account/recover", new RecoveryRequest(email))).StatusCode);
        var token = Parse(host.Mail.Messages.Single(x => x.Email == email && x.Purpose == "reset"));
        var recovery = new PasswordRecovery(token.UserId, token.Code, "Changed-Synthetic!81");
        Assert.Equal(HttpStatusCode.NoContent, (await resetClient.PostAsJsonAsync("/account/reset", recovery)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await resetClient.PostAsJsonAsync("/account/reset", recovery)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/account/me")).StatusCode);
        await Csrf(a);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.PostAsJsonAsync("/account/login", new Login(email, Password))).StatusCode);
        await Login(a, email, recovery.Password);
        await Csrf(second);
        await Login(second, email, recovery.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/account/logout-all", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/account/me")).StatusCode);
    }

    [Theory]
    [InlineData("register")]
    [InlineData("recover")]
    [InlineData("resend")]
    public async Task ProviderFailureDoesNotRevealWhetherAnAccountExists(string operation)
    {
        var deliveryLog = new MailFailureLog();
        await using var host = await Start(configureServices: services => {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddLogging(logging => logging.AddProvider(deliveryLog));
        });
        using var client = Client(host.App);
        var existing = $"qa-{Guid.NewGuid():N}@example.invalid";
        var missing = $"qa-{Guid.NewGuid():N}@example.invalid";
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/register", new Registration(existing, Password))).StatusCode);
        if (operation == "recover")
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/confirm", Parse(host.Mail.Messages.Single()))).StatusCode);
        host.Mail.DeliveryFailure = new AccountMailDeliveryException("MAIL_UNAVAILABLE");
        async Task<HttpResponseMessage> Request(string email) => operation == "register"
            ? await client.PostAsJsonAsync("/account/register", new Registration(email, Password))
            : await client.PostAsJsonAsync("/account/" + operation, new RecoveryRequest(email));
        using var existingResponse = await Request(existing);
        using var missingResponse = await Request(missing);
        Assert.Equal(HttpStatusCode.Accepted, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, missingResponse.StatusCode);
        Assert.Equal(await existingResponse.Content.ReadAsStringAsync(), await missingResponse.Content.ReadAsStringAsync());
        var diagnostic = Assert.Single(deliveryLog.Messages);
        Assert.Contains("MAIL_UNAVAILABLE", diagnostic);
        Assert.DoesNotContain(existing, diagnostic); Assert.DoesNotContain(missing, diagnostic);
        foreach (var message in host.Mail.Messages) Assert.DoesNotContain(message.Link, diagnostic);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("reset")]
    public async Task RealIdentityTokensExpireWithoutConfirmingOrChangingThePassword(string purpose)
    {
        await using var host = await Start(configureServices: services => {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromSeconds(1));
        });
        using var client = Client(host.App);
        var email = $"qa-{Guid.NewGuid():N}@example.invalid";
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        var confirmation = Parse(host.Mail.Messages.Single());
        if (purpose == "reset")
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/confirm", confirmation)).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/recover", new RecoveryRequest(email))).StatusCode);
        }
        var expired = Parse(host.Mail.Messages.Single(message => message.Purpose == purpose));
        // Uses the real Identity provider and a positive fixture lifetime. No clock
        // rewriting, custom token decoder or production token settings are involved.
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        using var response = purpose == "confirm"
            ? await client.PostAsJsonAsync("/account/confirm", expired)
            : await client.PostAsJsonAsync("/account/reset", new PasswordRecovery(expired.UserId, expired.Code, "Changed-Synthetic!81"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        if (purpose == "confirm")
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/account/login", new Login(email, Password))).StatusCode);
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/account/login", new Login(email, "Changed-Synthetic!81"))).StatusCode);
            await Login(client, email);
        }
    }

    private async Task<RunningHost> Start(string? webRoot = null, string origin = "https://127.0.0.1:5299", bool delegation = true, bool mcp = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var mail = new CapturedMail();
        var protocolLog = new SafeProtocolLog();
        var app = AccountHost.Build([], builder =>
        {
            builder.Environment.EnvironmentName = "Development";
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickPrompt:Accounts:Origin"] = origin,
                ["QuickPrompt:Delegation:Enabled"] = (delegation && origin.StartsWith("https:", StringComparison.Ordinal)).ToString(),
                ["ConnectionStrings:Accounts"] = postgres.ApplicationConnection,
                ["ConnectionStrings:Executions"] = postgres.ExecutionConnection,
                ["QuickPrompt:WebRoot"] = webRoot,
                ["QuickPrompt:Mcp:RedirectUri"] = mcp ? "https://client.example/callback" : null
            });
        }, services => { services.AddSingleton<IAccountMail>(mail); services.AddSingleton<DelegationKeys>(_ => TestDelegationKeys()); services.AddLogging(logging => logging.AddProvider(protocolLog)); configureServices?.Invoke(services); });
        await app.StartAsync();
        return new(app, mail, protocolLog);
    }

    private static HttpClient Client(WebApplication app, string origin = "https://127.0.0.1:5299")
    {
        var client = new HttpClient(new CookieHandler(app.GetTestServer().CreateHandler())) { BaseAddress = new Uri(origin) };
        return client;
    }
    private static async Task Csrf(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<JsonElement>("/account/csrf");
        client.DefaultRequestHeaders.Remove("X-QP-CSRF");
        client.DefaultRequestHeaders.Add("X-QP-CSRF", result.GetProperty("token").GetString());
    }
    private static async Task<string> Register(HttpClient client, CapturedMail mail)
    {
        var email = $"qa-{Guid.NewGuid():N}@example.invalid";
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/account/register", new Registration(email, Password))).StatusCode);
        var confirmation = Parse(mail.Messages.Single(x => x.Email == email && x.Purpose == "confirm"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/confirm", confirmation)).StatusCode);
        return email;
    }
    private static async Task<string> Login(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/account/login", new Login(email, password));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("qp.accounts.", StringComparison.Ordinal));
        Assert.True(cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase), "Session cookie must be HttpOnly.");
        Assert.True(cookie.Contains("secure", StringComparison.OrdinalIgnoreCase), "HTTPS session cookie must be Secure.");
        await Csrf(client);
        return cookie;
    }
    private static TokenConfirmation Parse(AccountMail mail)
    {
        var query = QueryHelpers.ParseQuery(new Uri(mail.Link).Query);
        return new(Guid.Parse(query["userId"].ToString()), query["code"].ToString());
    }
    private static async Task<Guid> CreateLibrary(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/accounts/v1/libraries/", new LibraryName("Conceptos"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private sealed class CapturedMail : IAccountMail
    {
        public ConcurrentBag<AccountMail> Messages { get; } = [];
        internal AccountMailDeliveryException? DeliveryFailure { get; set; }
        public bool Available => true;
        public Task Send(AccountMail message, CancellationToken cancellation)
        {
            Messages.Add(message);
            return DeliveryFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }
    }
    private sealed class MailFailureLog : ILoggerProvider, ILogger
    {
        internal ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Dispose() { }
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (eventId.Name == "AccountMailUnconfirmed") Messages.Add(formatter(state, exception)); }
    }
    private sealed record RunningHost(WebApplication App, CapturedMail Mail, SafeProtocolLog ProtocolLog) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }
    private sealed class CookieHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private readonly CookieContainer cookies = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var header = cookies.GetCookieHeader(request.RequestUri!);
            if (header.Length > 0) request.Headers.Add("Cookie", header);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var value in values) cookies.SetCookies(request.RequestUri!, value);
            return response;
        }
    }
}
