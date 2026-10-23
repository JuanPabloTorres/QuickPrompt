using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Nova.Evaluation.Retired;

// Rejected decoder protocol, retained for reproducible diagnostics only.
public record TaskOperationEvidence(SemanticOperation Operation, string Evidence);
public record TaskRequestConstraints(string? Language, string? Format, string LanguageEvidence, string FormatEvidence,
    string[] LiteralClauses, string[] TargetTerms, string[] UserProhibitions, bool Shortening);
public record TaskExecutionRequest(TaskOperationEvidence[] Effects, TaskOperationEvidence[] Forbidden, TargetReference TargetReference, Guid? TargetId,
    string Query, Dictionary<string, string> Values, string ContentEvidence, TaskRequestConstraints Constraints,
    string? ProviderId, bool ContinuesPendingTask, string Question, string Explanation)
{
    public UserActionRequest? UserActions { get; init; }
}
