using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuickPrompt.Web.Infrastructure;

public sealed class NovaInference : INovaInference, IDisposable
{
    private readonly ILocalLlmClient client;
    private readonly HttpClient planner;
    public NovaInference(ILocalLlmClient client, string endpoint, TimeSpan timeout)
    {
        this.client = client;
        planner = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = OllamaClient.ValidateEndpoint(endpoint), Timeout = timeout, MaxResponseContentBufferSize = 256000 };
    }
    public async Task<NovaPlan> Interpret(string message, string model, CancellationToken ct)
    {
        using var semantic = new PreparationInference(planner.BaseAddress!.AbsoluteUri, localEditor: client);
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
    public void Dispose() => planner.Dispose();
}
