using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class NovaInference : INovaInference
{
    private readonly ILocalLlmClient client;
    private readonly ISemanticIntentInterpreter semantic;
    public NovaInference(ILocalLlmClient client, ISemanticIntentInterpreter semantic)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.semantic = semantic ?? throw new ArgumentNullException(nameof(semantic));
    }
    public async Task<NovaPlan> Interpret(string message, string model, CancellationToken ct)
    {
        var interpretation = await semantic.Interpret(message, SemanticContext.Empty, model, ct);
        var intent = interpretation.Creates ? NovaIntent.CREATE_DRAFT : interpretation.Adapts ? NovaIntent.IMPROVE_DRAFT : interpretation.Executes ? NovaIntent.RUN_LOCAL
            : interpretation.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || interpretation.Has(SemanticOperation.PREPARE_PROVIDER) ? NovaIntent.SELECT_PROVIDER
            : interpretation.Has(SemanticOperation.FILL_VARIABLES) ? NovaIntent.FILL_VARIABLES : interpretation.Has(SemanticOperation.COMPILE_PROMPT) ? NovaIntent.COMPILE
            : interpretation.Has(SemanticOperation.OPEN_PROMPT) ? NovaIntent.OPEN : interpretation.Has(SemanticOperation.SEARCH_PROMPT) ? NovaIntent.SEARCH
            : interpretation.DesiredOutcome == SemanticOutcome.ADVICE ? NovaIntent.HELP : NovaIntent.UNKNOWN;
        return new(intent, interpretation.Query, Math.Min(3, interpretation.ProposedSteps.Length));
    }
    public Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct) => client.Propose(intent, context, source, model, ct);
    public Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct, bool shortening) => client.Propose(intent, context, source, model, ct, allowRevision: true, requestedShortening: shortening);
    public Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct, bool shortening, string? contentChange) => client.Propose(intent, context, source, model, ct, allowRevision: true, requestedShortening: shortening, interpretation: contentChange is null ? null : new(contentChange));
    public Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct, bool shortening, string? contentChange, string[] targetTerms) => client.Propose(intent, context, source, model, ct, allowRevision: true, requestedShortening: shortening, interpretation: contentChange is null ? null : new(contentChange, TargetTerms: targetTerms));
    public Task<Dictionary<string, string>> Suggest(Template source, string context, string model, CancellationToken ct) => client.Suggest(source, context, model, ct);
}
