namespace QuickPrompt.Nova.Application;

public sealed record ProviderCapability(string ProviderId, string DisplayName, string? DestinationUrl, string[] ModesSupported,
    bool CanPrepare, bool CanSubmit, bool RequiresExtension, bool RequiresCredentials, string AvailabilityStatus, DateTime? LastVerifiedAt);
public static class ProviderRegistry
{
    // These URLs are destinations only. No prompt text is appended to a URL or sent to a remote API.
    public static readonly IReadOnlyList<ProviderCapability> Providers = Array.AsReadOnly(new[] {
        new ProviderCapability("ollama", "Ollama local", null, ["LOCAL_EXECUTION"], true, false, false, false, "CHECK_LOCAL_MODELS", null),
        External("chatgpt", "ChatGPT", "https://chatgpt.com/"),
        External("claude", "Claude", "https://claude.ai/"),
        External("gemini", "Gemini", "https://gemini.google.com/"),
        External("copilot", "Copilot", "https://copilot.microsoft.com/"),
        External("perplexity", "Perplexity", "https://www.perplexity.ai/")
    });
    private static ProviderCapability External(string id, string name, string url) =>
        new(id, name, url, ["COPY_AND_OPEN"], true, false, false, false, "MANUAL_PASTE", null);
}
public record TransferInput(Guid PromptId, int Revision, Dictionary<string, string> Values, string ProviderId, bool Confirmed);
