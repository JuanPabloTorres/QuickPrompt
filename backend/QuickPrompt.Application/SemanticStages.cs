namespace QuickPrompt.Nova.Application;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SemanticTextResult { EXISTING_TEXT_UNCHANGED, EXPLANATION_WITHOUT_PROPOSAL, REWRITTEN_SOURCE_PROPOSAL, INDEPENDENT_NEW_PROMPT, NEW_PROMPT_FROM_SOURCE, NO_IDENTIFIABLE_TASK }
public record SemanticTextGoal(SemanticTextResult ResultKind, string RequestedTransformation, string Question, string Explanation, bool ContinuesPendingTask = false);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum InstructionAuthorship { TEMPLATE_UNCHANGED, REVISE_TEMPLATE, CREATE_TEMPLATE }

// Process-owned semantic decision. ContentInstruction preserves the literal
// scoped authoring directive, never a model-authored replacement or permission.
public sealed record InstructionAuthoring(InstructionAuthorship Kind,
    [property: AgentRepresentation(AgentRepresentation.LiteralAuthoringDirective, "The literal current-session authoring directive outside case data. Despite the legacy member name, this is NOT an already normalized future instruction. Author its requested reusable task; do not simply copy the request to create this artifact or the current session's execution/saving controls into its body. The full user work order remains the authority for verification.")] string ContentInstruction, string Evidence,
    string RequestHash, string? PendingRequestHash = null, bool EvidenceFromPending = false)
{
    public bool ContinuesPendingTask => PendingRequestHash is not null;
    public void ValidateFor(SemanticInterpretation plan, string request, string? pendingRequest = null)
    {
        if (!Enum.IsDefined(Kind) || RequestHash != LiveRuns.Hash(request) || EvidenceFromPending && PendingRequestHash is null
            || PendingRequestHash is not null && (pendingRequest is null || PendingRequestHash != LiveRuns.Hash(pendingRequest)))
            throw new QuickPrompt.Local.ValidationException("El encargo pertenece a otra petición o tarea pendiente.");
        var evidenceRequest = EvidenceFromPending ? pendingRequest! : request;
        if (Kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? ContentInstruction.Length != 0 || Evidence.Length != 0
            : string.IsNullOrWhiteSpace(ContentInstruction) || ContentInstruction.Length > 16000
                || string.IsNullOrWhiteSpace(Evidence) || !evidenceRequest.Contains(Evidence, StringComparison.Ordinal))
            throw new QuickPrompt.Local.ValidationException("Encargo de contenido sin evidencia vinculada.");
        var effects = plan.DeferredGoals ?? plan.Intentions;
        var revises = effects.Contains(SemanticOperation.PROPOSE_ADAPTATION);
        var creates = effects.Contains(SemanticOperation.CREATE_DRAFT);
        if (Kind switch { InstructionAuthorship.TEMPLATE_UNCHANGED => revises || creates,
            InstructionAuthorship.REVISE_TEMPLATE => !revises || creates, _ => !creates || revises })
            throw new QuickPrompt.Local.ValidationException("El plan contradice la decisión de autoría e identidad.");
    }
}
// The literal request owns these source-independent goals. Loading a document
// can refine its content/input roles, but cannot introduce clipboard/write/control goals.
public record WorkflowGoalSnapshot(string UserRequest, ReadObjective Access, ControlObjective Control, SaveObjective Save);
public record SemanticWorkflowObjectives(ReadObjective Access, ValuesObjective Values, ExecutionObjective Execution, ControlObjective Control, SaveObjective Save,
    string[] MissingInformation, string ExecutionEvidence, string? ProviderId, string Explanation)
{
    [System.Text.Json.Serialization.JsonIgnore] public string? DeletionEvidence { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public string? ClipboardEvidence { get; init; }

}

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
        var hasWorkflowTask = workflow.Access != ReadObjective.NONE || workflow.Values != ValuesObjective.NONE || workflow.Execution != ExecutionObjective.NONE || workflow.Control != ControlObjective.NONE || workflow.Save != SaveObjective.NONE;
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
            text.RequestedTransformation, missing, workflow.ExecutionEvidence, workflow.ProviderId, text.Explanation, workflow.Values) { DeletionEvidence = workflow.DeletionEvidence, ClipboardEvidence = workflow.ClipboardEvidence };
    }
}
