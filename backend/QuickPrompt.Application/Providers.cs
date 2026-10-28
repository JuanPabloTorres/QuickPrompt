namespace QuickPrompt.Nova.Application;

public sealed record ProviderCapability(string ProviderId, string DisplayName, string? DestinationUrl, string[] ModesSupported,
    bool CanPrepare, bool CanSubmit, bool RequiresExtension, bool RequiresCredentials, string AvailabilityStatus, DateTime? LastVerifiedAt);
public static class ProviderRegistry
{
    // Destination identity belongs to the domain, not to model/provider labels.
    // External preparation keeps its proposed registry ID for normal validation;
    // local execution has a fixed adapter, and read/content goals have no destination.
    public static string? DestinationFor(IEnumerable<SemanticOperation> effects, string? proposed)
    {
        var goals = effects.ToHashSet();
        if (goals.Contains(SemanticOperation.PREPARE_PROVIDER)) return proposed;
        return goals.Overlaps([SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL]) ? "ollama" : null;
    }

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
public record PreparedTransfer(string Text, int TemplateRevision, string DestinationUrl, string DisplayName, string Mode = "COPY_AND_OPEN");
public interface IPromptTransfer
{
    PreparedTransfer Prepare(TransferInput input);
}
public sealed class PromptTransfer(PromptLibrary library) : IPromptTransfer
{
    public PreparedTransfer Prepare(TransferInput input)
    {
        if (!input.Confirmed) throw new QuickPrompt.Local.ValidationException("Confirma el destino antes de preparar el texto.");
        var provider = ProviderRegistry.Providers.SingleOrDefault(p => p.ProviderId == input.ProviderId && p.ModesSupported.Contains("COPY_AND_OPEN"))
            ?? throw new QuickPrompt.Local.ValidationException("Destino no permitido para copiar y abrir.");
        var compiled = library.Compile(input.PromptId, new(input.Revision, input.Values));
        return new(compiled.Text, compiled.TemplateRevision, provider.DestinationUrl!, provider.DisplayName);
    }
}
