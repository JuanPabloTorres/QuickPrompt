using System.Text.Json.Serialization;
namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))] public enum ReadObjective { NONE, FIND_SAVED, VIEW_UNCHANGED, READY_FOR_CLIPBOARD, ADVICE_WITHOUT_PROPOSAL }
[JsonConverter(typeof(JsonStringEnumConverter))] public enum DraftObjective { NONE, REWRITE_INSTRUCTION_TEXT, CREATE_DISTINCT_INSTRUCTION }
[JsonConverter(typeof(JsonStringEnumConverter))] public enum ExecutionObjective { NONE, READY_FOR_LOCAL_QWEN, RUN_LOCAL_QWEN_NOW, SHOW_EXTERNAL_DESTINATIONS }
[JsonConverter(typeof(JsonStringEnumConverter))] public enum ControlObjective { NONE, CLARIFY, CANCEL, PAUSE, RESUME }
[JsonConverter(typeof(JsonStringEnumConverter))] public enum SaveObjective { NONE, REVIEW_PERSIST_EXISTING, REVIEW_PERSIST_COPY }
[JsonConverter(typeof(JsonStringEnumConverter))] public enum ValuesObjective { NONE, FILL_TEMPLATE_INPUTS }
public record SemanticDecision(ReadObjective Access, DraftObjective Draft, ExecutionObjective Execution, ControlObjective Control, SaveObjective Save,
    TargetReference TargetReference, string TargetDescription, Guid? ResolvedTargetId, string Query, string RequestedTransformation,
    SemanticConstraintInput Constraints, string[] MissingInformation, bool RequiresConfirmation, string ExecutionEvidence, string? ProviderId, string Explanation,
    ValuesObjective Values = ValuesObjective.NONE)
{
    public SemanticMeaning Meaning(string request, SemanticContext context)
    {
        Constraints.ValidateEvidence(request, context);
        if (Access == ReadObjective.ADVICE_WITHOUT_PROPOSAL && (Draft != DraftObjective.NONE || Execution != ExecutionObjective.NONE || Save != SaveObjective.NONE || Values != ValuesObjective.NONE))
            throw new PreparationToolException("ADVICE_HAS_EFFECTS", "semantic_plan", "El consejo no autoriza cambios ni ejecución.");
        var goals = new List<SemanticOperation>();
        if (Access != ReadObjective.NONE) goals.Add(Access switch { ReadObjective.FIND_SAVED => SemanticOperation.SEARCH_PROMPT, ReadObjective.VIEW_UNCHANGED => SemanticOperation.OPEN_PROMPT, ReadObjective.READY_FOR_CLIPBOARD => SemanticOperation.COPY_PROMPT, _ => SemanticOperation.EXPLAIN });
        if (Draft != DraftObjective.NONE) goals.Add(Draft == DraftObjective.REWRITE_INSTRUCTION_TEXT ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.CREATE_DRAFT);
        if (Values == ValuesObjective.FILL_TEMPLATE_INPUTS) goals.Add(SemanticOperation.FILL_VARIABLES);
        if (Execution != ExecutionObjective.NONE) goals.Add(Execution switch { ExecutionObjective.READY_FOR_LOCAL_QWEN => SemanticOperation.PREPARE_LOCAL_EXECUTION, ExecutionObjective.RUN_LOCAL_QWEN_NOW => SemanticOperation.EXECUTE_LOCAL, _ => SemanticOperation.PREPARE_PROVIDER });
        if (Control != ControlObjective.NONE) goals.Add(Control switch { ControlObjective.CLARIFY => SemanticOperation.ASK_CLARIFICATION, ControlObjective.CANCEL => SemanticOperation.CANCEL, ControlObjective.PAUSE => SemanticOperation.PAUSE, _ => SemanticOperation.RESUME });
        if (Save != SaveObjective.NONE) goals.Add(Save == SaveObjective.REVIEW_PERSIST_EXISTING ? SemanticOperation.REQUEST_SAVE_VERSION : SemanticOperation.REQUEST_SAVE_COPY);
        var outcome = Control switch { ControlObjective.CLARIFY => SemanticOutcome.CLARIFICATION, ControlObjective.CANCEL => SemanticOutcome.CANCELLED, ControlObjective.PAUSE => SemanticOutcome.PAUSED, ControlObjective.RESUME => SemanticOutcome.RESUMED,
            _ => Execution == ExecutionObjective.RUN_LOCAL_QWEN_NOW ? SemanticOutcome.LOCAL_RESPONSE : Execution == ExecutionObjective.READY_FOR_LOCAL_QWEN ? SemanticOutcome.READY_LOCAL
                : Execution == ExecutionObjective.SHOW_EXTERNAL_DESTINATIONS ? SemanticOutcome.READY_PROVIDER
                : Draft == DraftObjective.CREATE_DISTINCT_INSTRUCTION ? SemanticOutcome.NEW_DRAFT : Draft == DraftObjective.REWRITE_INSTRUCTION_TEXT ? SemanticOutcome.DRAFT : Access == ReadObjective.ADVICE_WITHOUT_PROPOSAL ? SemanticOutcome.ADVICE : Save != SaveObjective.NONE ? SemanticOutcome.REVIEW : SemanticOutcome.READ };
        // Capability ceilings derive from the model's typed objectives, never from words in the request.
        var forbidden = new List<SemanticOperation>();
        if (Draft == DraftObjective.NONE) forbidden.AddRange([SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.CREATE_DRAFT]);
        else forbidden.Add(Draft == DraftObjective.CREATE_DISTINCT_INSTRUCTION ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.CREATE_DRAFT);
        if (Execution != ExecutionObjective.RUN_LOCAL_QWEN_NOW) forbidden.Add(SemanticOperation.EXECUTE_LOCAL);
        if (Save == SaveObjective.NONE) forbidden.AddRange([SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY]);
        if (Constraints.PreserveOriginal || Draft == DraftObjective.CREATE_DISTINCT_INSTRUCTION) forbidden.Add(SemanticOperation.REQUEST_SAVE_VERSION);
        var constraints = new SemanticConstraints(forbidden.Distinct().ToArray(), Save != SaveObjective.REVIEW_PERSIST_EXISTING || Constraints.PreserveOriginal, Constraints.Language, Constraints.Format, Constraints.LiteralClauses, Constraints.TargetTerms, Constraints.Shortening, Constraints.UserProhibitions);
        var destination = Execution is ExecutionObjective.READY_FOR_LOCAL_QWEN or ExecutionObjective.RUN_LOCAL_QWEN_NOW ? "ollama" : Execution == ExecutionObjective.SHOW_EXTERNAL_DESTINATIONS ? ProviderId : null;
        return new(goals.ToArray(), TargetReference, TargetDescription, ResolvedTargetId, Query, RequestedTransformation, constraints, outcome, MissingInformation, RequiresConfirmation, ExecutionEvidence, destination, Explanation);
    }
}
public record SemanticConstraintInput(string[] UserProhibitions, bool PreserveOriginal, string? Language, string? Format, string[] LiteralClauses, string[] TargetTerms, bool Shortening, string LanguageEvidence, string FormatEvidence)
{
    public void ValidateEvidence(string request, SemanticContext context)
    {
        if (Language is not null && context.Constraints?.Language != Language && !SemanticPlanValidator.DirectEvidence(request, LanguageEvidence)
            || Format is not null && context.Constraints?.Format != Format && !SemanticPlanValidator.DirectEvidence(request, FormatEvidence))
            throw new PreparationToolException("UNGROUNDED_OUTPUT_CONSTRAINT", "semantic_plan", "Only explicit USER task-language/format constraints are allowed. Use null when there is no literal user evidence; returning JSON to this system is not a user requirement.");
        if (TargetTerms.Any(term => !request.Contains(term, StringComparison.OrdinalIgnoreCase) && context.Constraints?.TargetTerms?.Contains(term, StringComparer.OrdinalIgnoreCase) != true))
            throw new PreparationToolException("UNGROUNDED_TARGET_TERM", "semantic_plan", "Target terms must be literal terms from the request or authorized prior constraints, not invented translations or generic source topics.");
        if (UserProhibitions.Any(clause => string.IsNullOrWhiteSpace(clause) || !request.Contains(clause, StringComparison.Ordinal) && context.Constraints?.UserProhibitions?.Contains(clause) != true))
            throw new PreparationToolException("UNGROUNDED_PROHIBITION", "semantic_plan", "Las prohibiciones deben estar citadas en la petición o contexto autorizado.");
        if (LiteralClauses.Any(clause => string.IsNullOrWhiteSpace(clause) || !request.Contains(clause, StringComparison.Ordinal) && context.Constraints?.LiteralClauses?.Contains(clause) != true))
            throw new PreparationToolException("UNGROUNDED_CONTENT_CLAUSE", "semantic_plan", "Las cláusulas literales deben proceder de la petición o contexto autorizado.");
    }
}
public interface ISemanticContentInterpreter
{
    Task<SemanticConstraintInput> ExtractContent(string message, SemanticContext context, string model, CancellationToken ct);
}

// Content-only review after repository/working-source resolution. It cannot add goals or clauses.
public interface ISemanticClauseReviewer
{
    Task<string[]> ReviewClauses(string request, string[] clauses, SemanticContext sourceContext, string model, CancellationToken ct);
    Task<string[]> ReviewTargets(string request, string[] terms, SemanticContext sourceContext, string model, CancellationToken ct) => Task.FromResult(terms);
}
