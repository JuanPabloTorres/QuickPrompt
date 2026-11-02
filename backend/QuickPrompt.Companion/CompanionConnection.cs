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

internal enum CompanionProfileFailure { Missing, EmptyConnection, WrongCapability, WrongModel, WrongProtocol, NotReady, WrongConnection, ReasoningUnsupported, ReasoningNotAcknowledged }

// Only host-owned enum values enter this message: never the received profile,
// connection ID, response body, token or arbitrary exception text.
internal sealed class CompanionProfileException(CompanionProfileFailure failure)
    : InvalidOperationException("LOCAL_PROFILE_" + failure.ToString().ToUpperInvariant());

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
    private string? lastPresenceObservation;
    private string? proposalProtocol;
    private bool reasoningSupported;
    private const string PreviousProposalProtocol = "quickprompt-local-proposal/1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 64 };
    public CompanionConnection(CompanionOptions options, CompanionSecrets secrets)
    {
        if (!LocalWorkProtocol.Supports(options.WorkProtocol)
            || options.Profile != LocalWorkProtocol.Profile && options.WorkProtocol != LocalWorkProtocol.Version)
            throw new ArgumentException("Protocolo de ejecución local incompatible con la capacidad configurada.");
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
        if (secrets.Exists || secrets.HasPendingPair) throw new InvalidOperationException("Este perfil ya tiene una vinculación completa o pendiente. Usa status para comprobarla antes de solicitar otro acceso.");
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
        await AcceptPair(result.AccessToken, result.AccessTokenExpirationDate,
            result.RefreshToken ?? throw new InvalidOperationException("No se concedió renovación privada."), cancellation);
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
        if (!secrets.Exists && secrets.HasPendingPair)
        {
            var pending = secrets.LoadPendingPair(options.Origin.AbsoluteUri);
            var result = await ExchangeRefresh(pending, cancellation);
            await AcceptPair(result.AccessToken, result.AccessTokenExpirationDate, result.RefreshToken ?? pending, cancellation);
            return;
        }
        session = secrets.Load(options.Origin.AbsoluteUri, options.Profile);
        await Refresh(cancellation);
    }
    private async Task Refresh(CancellationToken cancellation)
    {
        var current = session ?? throw new InvalidOperationException("Vincula primero este perfil local.");
        var result = await ExchangeRefresh(current.RefreshToken, cancellation);
        await AcceptRefresh(result, cancellation);
    }
    private async Task<RefreshTokenAuthenticationResult> ExchangeRefresh(string refreshToken, CancellationToken cancellation) =>
        await oauth.AuthenticateWithRefreshTokenAsync(new RefreshTokenAuthenticationRequest
        {
            RegistrationId = "quickprompt-local",
            RefreshToken = refreshToken,
            Resources = [options.Resource],
            DisableUserInfo = true,
            CancellationToken = cancellation
        });
    // An approved device grant has no trusted connection ID until Profile
    // succeeds. Retain it under a separate DPAPI purpose, never as a usable
    // CompanionSession. Explicit status/run can resume without new consent.
    private async Task AcceptPair(string token, DateTimeOffset? expiration, string refreshToken, CancellationToken cancellation)
    {
        secrets.SavePendingPair(options.Origin.AbsoluteUri, refreshToken);
        accessToken = token; expiresAt = expiration ?? DateTimeOffset.UtcNow.AddMinutes(2);
        var profile = await Profile(cancellation);
        var accepted = new CompanionSession(options.Origin.AbsoluteUri, profile.ConnectionId, refreshToken, options.Profile);
        secrets.Save(accepted);
        session = accepted;
        secrets.CompletePendingPair();
    }
    private async Task AcceptRefresh(RefreshTokenAuthenticationResult result, CancellationToken cancellation)
    {
        var current = session ?? throw new InvalidOperationException("Vincula primero este perfil local.");
        // The issuer can consume the previous refresh token as soon as OAuth
        // succeeds. Commit its validated replacement before any dependent HTTP
        // request (even if cancellation has arrived), or a failed profile read
        // strands the next connection with a spent token. This checkpoint keeps
        // the original identity; Profile still gates all work below.
        var renewed = current with { RefreshToken = result.RefreshToken ?? current.RefreshToken };
        secrets.Save(renewed);
        session = renewed;
        accessToken = result.AccessToken; expiresAt = result.AccessTokenExpirationDate ?? DateTimeOffset.UtcNow.AddMinutes(2);
        var profile = await Profile(cancellation);
        if (profile.ConnectionId != current.ConnectionId) throw new CompanionProfileException(CompanionProfileFailure.WrongConnection);
    }
    public async Task<LocalCompanionProfile> Profile(CancellationToken cancellation)
    {
        using var response = await Send(HttpMethod.Get, "/api/companion/v1/profile", null, cancellation);
        var profile = await response.Content.ReadFromJsonAsync<LocalCompanionProfile>(Json, cancellation)
            ?? throw new CompanionProfileException(CompanionProfileFailure.Missing);
        if (profile.ConnectionId == Guid.Empty) throw new CompanionProfileException(CompanionProfileFailure.EmptyConnection);
        if (profile.ProfileId != options.Profile) throw new CompanionProfileException(CompanionProfileFailure.WrongCapability);
        if (profile.Model != LocalWorkProtocol.Model) throw new CompanionProfileException(CompanionProfileFailure.WrongModel);
        var expectedProtocol = options.Profile == LocalWorkProtocol.Profile ? options.WorkProtocol : LocalCompanionProfiles.Protocol(options.Profile);
        if (profile.WorkProtocol != expectedProtocol
            && !(options.Profile == LocalProposalProtocol.Profile && profile.WorkProtocol == PreviousProposalProtocol))
            throw new CompanionProfileException(CompanionProfileFailure.WrongProtocol);
        if (!profile.DispatchReady) throw new CompanionProfileException(CompanionProfileFailure.NotReady);
        reasoningSupported = profile.ReasoningContracts?.Contains(PairedReasoningProtocol.Profile, StringComparer.Ordinal) == true;
        if (options.ReasoningProfile is not null && !reasoningSupported)
            throw new CompanionProfileException(CompanionProfileFailure.ReasoningUnsupported);
        // The v2 client can enforce the older v1 contract. Never accept an
        // arbitrary version, another capability or v2-only fields under v1.
        proposalProtocol = profile.WorkProtocol;
        return profile;
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, path);
        if (options.Profile == LocalWorkProtocol.Profile && options.WorkProtocol != LocalWorkProtocol.Version)
            request.Headers.Add(LocalWorkProtocol.VersionHeader, options.WorkProtocol);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken ?? throw new InvalidOperationException("No hay acceso activo."));
        var reasoningExchange = options.Command == "run" && options.ReasoningProfile is not null
            && (path == "/api/companion/v1/proposals/claim" || path.StartsWith("/api/companion/v1/proposals/", StringComparison.Ordinal) && path.EndsWith("/heartbeat", StringComparison.Ordinal));
        if (reasoningExchange)
        {
            if (!reasoningSupported) throw new CompanionProfileException(CompanionProfileFailure.ReasoningUnsupported);
            request.Headers.Add(PairedReasoningProtocol.ProfileHeader, options.ReasoningProfile);
        }
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        var response = await http.SendAsync(request, cancellation);
        if ((path.EndsWith("/claim", StringComparison.Ordinal) || path.EndsWith("/heartbeat", StringComparison.Ordinal))
            && response.Headers.TryGetValues(CompanionPresenceProtocol.Header, out var observations)
            && observations.Take(2).ToArray() is { Length: 1 } values
            && Guid.TryParse(values[0], out var observation) && observation != Guid.Empty)
            Volatile.Write(ref lastPresenceObservation, observation.ToString());
        if (!response.IsSuccessStatusCode) { var status = response.StatusCode; response.Dispose(); throw new CompanionRequestException(status); }
        if (reasoningExchange && (!response.Headers.TryGetValues(PairedReasoningProtocol.AcceptedHeader, out var accepted)
            || accepted.Take(2).ToArray() is not { Length: 1 } acceptedValues || acceptedValues[0] != PairedReasoningProtocol.Profile))
        {
            response.Dispose();
            throw new CompanionProfileException(CompanionProfileFailure.ReasoningNotAcknowledged);
        }
        return response;
    }

    /// <summary>Best-effort CAS withdrawal; failure expires naturally and never revokes consent or masks the original run outcome.</summary>
    internal async Task StopPresence()
    {
        if (!Guid.TryParse(Interlocked.Exchange(ref lastPresenceObservation, null), out var observation)) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { using var response = await Work(CompanionPresenceProtocol.StopPath, new LocalPresenceStop(observation), timeout.Token); }
        catch (Exception error) when (error is HttpRequestException or CompanionRequestException or OperationCanceledException
            or InvalidOperationException or System.Text.Json.JsonException or OpenIddictExceptions.ProtocolException
            or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { Console.WriteLine("No se confirmó la retirada de la señal; caducará automáticamente. La autorización no se revocó."); }
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
        LocalWorkLease? work;
        if (options.WorkProtocol == LocalWorkProtocol.BoundVersion)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation),
                new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = 64 });
            work = document.RootElement.Deserialize<LocalWorkLease>(Json);
        }
        else work = await response.Content.ReadFromJsonAsync<LocalWorkLease>(Json, cancellation);
        if (work is null) throw new InvalidOperationException();
        if (work.Id == Guid.Empty || work.Token == Guid.Empty || work.Model != LocalWorkProtocol.Model || work.Instruction is not { Length: > 0 and <= 64000 }
            || work.LeaseSeconds is < 5 or > 120 || work.MaximumSeconds is < 1 or > 600) throw new InvalidOperationException("El trabajo excede el perfil permitido.");
        try
        {
            if (options.WorkProtocol == LocalWorkProtocol.BoundVersion)
                work = work with { Context = (work.Context ?? throw new InvalidOperationException("Falta el contexto de ejecución solicitado.")).Validate(work.Instruction) };
            else if (work.Context is not null) throw new InvalidOperationException("El trabajo v1 contiene contexto no negociado.");
        }
        catch (QuickPrompt.Local.ValidationException) { throw new InvalidOperationException("El contexto recibido no corresponde al trabajo solicitado."); }
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
            if (work is not null && options.WorkProtocol == LocalWorkProtocol.BoundVersion)
                return new(work.Id, work.Token, QuickPrompt.Web.Infrastructure.LocalExecutionFrame.Packet(work.Context!, work.Instruction), work.LeaseSeconds, work.MaximumSeconds);
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
        if (proposal.Packet.TryGetProperty("think", out var thinking) && thinking.GetBoolean()
            && (options.ReasoningProfile != PairedReasoningProtocol.Profile || !options.Inference.Thinking || !options.Inference.QwenThinkingSamplingEnabled))
            throw new CompanionProfileException(CompanionProfileFailure.ReasoningUnsupported);
        if (proposalProtocol == PreviousProposalProtocol && proposal.Packet.GetProperty("options").TryGetProperty("max_requests", out _))
            throw new QuickPrompt.Local.ValidationException("Paquete de propuesta local fuera de contrato.");
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
        if (LocalCompanionProfiles.Proposal(options.Profile)) LocalProposalProtocol.ValidateReply(reply, request);
        else if (reply.GetProperty("message").GetProperty("content").GetString() is not { Length: > 0 and <= 64000 } text || string.IsNullOrWhiteSpace(text))
            throw new QuickPrompt.Local.ValidationException("Respuesta local inválida.");
    }
    internal async Task FinishInference(LocalProposalLease lease, JsonElement? reply, string? error, CancellationToken cancellation)
    {
        object body = LocalCompanionProfiles.Proposal(options.Profile) ? new LocalProposalReceipt(lease.Token, reply, error)
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
