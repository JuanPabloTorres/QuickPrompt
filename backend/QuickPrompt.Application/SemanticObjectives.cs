namespace QuickPrompt.Nova.Application;

public record SemanticObjectives(ReadObjective Access, DraftObjective Draft, ExecutionObjective Execution, ControlObjective Control, SaveObjective Save,
    TargetReference TargetReference, string TargetDescription, Guid? ResolvedTargetId, string Query, string RequestedTransformation,
    string[] MissingInformation, string ExecutionEvidence, string? ProviderId, string Explanation, ValuesObjective Values = ValuesObjective.NONE)
{
    [System.Text.Json.Serialization.JsonIgnore] public string? DeletionEvidence { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public string? ClipboardEvidence { get; init; }
    public SemanticDecision WithConstraints(SemanticConstraintInput constraints) => new(Access, Draft, Execution, Control, Save, TargetReference, TargetDescription,
        ResolvedTargetId, Query, RequestedTransformation, constraints, MissingInformation, Save != SaveObjective.NONE, ExecutionEvidence, ProviderId, Explanation, Values) { DeletionEvidence = DeletionEvidence, ClipboardEvidence = ClipboardEvidence };
}
