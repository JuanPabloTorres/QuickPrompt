namespace QuickPrompt.Nova.Application;

// Adapter for existing typed application consumers/tests. API DI always uses ISemanticIntentInterpreter.
// It maps a structured contract, never classifies message text or decides from words.
internal sealed class StructuredIntentCompatibility(IIntentInterpreter legacy) : ISemanticIntentInterpreter
{
    public async Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
    {
        var understood = await legacy.Understand(message, model, ct);
        var operations = understood.Operations ?? [understood.Intent];
        var changes = operations.Contains(PreparationIntent.ADAPT_PROMPT) || operations.Contains(PreparationIntent.IMPROVE_PROMPT);
        var creates = !changes && operations.Contains(PreparationIntent.CREATE_PROMPT);
        var execute = operations.Contains(PreparationIntent.RUN_LOCAL);
        var steps = new List<SemanticStep>();
        void Add(SemanticOperation op) => steps.Add(new(steps.Count + 1, op, steps.Count == 0 ? [] : [steps.Count]));
        if (understood.Intent is PreparationIntent.UNKNOWN or PreparationIntent.ORGANIZE_PROMPT) Add(SemanticOperation.ASK_CLARIFICATION);
        else {
            if (creates) Add(SemanticOperation.SEARCH_PROMPT);
            if (!creates) { if (context.Selected is null) Add(SemanticOperation.SEARCH_PROMPT); Add(SemanticOperation.RESOLVE_PROMPT); }
            if (changes) Add(SemanticOperation.PROPOSE_ADAPTATION);
            else if (creates) Add(SemanticOperation.CREATE_DRAFT);
            else Add(SemanticOperation.OPEN_PROMPT);
            if (operations.Contains(PreparationIntent.COMPLETE_PROMPT)) Add(SemanticOperation.FILL_VARIABLES);
            if (changes || creates) Add(SemanticOperation.VALIDATE_DRAFT);
            Add(SemanticOperation.COMPILE_PROMPT);
            if (execute) { Add(SemanticOperation.PREPARE_LOCAL_EXECUTION); Add(SemanticOperation.EXECUTE_LOCAL); }
        }
        var result = new SemanticInterpretation(steps.Select(s => s.Operation).ToArray(), creates ? TargetReference.NONE : context.Selected is not null ? TargetReference.SELECTED : TargetReference.NAMED,
            understood.Query, creates ? null : context.Selected?.Id, understood.Query, changes ? message : "", new([], false, null, null, [], [], false),
            understood.Intent == PreparationIntent.UNKNOWN ? SemanticOutcome.CLARIFICATION : execute ? SemanticOutcome.LOCAL_RESPONSE : changes ? SemanticOutcome.DRAFT : creates ? SemanticOutcome.NEW_DRAFT : SemanticOutcome.READ,
            steps.ToArray(), string.IsNullOrWhiteSpace(understood.Clarification) ? [] : [understood.Clarification], false, execute ? message : "", null, understood.Clarification);
        return string.IsNullOrWhiteSpace(understood.Clarification) ? result : SemanticPlanning.Clarify(result, understood.Clarification);
    }
}
internal sealed class RankedCandidateCompatibility : ICandidateSelector
{
    public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var relevant = candidates.Where(c => c.Score >= 2).ToArray();
        var ids = relevant.Length == 0 ? [] : relevant.Where(c => c.Score >= relevant[0].Score * .85).Take(3).Select(c => c.Prompt.Id).ToArray();
        return Task.FromResult(new SemanticCandidateChoice(ids, "Adaptador del contrato estructurado anterior; no usado para interpretar lenguaje natural en la API."));
    }
}
