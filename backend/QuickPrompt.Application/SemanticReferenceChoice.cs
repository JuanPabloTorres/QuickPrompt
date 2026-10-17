namespace QuickPrompt.Nova.Application;
public record SemanticReferenceChoice(TargetReference TargetReference, Guid? ResolvedTargetId, string Query, bool NeedsClarification, string Clarification);
