namespace QuickPrompt.Nova.Application;

/// <summary>Versioned companion wire contract. It never transports identity, library access or OS commands.</summary>
public static class LocalWorkProtocol
{
    public const string Version = "quickprompt-local-work/1";
    // Explicit evaluation opt-in. Absence of the header preserves the v1 wire.
    public const string BoundVersion = "quickprompt-local-work/2";
    public const string VersionHeader = "X-QuickPrompt-Work-Protocol";
    public static bool Supports(string? version) => version is Version or BoundVersion;
    public static string RequireVersion(string version) => Supports(version) ? version
        : throw new QuickPrompt.Local.ValidationException("Protocolo de ejecución local no admitido.");
    public const string Model = QuickPrompt.Web.Infrastructure.LocalModelPolicy.DefaultModelName;
    public const string ClientId = "quickprompt-companion";
    public const string Scope = "companion:work";
    public const string Profile = "ollama-qwen3-8b";
    /// <summary>Fixed failure data shared by both leased work receipts; never accepts arbitrary model text.</summary>
    public static bool IsFailureCode(string? code) => code is "MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_FAILED" or "MODEL_OUTPUT_LIMIT" or "LOCAL_STOPPED";
}
public sealed record LocalWorkLease(Guid Id, Guid Token, string Instruction, string Model, int LeaseSeconds, int MaximumSeconds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LocalExecutionContext? Context { get; init; }
}
public sealed record LocalWorkReceipt(Guid Token, string? Response, string? ErrorCode = null);
public sealed record LocalLeaseToken(Guid Token);
public sealed record LocalCompanionProfile(Guid ConnectionId, string ProfileId, string Model, string WorkProtocol, bool DispatchReady, string[]? ReasoningContracts = null);
public sealed record LocalLeasePulse(bool Owned, bool CancelRequested);

/// <summary>Operational observation only; neither consent nor a claim of model-generation readiness.</summary>
public static class CompanionPresenceProtocol
{
    public const string Header = "X-QP-Companion-Observation";
    public const string StopPath = "/api/companion/v1/presence/stop";
}
public sealed record LocalPresenceStop(Guid ObservationId);
