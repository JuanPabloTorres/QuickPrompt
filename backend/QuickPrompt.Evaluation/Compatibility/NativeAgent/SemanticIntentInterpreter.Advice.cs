using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Compatibility facade; the advice owner has no dependency on legacy planning.
public sealed partial class SemanticIntentInterpreter : ISemanticAdviceResponder
{
    public Task<string> Explain(string request, PromptDocument? source, PreparedDraft? workingDraft, string model, CancellationToken ct)
        => new PromptAdviceResponder(transport, semanticThinking, modelPolicy).Explain(request, source, workingDraft, model, ct);
}
