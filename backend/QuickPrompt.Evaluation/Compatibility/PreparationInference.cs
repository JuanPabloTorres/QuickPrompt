using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Compatibility facade only. Production composition resolves each capability independently.
public sealed class PreparationInference : IIntentInterpreter, ISemanticIntentInterpreter, ISemanticContentInterpreter, IPromptGenerator, ICandidateSelector, IInputValueExtractor, IInputScopeInterpreter, IUiDecisionModel, IAgentModelTransport, ISemanticClauseReviewer, ISourceReferenceResolver, IGroundedSourceSelector, ISemanticAdviceResponder, ITaskGoalInterpreter, IDisposable
{
    // Match the existing UI-decision budget throughout a task. Changing runner
    // context sizes between semantic, editor and UI stages forces model reloads.
    public const int InferenceContextLength = LocalModelPolicy.AgentContextLength;
    private readonly IValidatedAgentModelTransport transport;
    private readonly IDisposable? ownedTransport;
    private readonly SemanticIntentInterpreter semanticInterpretation;
    private readonly IPromptGenerator promptGeneration;
    private readonly UiDecisionModel uiDecisions;
    private readonly SemanticCandidateSelector candidateSelection;
    private readonly IInputValueExtractor inputExtraction;
    private readonly IInputScopeInterpreter inputScope;
    private readonly IDraftFidelityReviewer fidelityReview;
    private readonly HttpClient? editorHttp;
    private readonly bool semanticThinking;
    private readonly LocalModelPolicy modelPolicy;
    private readonly bool authoringUseContract;
    public PreparationInference(string endpoint, Action<InferenceObservation>? observe = null, ILocalLlmClient? localEditor = null, bool reuseValidatedInference = false, bool semanticThinking = true, bool taskGoals = false, bool qwenThinkingSampling = false, bool taskResultContract = false, ICapabilityKnowledge? capabilityKnowledge = null, LocalModelPolicy? modelPolicy = null, bool authoringUseContract = false, IValidatedAgentModelTransport? modelTransport = null, IInputValueExtractor? valueExtractor = null, IInputScopeInterpreter? scopeInterpreter = null, IDraftFidelityReviewer? fidelityReviewer = null, IPromptGenerator? promptGenerator = null, SemanticIntentInterpreter? intentInterpreter = null)
    {
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.semanticThinking = semanticThinking;
        this.authoringUseContract = authoringUseContract;
        OllamaClient.ValidateEndpoint(endpoint);
        if (modelTransport is null) {
            var owned = new OllamaAgentModelTransport(endpoint, observe, reuseValidatedInference, semanticThinking, qwenThinkingSampling, this.modelPolicy);
            transport = owned; ownedTransport = owned;
        } else transport = modelTransport;
        uiDecisions = new UiDecisionModel(transport, this.modelPolicy);
        candidateSelection = new SemanticCandidateSelector(transport);
        semanticInterpretation = intentInterpreter ?? new SemanticIntentInterpreter(transport, candidateSelection, observe, semanticThinking,
            taskGoals, taskResultContract, capabilityKnowledge, this.modelPolicy, authoringUseContract);
        inputExtraction = valueExtractor ?? new PromptInputValueExtractor(transport, observe);
        inputScope = scopeInterpreter ?? new PromptInputScopeInterpreter(inputExtraction, this.modelPolicy, observe);
        fidelityReview = fidelityReviewer ?? new DraftFidelityReviewer(transport, semanticThinking, this.modelPolicy, authoringUseContract);
        if (promptGenerator is not null) promptGeneration = promptGenerator;
        else {
            var editor = localEditor;
            if (editor is null) {
                editorHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
                editor = new OllamaClient(editorHttp, endpoint, TimeSpan.FromSeconds(90), InferenceContextLength, qwenThinkingSampling, this.modelPolicy);
            }
            promptGeneration = new PromptDraftGenerator(transport, editor, inputExtraction, fidelityReview, semanticThinking, this.modelPolicy, authoringUseContract);
        }
    }
    public Action<int, string, string?>? SemanticDiagnostic {
        get => semanticInterpretation.SemanticDiagnostic;
        set => semanticInterpretation.SemanticDiagnostic = value;
    }
    public bool TaskGoalsEnabled => semanticInterpretation.TaskGoalsEnabled;
    public static readonly JsonElement UnderstandingSchema = ContractSchema.Object(new()
    {
        ["intent"] = ContractSchema.Text(40, Enum.GetNames<PreparationIntent>()), ["query"] = ContractSchema.Text(200), ["clarification"] = ContractSchema.Text(500),
        ["operations"] = new { type = "array", maxItems = 4, items = ContractSchema.Text(40, Enum.GetNames<PreparationIntent>()) }
    }, "intent", "query", "clarification", "operations");
    public static readonly JsonElement SemanticSchema = SemanticIntentInterpreter.SemanticSchema;
    public static readonly JsonElement MeaningSchema = SemanticIntentInterpreter.MeaningSchema;
    public static readonly JsonElement ObjectiveSchema = SemanticIntentInterpreter.ObjectiveSchema;
    public static readonly JsonElement TextGoalSchema = SemanticIntentInterpreter.TextGoalSchema;
    public static IReadOnlyDictionary<string, JsonElement> InstructionScopeSchemas() => SemanticIntentInterpreter.InstructionScopeSchemas();
    public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.Interpret(message, context, model, ct);
    public Task<SemanticConstraintInput> ExtractContent(string message, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.ExtractContent(message, context, model, ct);
    public Task<SemanticReferenceChoice?> ResolveSourceReference(string request, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.ResolveSourceReference(request, context, model, ct);
    public Task<GroundedSourceChoice> SelectSource(string message, RetrievalCandidate[] candidates, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.SelectSource(message, candidates, context, model, ct);
    public Task<string[]> ReviewClauses(string request, string[] clauses, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.ReviewClauses(request, clauses, context, model, ct);
    public Task<string[]> ReviewTargets(string request, string[] terms, SemanticContext context, string model, CancellationToken ct)
        => semanticInterpretation.ReviewTargets(request, terms, context, model, ct);
    public Task<string> Explain(string request, PromptDocument? source, PreparedDraft? workingDraft, string model, CancellationToken ct)
        => semanticInterpretation.Explain(request, source, workingDraft, model, ct);
    public static readonly JsonElement ProposalSchema = PromptDraftGenerator.ProposalSchema;
    private async Task<JsonDocument> Chat(object body, CancellationToken ct, [System.Runtime.CompilerServices.CallerMemberName] string purpose = "Chat")
    {
        ct.ThrowIfCancellationRequested();
        var packet = AgentStagePacket.Encode(body, typeof(SemanticIntentInterpreter), purpose, authoringUseContract);
        return await transport.Send(packet, ct, purpose);
    }
    // Shared local transport only. The replacement owns its context and bounded tool conversation.
    internal Task<JsonDocument> TaskTurn(object packet, CancellationToken ct, string purpose) => Chat(packet, ct, purpose);
    internal bool TaskThinking(string model) => semanticThinking && modelPolicy.Require(model).Thinking;
    Task<JsonDocument> IAgentModelTransport.Send(object packet, CancellationToken ct, string purpose) => TaskTurn(packet, ct, purpose);
    bool IAgentModelTransport.UsesThinking(string model) => TaskThinking(model);
    void IAgentModelTransport.Observe(int stage, string content, string? error) => SemanticDiagnostic?.Invoke(stage, content, error);
    public Task<Understanding> Understand(string message, string model, CancellationToken ct)
        => semanticInterpretation.Understand(message, model, ct);
    // Compatibility only; production composition resolves generation independently.
    public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        => promptGeneration.Generate(input, understanding, candidates, ct,
            (stage, content, error) => SemanticDiagnostic?.Invoke(stage, content, error));
    public static JsonElement ProposalFor(Guid[] candidateIds, Guid? selectedSource, PreparationIntent? intent = null)
        => PromptDraftGenerator.ProposalFor(candidateIds, selectedSource, intent);
    // Compatibility only; the same registered extraction capability serves every caller.
    public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
        => inputExtraction.ExtractInputs(input, template, names, ct);
    public Task<UserInputScope?> ResolveInputScope(string request, SemanticContext context, string model, CancellationToken ct)
        => inputScope.ResolveInputScope(request, context, model, ct);
    // Compatibility only; agent composition resolves candidate selection independently.
    public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        => candidateSelection.Select(message, candidates, model, ct);
    public Task<SemanticCandidateChoice> Select(SourceLookupContext lookup, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        => candidateSelection.Select(lookup, candidates, model, ct);
    // Compatibility only; visible execution composes the selector directly.
    public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct) => uiDecisions.Decide(context, ct);
    private Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct, string? boundTitleLiteral = null)
        => fidelityReview.ReviewDraftFidelity(input, proposal, candidates, ct, boundTitleLiteral,
            (stage, content, error) => SemanticDiagnostic?.Invoke(stage, content, error));
    public void Dispose() { ownedTransport?.Dispose(); editorHttp?.Dispose(); }
}
