using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

public sealed record StartFactualReview(Guid OperationId, Guid PromptId, int Revision, Guid ConnectionId,
    Dictionary<string, string> Values, string[] SourceFields, string Title);
public sealed record ConfirmFactualReview(long Version, bool Confirmed, FactualStatement[] Facts, string Title);
public sealed record FactualDecision(long Version, bool Confirmed = false);
public sealed record ForgetFactualDocument(bool Confirmed);
public sealed record FactualReviewView(Guid Id, Guid LibraryId, Guid PromptId, int PromptRevision, Guid ConnectionId,
    long Version, string Status, string Title, Dictionary<string, string> SourceFields, FactualStatement[] ProposalFacts,
    FactualStatement[] ConfirmedFacts, string? Preview, Guid? DocumentId, bool Edited, DateTimeOffset ExpiresAt, string? ErrorCode);
public sealed record FactualDocumentSummary(Guid Id, Guid LibraryId, Guid ReviewId, Guid PromptId, int PromptRevision,
    string Title, bool Edited, DateTimeOffset CreatedAt);
public sealed record FactualDocumentView(Guid Id, Guid LibraryId, Guid ReviewId, Guid PromptId, int PromptRevision,
    string Title, string Text, FactualStatement[] Facts, FactualStatement[] ProposalFacts, bool Edited, string Model, DateTimeOffset CreatedAt,
    Dictionary<string, string> SourceFields);
public sealed record FactualDocumentPage(FactualDocumentSummary[] Items, int Total, int Page, int Limit);
