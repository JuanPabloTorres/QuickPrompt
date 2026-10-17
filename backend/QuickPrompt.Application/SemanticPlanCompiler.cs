namespace QuickPrompt.Nova.Application;

// Qwen chooses user goals and references. Code adds domain dependencies, never semantic effects.
public record SemanticMeaning(SemanticOperation[] Goals, TargetReference TargetReference, string TargetDescription, Guid? ResolvedTargetId,
    string Query, string RequestedTransformation, SemanticConstraints Constraints, SemanticOutcome DesiredOutcome,
    string[] MissingInformation, bool RequiresConfirmation, string ExecutionEvidence, string? ProviderId, string Explanation);
public static class SemanticPlanCompiler
{
    public static readonly SemanticOperation[] Goals = [SemanticOperation.SEARCH_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.PROPOSE_ADAPTATION,
        SemanticOperation.CREATE_DRAFT, SemanticOperation.FILL_VARIABLES, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.EXECUTE_LOCAL,
        SemanticOperation.PREPARE_PROVIDER, SemanticOperation.COPY_PROMPT, SemanticOperation.EXPLAIN, SemanticOperation.ASK_CLARIFICATION,
        SemanticOperation.CANCEL, SemanticOperation.PAUSE, SemanticOperation.RESUME, SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY];
    public static SemanticInterpretation Compile(SemanticMeaning meaning) => Compile(meaning, false);
    public static SemanticInterpretation Compile(SemanticMeaning meaning, bool resolveProvidedInputBindings)
    {
        if (meaning.Goals.Length is < 1 or > 8 || meaning.Goals.Distinct().Count() != meaning.Goals.Length || meaning.Goals.Any(op => !Goals.Contains(op)))
            throw new PreparationToolException("INVALID_SEMANTIC_GOALS", "semantic_plan", "Objetivos fuera del contrato permitido.");
        var goals = meaning.Goals.ToHashSet(); var steps = new List<SemanticStep>();
        void Add(SemanticOperation operation) { if (!steps.Any(s => s.Operation == operation)) steps.Add(new(steps.Count + 1, operation, steps.Count == 0 ? [] : [steps.Count])); }
        var terminal = goals.Any(op => op is SemanticOperation.ASK_CLARIFICATION or SemanticOperation.CANCEL or SemanticOperation.PAUSE or SemanticOperation.RESUME);
        if (terminal && goals.Count != 1) throw new PreparationToolException("TERMINAL_GOAL_CONFLICT", "semantic_plan", "Una aclaración o control no puede mezclarse con nuevos efectos.");
        var resolveBindings = resolveProvidedInputBindings && !terminal
            && !meaning.Constraints.ForbiddenOperations.Contains(SemanticOperation.FILL_VARIABLES)
            && goals.Any(op => op is SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_PROVIDER);
        var sourceRequired = goals.Any(op => op is SemanticOperation.SEARCH_PROMPT or SemanticOperation.OPEN_PROMPT or SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.REQUEST_SAVE_VERSION)
            || !goals.Contains(SemanticOperation.CREATE_DRAFT) && goals.Any(op => op is SemanticOperation.FILL_VARIABLES or SemanticOperation.COPY_PROMPT
                or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_PROVIDER)
            || goals.Contains(SemanticOperation.CREATE_DRAFT) && meaning.TargetReference != TargetReference.NONE;
        if (sourceRequired && meaning.TargetReference == TargetReference.NONE && !goals.Contains(SemanticOperation.CREATE_DRAFT))
            return new([SemanticOperation.ASK_CLARIFICATION], TargetReference.NONE, meaning.TargetDescription, null, "", meaning.RequestedTransformation,
                meaning.Constraints, SemanticOutcome.CLARIFICATION, [new(1, SemanticOperation.ASK_CLARIFICATION, [])],
                ["¿Qué prompt quieres usar como origen? Selecciónalo o indica su título."], false, "", null,
                "No se ejecutó el plan: falta identificar su origen.", ["MISSING_TARGET_REFERENCE"], meaning.Goals, meaning.DesiredOutcome);
        if (!terminal) {
            if (goals.Contains(SemanticOperation.SEARCH_PROMPT) || sourceRequired && meaning.TargetReference == TargetReference.NAMED) Add(SemanticOperation.SEARCH_PROMPT);
            if (sourceRequired) Add(SemanticOperation.RESOLVE_PROMPT);
            if (goals.Contains(SemanticOperation.OPEN_PROMPT) || goals.Contains(SemanticOperation.PROPOSE_ADAPTATION)) Add(SemanticOperation.OPEN_PROMPT);
            if (goals.Contains(SemanticOperation.PROPOSE_ADAPTATION)) Add(SemanticOperation.PROPOSE_ADAPTATION);
            if (goals.Contains(SemanticOperation.CREATE_DRAFT)) Add(SemanticOperation.CREATE_DRAFT);
            if (goals.Contains(SemanticOperation.PROPOSE_ADAPTATION) || goals.Contains(SemanticOperation.CREATE_DRAFT)) Add(SemanticOperation.VALIDATE_DRAFT);
            if (goals.Contains(SemanticOperation.FILL_VARIABLES) || resolveBindings) Add(SemanticOperation.FILL_VARIABLES);
            if (goals.Any(op => op is SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.CREATE_DRAFT or SemanticOperation.FILL_VARIABLES or SemanticOperation.COPY_PROMPT
                or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_PROVIDER)) Add(SemanticOperation.COMPILE_PROMPT);
            if (goals.Contains(SemanticOperation.PREPARE_LOCAL_EXECUTION) || goals.Contains(SemanticOperation.EXECUTE_LOCAL)) Add(SemanticOperation.PREPARE_LOCAL_EXECUTION);
            if (goals.Contains(SemanticOperation.PREPARE_PROVIDER)) Add(SemanticOperation.PREPARE_PROVIDER);
            if (goals.Contains(SemanticOperation.EXECUTE_LOCAL)) Add(SemanticOperation.EXECUTE_LOCAL);
        }
        foreach (var goal in meaning.Goals) Add(goal);
        return new(steps.Select(s => s.Operation).ToArray(), meaning.TargetReference, meaning.TargetDescription, meaning.ResolvedTargetId, meaning.Query,
            meaning.RequestedTransformation, meaning.Constraints, meaning.DesiredOutcome, steps.ToArray(), meaning.MissingInformation,
            meaning.RequiresConfirmation, meaning.ExecutionEvidence, meaning.ProviderId, meaning.Explanation,
            resolveBindings && !goals.Contains(SemanticOperation.FILL_VARIABLES) ? ["PROVIDED_INPUT_BINDINGS_RESOLVED"] : null);
    }
}
