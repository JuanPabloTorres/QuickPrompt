using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Companion;

public enum CompanionRunPhase { Connecting, Claiming, Generating, Submitting }
public enum CompanionRunEnd { Idle, JobLimit, UserStopped, OAuthRejected, HttpRejected, TransportOrConfigurationError, UnconfirmedExit }

/// <summary>Local lifecycle metadata only. A historical PID/phase never proves that a worker is alive.</summary>
public sealed record CompanionRunDiagnostic(int Version, int ProcessId, string ProfileId, string? BuildSha256, DateTimeOffset StartedUtc,
    DateTimeOffset UpdatedUtc, DateTimeOffset? EndedUtc, CompanionRunPhase Phase, CompanionRunEnd? End,
    int HandledJobs, string? LastWorkCode, int? HttpStatus, string? OAuthCode);

/// <summary>One run, one writer; no packets, leases, paths, credentials or exception messages are accepted.</summary>
public sealed class CompanionRunDiagnostics(string profile, Action<CompanionRunDiagnostic> persist)
{
    internal static readonly JsonSerializerOptions Json = new() {
        Converters = { new JsonStringEnumConverter<CompanionRunPhase>(), new JsonStringEnumConverter<CompanionRunEnd>() }
    };
    private CompanionRunDiagnostic current = Initial(profile);
    private bool published;

    private static CompanionRunDiagnostic Initial(string profile)
    {
        var now = DateTimeOffset.UtcNow;
        if (profile is not (LocalWorkProtocol.Profile or LocalProposalProtocol.Profile)) throw new ArgumentException("Perfil de diagnóstico inválido.");
        string? hash = null;
        try { hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { /* Missing build identity is explicit, never a reason to stop work. */ }
        return new(1, Environment.ProcessId, profile, hash, now, now, null, CompanionRunPhase.Connecting, null, 0, null, null, null);
    }

    public void Stage(CompanionRunPhase phase)
    {
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (current.End is not null) return;
        if (published && current.Phase == phase) return;
        current = current with { Phase = phase, UpdatedUtc = DateTimeOffset.UtcNow };
        Publish();
    }

    public void WorkFinished(string? code)
    {
        if (current.End is not null) return;
        var safe = code is null ? "SUCCEEDED" : WorkCode(code) ? code : "UNKNOWN_FAILURE";
        current = current with { HandledJobs = current.HandledJobs + 1, LastWorkCode = safe, UpdatedUtc = DateTimeOffset.UtcNow };
        Publish();
    }

    public void Finish(CompanionRunEnd end, int? httpStatus = null, string? oauthCode = null)
    {
        if (!Enum.IsDefined(end)) throw new ArgumentOutOfRangeException(nameof(end));
        if (current.End is not null) return;
        var now = DateTimeOffset.UtcNow;
        current = current with { End = end, EndedUtc = now, UpdatedUtc = now,
            HttpStatus = httpStatus is >= 100 and <= 599 ? httpStatus : null,
            OAuthCode = oauthCode is null ? null : SafeOAuthCode(oauthCode) };
        Publish();
    }

    public static string SafeOAuthCode(string? code) => code is "invalid_request" or "invalid_scope" or "invalid_client" or "server_error"
        or "unauthorized_client" or "invalid_target" or "access_denied" or "invalid_grant" or "temporarily_unavailable" ? code : "protocol_error";

    internal static bool Valid(CompanionRunDiagnostic value) => value.Version == 1 && value.ProcessId > 0
        && value.ProfileId is LocalWorkProtocol.Profile or LocalProposalProtocol.Profile
        && (value.BuildSha256 is null || value.BuildSha256.Length == 64 && value.BuildSha256.All(Uri.IsHexDigit))
        && value.StartedUtc >= DateTimeOffset.UnixEpoch && value.StartedUtc <= value.UpdatedUtc && Enum.IsDefined(value.Phase)
        && (value.End is null && value.EndedUtc is null || value.End is { } end && Enum.IsDefined(end) && value.EndedUtc == value.UpdatedUtc)
        && value.HandledJobs is >= 0 and <= 50 && (value.LastWorkCode is null || WorkCode(value.LastWorkCode))
        && (value.HttpStatus is null or >= 100 and <= 599)
        && (value.OAuthCode is null || value.OAuthCode == SafeOAuthCode(value.OAuthCode));

    private static bool WorkCode(string code) => code is "SUCCEEDED" or "MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_FAILED"
        or "LOCAL_STOPPED" or "LEASE_LOST" or "UNKNOWN_FAILURE";

    private void Publish()
    {
        published = true;
        try { persist(current); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Console.Error.WriteLine("No se pudo conservar el diagnóstico local. No confirma actividad ni resultado."); }
    }
}
