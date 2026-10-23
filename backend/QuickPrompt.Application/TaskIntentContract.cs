using System.Collections.Immutable;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Immutable action request and product-owned postconditions, fixed before the
// execution plan. Structure, scope and receipts do not prove semantic meaning;
// real-provider acceptance against the literal request remains independent.
public sealed class TaskIntentContract
{
    public string RequestHash { get; }
    public UserActionRequest UserActions { get; }
    public ImmutableArray<SemanticOperation> RequestedEffects { get; }
    public ImmutableArray<SemanticOperation> AllowedEffects { get; }
    public ImmutableArray<SemanticOperation> ForbiddenEffects { get; }
    public ImmutableArray<CompletionCondition> RequiredOutcomes { get; }
    public ImmutableArray<SemanticOperation> ApprovalRequirements { get; }
    public SemanticOutcome Outcome { get; }
    public Guid? TargetId { get; }
    public TargetReference Reference { get; }
    public string? ProviderId { get; }
    public CopyContent CopyContent { get; }

    private TaskIntentContract(UserActionRequest request, SemanticMeaning intent, bool bindInputs)
    {
        UserActions = request;
        RequestHash = request.RequestHash;
        RequestedEffects = intent.Goals.ToImmutableArray();
        ForbiddenEffects = intent.Constraints.ForbiddenOperations.ToImmutableArray();
        Outcome = intent.DesiredOutcome;
        TargetId = intent.ResolvedTargetId;
        ProviderId = intent.ProviderId;
        CopyContent = intent.CopyContent;
        // Reuse the domain dependency compiler, independently of the proposed
        // execution plan. Freeze the result; later plans cannot rewrite it.
        var dependencies = SemanticPlanCompiler.Compile(intent, bindInputs);
        // Source lookup normalizes NONE + an explicit search/query to NAMED.
        // Freeze that existing domain rule too, instead of treating it as a
        // later model-created identity or blocking legitimate clarification.
        Reference = dependencies.TargetReference;
        AllowedEffects = dependencies.Intentions.ToImmutableArray();
        ApprovalRequirements = RequestedEffects.Where(op => op is SemanticOperation.REQUEST_SAVE_VERSION
            or SemanticOperation.REQUEST_SAVE_COPY or SemanticOperation.REQUEST_DELETE).ToImmutableArray();
        // User-facing postconditions precede operation compilation. The compiler
        // cannot erase authoring/clipboard/execution obligations by dropping roots.
        var required = request.Results.ToHashSet();
        bool Has(SemanticOperation effect) => request.Effects.Contains(effect);
        if ((Has(SemanticOperation.SEARCH_PROMPT) || Has(SemanticOperation.OPEN_PROMPT) || Has(SemanticOperation.PROPOSE_ADAPTATION)
                || SemanticPlanCompiler.RequiresSource(intent)) && !Has(SemanticOperation.REQUEST_DELETE)
            && (Outcome != SemanticOutcome.ADVICE || Has(SemanticOperation.OPEN_PROMPT)))
            required.Add(Has(SemanticOperation.SEARCH_PROMPT) && ForbiddenEffects.Contains(SemanticOperation.OPEN_PROMPT)
                ? CompletionCondition.SEARCH_RESULTS_VISIBLE : CompletionCondition.SOURCE_VISIBLE);
        if (CopyContent == CopyContent.TEMPLATE && request.Effects.Any(e => e is SemanticOperation.COPY_PROMPT or SemanticOperation.COPY_TO_CLIPBOARD)) {
            required.Remove(CompletionCondition.COMPILED_VISIBLE); required.Add(CompletionCondition.TEMPLATE_VISIBLE);
        }
        RequiredOutcomes = required.ToImmutableArray();
    }

    public static TaskIntentContract Accept(UserActionRequest request, SemanticMeaning intent, bool bindInputs = false)
    {
        var expected = bindInputs && !intent.Constraints.ForbiddenOperations.Contains(SemanticOperation.FILL_VARIABLES)
            ? SemanticPlanCompiler.MinimalGoals(request.Effects.ToArray(), intent.TargetReference, true) : request.Effects.ToArray();
        if (!expected.ToHashSet().SetEquals(intent.Goals))
            throw new PreparationToolException("ACTION_REQUEST_DIVERGED", "semantic_goal", "El plan pierde o añade acciones del encargo original.");
        if (intent.Goals.Intersect(intent.Constraints.ForbiddenOperations).Any())
            throw new PreparationToolException("INTENT_CONTRADICTION", "semantic_goal", "El encargo requiere y prohíbe el mismo efecto.");
        return new(request, intent, bindInputs);
    }

    public bool Allows(SemanticInterpretation plan)
    {
        // Suspension preserves the original intent, but can never complete it.
        if (plan.Intentions is [SemanticOperation.ASK_CLARIFICATION] && plan.DeferredGoals is { Length: > 0 } deferred)
            return (RequestedEffects.ToHashSet().SetEquals(deferred)
                || AllowedEffects.Where(SemanticPlanCompiler.Goals.Contains).ToHashSet().SetEquals(deferred))
                && plan.DesiredOutcome == SemanticOutcome.CLARIFICATION && plan.DeferredOutcome == Outcome
                && !plan.Intentions.Intersect(ForbiddenEffects).Any();
        return Outcome == plan.DesiredOutcome && ProviderId == plan.ProviderId && CopyContent == plan.CopyContent && AllowedEffects.ToHashSet().SetEquals(plan.Intentions)
            && !plan.Intentions.Intersect(ForbiddenEffects).Any()
            && (TargetId is null || plan.ResolvedTargetId == TargetId)
            && (Reference != TargetReference.NONE || plan.TargetReference == TargetReference.NONE);
    }
}
