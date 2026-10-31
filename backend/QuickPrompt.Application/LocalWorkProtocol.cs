namespace QuickPrompt.Nova.Application;

/// <summary>Versioned companion wire contract. It never transports identity, library access or OS commands.</summary>
public static class LocalWorkProtocol
{
    public const string Version = "quickprompt-local-work/1";
    public const string Model = QuickPrompt.Web.Infrastructure.LocalModelPolicy.DefaultModelName;
    public const string ClientId = "quickprompt-companion";
    public const string Scope = "companion:work";
    public const string Profile = "ollama-qwen3-8b";
    /// <summary>Fixed failure data shared by both leased work receipts; never accepts arbitrary model text.</summary>
    public static bool IsFailureCode(string? code) => code is "MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_FAILED" or "MODEL_OUTPUT_LIMIT" or "LOCAL_STOPPED";
}
public sealed record LocalWorkLease(Guid Id, Guid Token, string Instruction, string Model, int LeaseSeconds, int MaximumSeconds);
public sealed record LocalWorkReceipt(Guid Token, string? Response, string? ErrorCode = null);
public sealed record LocalLeaseToken(Guid Token);
public sealed record LocalCompanionProfile(Guid ConnectionId, string ProfileId, string Model, string WorkProtocol, bool DispatchReady);
public sealed record LocalLeasePulse(bool Owned, bool CancelRequested);

/// <summary>Operational observation only; neither consent nor a claim of model-generation readiness.</summary>
public static class CompanionPresenceProtocol
{
    public const string Header = "X-QP-Companion-Observation";
    public const string StopPath = "/api/companion/v1/presence/stop";
}
public sealed record LocalPresenceStop(Guid ObservationId);
