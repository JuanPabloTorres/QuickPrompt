using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Cloud.Delegation;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private sealed class SafeProtocolLog : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> reasons = new();
        public string Summary => string.Join(",", reasons);
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Dispose() { }
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id != 5285 || state is not IEnumerable<KeyValuePair<string, object?>> values) return;
            foreach (var value in values.Where(x => x.Key == "Reason"))
                if (value.Value is string reason && reason.All(c => c is >= 'A' and <= 'Z' or '_')) reasons.Enqueue(reason);
        }
    }
    private static readonly Lazy<RSAParameters> SigningParameters = new(() => { using var key = RSA.Create(3072); return key.ExportParameters(true); });
    private static readonly Lazy<RSAParameters> EncryptionParameters = new(() => { using var key = RSA.Create(3072); return key.ExportParameters(true); });
    private static DelegationKeys TestDelegationKeys()
    {
        var signing = RSA.Create(); signing.ImportParameters(SigningParameters.Value);
        var encryption = RSA.Create(); encryption.ImportParameters(EncryptionParameters.Value);
        return new(signing, encryption);
    }
    private sealed record DeviceDocument(string DeviceCode, string UserCode, string VerificationUri, string VerificationUriComplete, int ExpiresIn, int Interval);
    private sealed record TokenDocument(string AccessToken, string RefreshToken);
    private static async Task<DeviceDocument> BeginDevice(HttpClient client, string label = "QA local", string profile = DelegationPolicy.Profile)
    {
        var response = await client.PostAsync("/connect/device", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = DelegationPolicy.ClientId,
            ["scope"] = LocalCompanionProfiles.Scope(profile) + " offline_access",
            ["resource"] = new Uri(client.BaseAddress!, LocalCompanionProfiles.ResourcePath(profile)).AbsoluteUri,
            ["device_name"] = label,
            ["profile_id"] = profile
        }));
        await AssertProtocolSuccess(response, "Device authorization");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(body.GetProperty("device_code").GetString()!, body.GetProperty("user_code").GetString()!, body.GetProperty("verification_uri").GetString()!,
            body.GetProperty("verification_uri_complete").GetString()!, body.GetProperty("expires_in").GetInt32(), body.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5);
    }
    private static Task<HttpResponseMessage> VerifyDevice(HttpClient cookie, DeviceDocument device, string action) => cookie.PostAsync("/connect/verify", new FormUrlEncodedContent(
        new Dictionary<string, string> { ["user_code"] = device.UserCode, ["action"] = action }));
    private static async Task<TokenDocument> RedeemDevice(HttpClient client, DeviceDocument device, SafeProtocolLog? log = null, string profile = DelegationPolicy.Profile)
    {
        var response = await Token(client, "urn:ietf:params:oauth:grant-type:device_code", "device_code", device.DeviceCode, profile);
        await AssertProtocolSuccess(response, "Device redemption " + log?.Summary);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!);
    }
    private static Task<HttpResponseMessage> Token(HttpClient client, string grant, string parameter, string token, string profile = DelegationPolicy.Profile) => client.PostAsync("/connect/token", new FormUrlEncodedContent(
        new Dictionary<string, string>
        {
            ["client_id"] = DelegationPolicy.ClientId,
            ["grant_type"] = grant,
            [parameter] = token,
            ["resource"] = new Uri(client.BaseAddress!, LocalCompanionProfiles.ResourcePath(profile)).AbsoluteUri
        }));

    private static async Task AssertProtocolSuccess(HttpResponseMessage response, string stage)
    {
        var error = "none";
        var diagnostic = "none";
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            try
            {
                var body = JsonSerializer.Deserialize<JsonElement>(content); if (body.TryGetProperty("error", out var code)
                && code.GetString() is { } value && value.All(c => c is >= 'a' and <= 'z' or '_')) error = value;
            }
            catch (JsonException) { }
            // The SDK's local verification errors use plain text, not the token endpoint's JSON.
            var match = System.Text.RegularExpressions.Regex.Match(content, @"(?m)^error:([a-z_]+)\r?$");
            if (match.Success) error = match.Groups[1].Value;
            var reference = System.Text.RegularExpressions.Regex.Match(content, @"https://documentation\.openiddict\.com/errors/(ID[0-9]{4})");
            if (reference.Success) diagnostic = reference.Groups[1].Value;
        }
        Assert.True(response.IsSuccessStatusCode, $"{stage}: status={(int)response.StatusCode}; error={error}; reference={diagnostic}; sensitive response omitted.");
    }

    [Fact]
    public async Task DeviceConsentIssuesOnlyItsProfileAndRevocationBlocksAccessAndRefresh()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var other = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var pending = await BeginDevice(device);
        var review = await VerifyDevice(owner, pending, "review");
        Assert.True(review.IsSuccessStatusCode, "A logged-in owner with CSRF must be able to review this code.");
        var reviewed = await review.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(DelegationPolicy.Model, reviewed.GetProperty("model").GetString());
        var approval = await VerifyDevice(owner, pending, "approve");
        var approvedCount = (await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!.Length;
        await AssertProtocolSuccess(approval, $"Explicit approval; stored connections={approvedCount}");
        var token = await RedeemDevice(device, pending, host.ProtocolLog);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var profile = await device.GetAsync("/api/companion/v1/profile");
        Assert.True(profile.IsSuccessStatusCode, "An approved token must reach its own companion audience.");
        Assert.True((await profile.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispatchReady").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/account/me")).StatusCode);
        var connections = (await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!;
        var connection = Assert.Single(connections); Assert.Empty((await other.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!);
        Assert.True(connection.Active);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{connection.Id}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/companion/v1/profile")).StatusCode);
        Assert.False((await Token(device, "refresh_token", "refresh_token", token.RefreshToken)).IsSuccessStatusCode);
        Assert.False(Assert.Single((await owner.GetFromJsonAsync<ConnectionView[]>("/account/connections/"))!).Active);
    }
}
