namespace QuickPrompt.Nova.Application;
public record SemanticReferenceChoice(TargetReference TargetReference, Guid? ResolvedTargetId, string Query, bool NeedsClarification, string Clarification)
{
    // Application-owned proposal selected from an offered catalog role. It is
    // not a resolved identity: the source owner must perform a fresh scoped read.
    [System.Text.Json.Serialization.JsonIgnore] public GoalTarget? ProposedCatalogSource { get; init; }
}

// Optional interpreter capability: references can be resolved before task content/use.
// Returning null advertises only combined interpretation, not guessed capability support.
public interface ISourceReferenceResolver
{
    Task<SemanticReferenceChoice?> ResolveSourceReference(string request, SemanticContext context, string model, CancellationToken ct);
}
