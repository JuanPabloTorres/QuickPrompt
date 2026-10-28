using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using QuickPrompt.Nova.Application;
using static OpenIddict.Client.OpenIddictClientModels;

namespace QuickPrompt.Companion;

public sealed class CompanionRequestException(HttpStatusCode status) : Exception("El servidor no aceptó la operación del componente local.")
{ public HttpStatusCode Status { get; } = status; }

/// <summary>OpenIddict owns OAuth. This client owns its fixed queue HTTP contract and DPAPI persistence, never account credentials.</summary>
public sealed class CompanionConnection : IAsyncDisposable
{
    private readonly CompanionOptions options;
    private readonly CompanionSecrets secrets;
    private readonly ServiceProvider services;
    private readonly OpenIddictClientService oauth;
    private readonly HttpClient http;
    private CompanionSession? session;
    private string? accessToken;
    private DateTimeOffset expiresAt;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 64 };
    public CompanionConnection(CompanionOptions options, CompanionSecrets secrets)
    {
        this.options = options; this.secrets = secrets;
        var builder = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
        builder.AddOpenIddict().AddClient(client =>
        {
            client.AllowDeviceAuthorizationFlow().AllowRefreshTokenFlow().DisableTokenStorage();
            client.UseSystemNetHttp().ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(15))
                .ConfigureHttpClientHandler(handler => { handler.AllowAutoRedirect = false; handler.UseProxy = false; });
            client.AddRegistration(new OpenIddictClientRegistration
            {
                RegistrationId = "quickprompt-local",
                Issuer = options.Origin,
                ClientId = LocalWorkProtocol.ClientId,
                Scopes = { options.Scope, OpenIddictConstants.Scopes.OfflineAccess }
            });
        });
        services = builder.BuildServiceProvider(); oauth = services.GetRequiredService<OpenIddictClientService>();
        http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
        { BaseAddress = options.Origin, Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 512000 };
    }
    public async Task Pair(Action<Uri, string> showCode, CancellationToken cancellation)
    {
        if (secrets.Exists) throw new InvalidOperationException("Este perfil ya tiene una vinculación. Usa status o disconnect antes de vincular otra cuenta.");
        var challenge = await oauth.ChallengeUsingDeviceAsync(DeviceRequest(cancellation));
        if (challenge.VerificationUri.GetLeftPart(UriPartial.Authority) != options.Origin.GetLeftPart(UriPartial.Authority)
            || challenge.VerificationUri.AbsolutePath != "/connect/verify" || challenge.ExpiresIn <= TimeSpan.Zero || challenge.ExpiresIn > TimeSpan.FromMinutes(10))
            throw new InvalidOperationException("El servidor entregó un flujo de vinculación incompatible.");
        showCode(challenge.VerificationUri, challenge.UserCode);
        var result = await oauth.AuthenticateWithDeviceAsync(new DeviceAuthenticationRequest
        {
            RegistrationId = "quickprompt-local",
            DeviceCode = challenge.DeviceCode,
            Interval = challenge.Interval,
            Timeout = challenge.ExpiresIn,
            DisableUserInfo = true,
            CancellationToken = cancellation,
            // The SDK also excludes Resources when the grant is device_code.
            AdditionalTokenRequestParameters = new() { [OpenIddictConstants.Parameters.Resource] = options.Resource }
        });
        accessToken = result.AccessToken; expiresAt = result.AccessTokenExpirationDate ?? DateTimeOffset.UtcNow.AddMinutes(2);
        var profile = await Profile(cancellation);
        session = new(options.Origin.AbsoluteUri, profile.ConnectionId, result.RefreshToken ?? throw new InvalidOperationException("No se concedió renovación privada."), options.Profile);
        secrets.Save(session);
    }
    private DeviceChallengeRequest DeviceRequest(CancellationToken cancellation) => new()
    {
        RegistrationId = "quickprompt-local",
        CancellationToken = cancellation,
        Scopes = [options.Scope, OpenIddictConstants.Scopes.OfflineAccess],
        // In OpenIddict 7.7.2 AttachDeviceAuthorizationRequestParameters forwards
        // Scopes, not ProcessChallengeContext.Resources. Supply the RFC 8707
        // extension explicitly on the device wire request; authority is still the
        // configured issuer resource, never a task/model argument.
        AdditionalDeviceAuthorizationRequestParameters = new()
        {
            [OpenIddictConstants.Parameters.Resource] = options.Resource,
            ["device_name"] = options.Label,
            ["profile_id"] = options.Profile
        }
    };
    public async Task Connect(CancellationToken cancellation)
    {
        session = secrets.Load(options.Origin.AbsoluteUri, options.Profile);
        await Refresh(cancellation);
    }
    private async Task Refresh(CancellationToken cancellation)
    {
        var current = session ?? throw new InvalidOperationException("Vincula primero este perfil local.");
        var result = await oauth.AuthenticateWithRefreshTokenAsync(new RefreshTokenAuthenticationRequest
        {
            RegistrationId = "quickprompt-local",
            RefreshToken = current.RefreshToken,
            Resources = [options.Resource],
            DisableUserInfo = true,
            CancellationToken = cancellation
        });
        accessToken = result.AccessToken; expiresAt = result.AccessTokenExpirationDate ?? DateTimeOffset.UtcNow.AddMinutes(2);
        var profile = await Profile(cancellation);
        if (profile.ConnectionId != current.ConnectionId) throw new InvalidOperationException("La renovación no corresponde a la vinculación guardada.");
        session = current with { RefreshToken = result.RefreshToken ?? current.RefreshToken }; secrets.Save(session);
    }
    public async Task<LocalCompanionProfile> Profile(CancellationToken cancellation)
    {
        using var response = await Send(HttpMethod.Get, "/api/companion/v1/profile", null, cancellation);
        var profile = await response.Content.ReadFromJsonAsync<LocalCompanionProfile>(Json, cancellation) ?? throw new InvalidOperationException("Falta el perfil local.");
        if (profile.ConnectionId == Guid.Empty || profile.ProfileId != options.Profile || profile.Model != LocalWorkProtocol.Model
            || profile.WorkProtocol != LocalCompanionProfiles.Protocol(options.Profile) || !profile.DispatchReady) throw new InvalidOperationException("El servidor no admite este protocolo local.");
        return profile;
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken ?? throw new InvalidOperationException("No hay acceso activo."));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        var response = await http.SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode) { var status = response.StatusCode; response.Dispose(); throw new CompanionRequestException(status); }
        return response;
    }
    private async Task<HttpResponseMessage> Work(string path, object? body, CancellationToken cancellation)
    {
        if (expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) await Refresh(cancellation);
        return await Send(HttpMethod.Post, path, body, cancellation);
    }
    public async Task<LocalWorkLease?> Claim(CancellationToken cancellation)
    {
        using var response = await Work("/api/companion/v1/work/claim", new { }, cancellation);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        var work = await response.Content.ReadFromJsonAsync<LocalWorkLease>(Json, cancellation) ?? throw new InvalidOperationException();
        if (work.Id == Guid.Empty || work.Token == Guid.Empty || work.Model != LocalWorkProtocol.Model || work.Instruction is not { Length: > 0 and <= 64000 }
            || work.LeaseSeconds is < 5 or > 120 || work.MaximumSeconds is < 1 or > 600) throw new InvalidOperationException("El trabajo excede el perfil permitido.");
        return work;
    }
    public async Task<LocalLeasePulse> Heartbeat(LocalWorkLease lease, CancellationToken cancellation)
    {
        using var response = await Work($"/api/companion/v1/work/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token), cancellation);
        return await response.Content.ReadFromJsonAsync<LocalLeasePulse>(Json, cancellation) ?? throw new InvalidOperationException();
    }
    public async Task Finish(LocalWorkLease lease, string? answer, string? error, CancellationToken cancellation)
    {
        using var response = await Work($"/api/companion/v1/work/{lease.Id}/finish", new LocalWorkReceipt(lease.Token, answer, error), cancellation);
    }

    // Both capabilities share lease/cancellation handling; the operator's approved profile
    // fixes the wire route and result shape. A model packet cannot choose either.
    internal async Task<LocalProposalLease?> ClaimInference(CancellationToken cancellation)
    {
        if (options.Profile == LocalWorkProtocol.Profile)
        {
            var work = await Claim(cancellation);
            return work is null ? null : new(work.Id, work.Token, JsonSerializer.SerializeToElement(new
            {
                model = work.Model,
                stream = false,
                think = false,
                messages = new[] { new { role = "user", content = work.Instruction } },
                options = new { num_ctx = QuickPrompt.Web.Infrastructure.LocalModelPolicy.AgentContextLength, num_predict = 2600, temperature = 0 }
            }), work.LeaseSeconds, work.MaximumSeconds);
        }
        using var response = await Work("/api/companion/v1/proposals/claim", new { }, cancellation);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        var proposal = await response.Content.ReadFromJsonAsync<LocalProposalLease>(Json, cancellation) ?? throw new InvalidOperationException();
        if (proposal.Id == Guid.Empty || proposal.Token == Guid.Empty || proposal.LeaseSeconds is < 5 or > 120 || proposal.MaximumSeconds is < 1 or > 600)
            throw new InvalidOperationException("El trabajo excede el perfil permitido.");
        LocalProposalProtocol.ValidateRequest(proposal.Packet);
        return proposal;
    }
    private string InferencePath => options.Profile == LocalWorkProtocol.Profile ? "/api/companion/v1/work" : "/api/companion/v1/proposals";
    internal async Task<LocalLeasePulse> HeartbeatInference(LocalProposalLease lease, CancellationToken cancellation)
    {
        using var response = await Work($"{InferencePath}/{lease.Id}/heartbeat", new LocalLeaseToken(lease.Token), cancellation);
        return await response.Content.ReadFromJsonAsync<LocalLeasePulse>(Json, cancellation) ?? throw new InvalidOperationException();
    }
    internal void ValidateInference(JsonElement reply, JsonElement request)
    {
        if (options.Profile == LocalProposalProtocol.Profile) LocalProposalProtocol.ValidateReply(reply, request);
        else if (reply.GetProperty("message").GetProperty("content").GetString() is not { Length: > 0 and <= 64000 } text || string.IsNullOrWhiteSpace(text))
            throw new QuickPrompt.Local.ValidationException("Respuesta local inválida.");
    }
    internal async Task FinishInference(LocalProposalLease lease, JsonElement? reply, string? error, CancellationToken cancellation)
    {
        object body = options.Profile == LocalProposalProtocol.Profile ? new LocalProposalReceipt(lease.Token, reply, error)
            : new LocalWorkReceipt(lease.Token, reply?.GetProperty("message").GetProperty("content").GetString(), error);
        using var response = await Work($"{InferencePath}/{lease.Id}/finish", body, cancellation);
    }
    public async Task Disconnect(CancellationToken cancellation)
    {
        using var response = await Work("/api/companion/v1/disconnect", new { }, cancellation);
        secrets.Forget();
    }
    public async ValueTask DisposeAsync() { http.Dispose(); await services.DisposeAsync(); }
}
