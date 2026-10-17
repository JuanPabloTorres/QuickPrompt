namespace QuickPrompt.Nova.Application;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SemanticTextResult { EXISTING_TEXT_UNCHANGED, EXPLANATION_WITHOUT_PROPOSAL, REWRITTEN_SOURCE_PROPOSAL, INDEPENDENT_NEW_PROMPT, NEW_PROMPT_FROM_SOURCE, NO_IDENTIFIABLE_TASK }
public record SemanticTextGoal(SemanticTextResult ResultKind, string RequestedTransformation, string Question, string Explanation, bool ContinuesPendingTask = false);
public record SemanticWorkflowObjectives(ReadObjective Access, ValuesObjective Values, ExecutionObjective Execution, ControlObjective Control, SaveObjective Save,
    string[] MissingInformation, string ExecutionEvidence, string? ProviderId, string Explanation);

// Independent real model choices are composed as typed facts; no request words are inspected here.
public static class SemanticStageComposition
{
    public static SemanticObjectives Compose(SemanticTextGoal text, SemanticWorkflowObjectives workflow, SemanticReferenceChoice reference, string sourceDescription)
    {
        var draft = text.ResultKind switch { SemanticTextResult.REWRITTEN_SOURCE_PROPOSAL => DraftObjective.REWRITE_INSTRUCTION_TEXT,
            SemanticTextResult.INDEPENDENT_NEW_PROMPT or SemanticTextResult.NEW_PROMPT_FROM_SOURCE => DraftObjective.CREATE_DISTINCT_INSTRUCTION, _ => DraftObjective.NONE };
        var access = workflow.Access; var control = workflow.Control; var missing = workflow.MissingInformation;
        if (text.ResultKind == SemanticTextResult.EXPLANATION_WITHOUT_PROPOSAL)
            return new(ReadObjective.ADVICE_WITHOUT_PROPOSAL, DraftObjective.NONE, ExecutionObjective.NONE, ControlObjective.NONE, SaveObjective.NONE,
                reference.TargetReference, sourceDescription, reference.ResolvedTargetId, reference.Query, "", [], "", null, text.Explanation);
        var hasWorkflowTask = workflow.Access != ReadObjective.NONE || workflow.Values != ValuesObjective.NONE
            || workflow.Execution != ExecutionObjective.NONE || workflow.Save != SaveObjective.NONE || workflow.Control != ControlObjective.NONE;
        // An absent instruction-writing task does not negate an independently requested
        // read/fill/prepare/execute/control workflow. Source prerequisites still validate it.
        if (text.ResultKind == SemanticTextResult.NO_IDENTIFIABLE_TASK && !hasWorkflowTask) {
            control = ControlObjective.CLARIFY; missing = [string.IsNullOrWhiteSpace(text.Question) ? "¿Qué resultado quieres preparar?" : text.Question];
        }
        if (control != ControlObjective.NONE)
            return new(ReadObjective.NONE, DraftObjective.NONE, ExecutionObjective.NONE, control, SaveObjective.NONE, reference.TargetReference, sourceDescription,
                reference.ResolvedTargetId, reference.Query, text.RequestedTransformation, control == ControlObjective.CLARIFY ? missing : [], "", null, text.Explanation);
        if (text.ResultKind == SemanticTextResult.EXISTING_TEXT_UNCHANGED && access == ReadObjective.NONE && workflow.Values == ValuesObjective.NONE
            && workflow.Execution == ExecutionObjective.NONE && workflow.Save == SaveObjective.NONE) access = ReadObjective.VIEW_UNCHANGED;
        return new(access, draft, workflow.Execution, control, workflow.Save, reference.TargetReference, sourceDescription, reference.ResolvedTargetId, reference.Query,
            text.RequestedTransformation, missing, workflow.ExecutionEvidence, workflow.ProviderId, text.Explanation, workflow.Values);
    }
}
