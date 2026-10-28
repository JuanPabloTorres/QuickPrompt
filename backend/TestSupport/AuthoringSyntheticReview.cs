using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>
/// Migrates controlled-provider composition tests through the explicit human
/// phase. This is a fixture decision, never semantic acceptance or product code.
/// References are explicitly future-task by fixture policy unless a test bound
/// a different role before generation; no expected role is inferred from output.
/// </summary>
internal static class AuthoringSyntheticReview
{
    internal static async Task<PromptProposal> GenerateAfterSyntheticReview(this IPromptGenerator generator,
        PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct,
        Action<int, string, string?>? diagnostic = null, AuthoringReferenceRole referenceRole = AuthoringReferenceRole.FUTURE_TASK)
    {
        if (input.ConfirmedBrief is null && input.Interpretation?.Goal?.AuthoringContract?.ConfirmedBrief is { } prior)
            input = input with { ConfirmedBrief = prior };
        var proposal = await generator.Generate(input, understanding, candidates, ct, diagnostic);
        if (proposal.Brief is not { } review) return proposal;
        if (proposal.TemplateText.Length != 0 || input.Interpretation!.Goal!.HasAuthoringStructure(proposal.TemplateText))
            throw new InvalidOperationException("An unconfirmed brief cannot register final content.");
        var references = review.Proposed.References.Select(item => item with {
            Role = input.Interpretation.Goal.ReferenceCoverage is { } bound
                ? bound.Uses[bound.References.IndexOf(item.Uri)].Role : referenceRole
        }).ToArray();
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed with { References = references }, true));
        return await generator.Generate(input with { ConfirmedBrief = confirmed }, understanding, candidates, ct, diagnostic);
    }
}
