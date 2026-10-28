namespace QuickPrompt.Nova.Application;

/// <summary>Versioned companion wire contract. It never transports identity, library access or OS commands.</summary>
public static class LocalWorkProtocol
{
    public const string Version = "quickprompt-local-work/1";
    public const string Model = QuickPrompt.Web.Infrastructure.LocalModelPolicy.DefaultModelName;
    public const string ClientId = "quickprompt-companion";
    public const string Scope = "companion:work";
    public const string Profile = "ollama-qwen3-8b";
}
public sealed record LocalWorkLease(Guid Id, Guid Token, string Instruction, string Model, int LeaseSeconds, int MaximumSeconds);
public sealed record LocalWorkReceipt(Guid Token, string? Response, string? ErrorCode = null);
public sealed record LocalLeaseToken(Guid Token);
public sealed record LocalCompanionProfile(Guid ConnectionId, string ProfileId, string Model, string WorkProtocol, bool DispatchReady);
public sealed record LocalLeasePulse(bool Owned, bool CancelRequested);
