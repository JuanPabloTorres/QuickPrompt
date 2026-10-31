using System.Text.Json;
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
    // Controlled providers opt into READY for the discovery contract explicitly.
    // This is not a semantic judgment about a request or an application grant.
    internal static bool IsCurrentAuthorDecision(JsonElement packet)
    {
        if (!packet.TryGetProperty("format", out var schema) || !schema.TryGetProperty("anyOf", out var branches)) return false;
        return branches.EnumerateArray().Any(branch => branch.TryGetProperty("properties", out var fields)
            && fields.TryGetProperty("decisionText", out _));
    }

    internal static object CurrentAuthorDecisionReady() => new { outcome = "READY", decisionText = (string?)null };

    // Explicit fixture actor: establish a task before testing the controlled
    // provider. Copying the projected work order here is not semantic grading.
    internal static async Task<PromptProposal> GenerateAfterSyntheticTask(this IPromptGenerator generator,
        PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct,
        Action<int, string, string?>? diagnostic = null, AuthoringReferenceRole referenceRole = AuthoringReferenceRole.FUTURE_TASK,
        AuthoringBriefContent? establishedContent = null)
    {
        var proposal = await generator.Generate(input, understanding, candidates, ct, diagnostic);
        if (proposal.Brief is not { Stage: AuthoringBriefs.EstablishTask } review) return proposal;
        var references = review.Proposed.References.Select(item => item with {
                Role = input.Interpretation!.Goal!.ReferenceCoverage is { } bound && bound.References.Contains(item.Uri)
                    ? bound.Uses[bound.References.IndexOf(item.Uri)].Role : referenceRole
            }).ToArray();
        var task = string.IsNullOrWhiteSpace(review.Proposed.Task) ? review.OriginalRequest : review.Proposed.Task;
        // This controlled actor explicitly excludes its session-only links from
        // the future task. The production source/request remain unchanged.
        foreach (var reference in references.Where(item => item.Role == AuthoringReferenceRole.CURRENT_SESSION))
            task = task.Replace(reference.Uri, "", StringComparison.Ordinal);
        var content = (establishedContent ?? review.Proposed with { Task = task }) with { References = references };
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, content, true));
        return await generator.Generate(input with { ConfirmedBrief = confirmed }, understanding, candidates, ct, diagnostic);
    }

    internal static async Task<PromptProposal> GenerateAfterSyntheticReview(this IPromptGenerator generator,
        PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct,
        Action<int, string, string?>? diagnostic = null, AuthoringReferenceRole referenceRole = AuthoringReferenceRole.FUTURE_TASK,
        AuthoringBriefContent? establishedContent = null)
    {
        if (input.ConfirmedBrief is null && input.Interpretation?.Goal?.AuthoringContract?.ConfirmedBrief is { } prior)
            input = input with { ConfirmedBrief = prior };
        var proposal = await generator.GenerateAfterSyntheticTask(input, understanding, candidates, ct, diagnostic, referenceRole, establishedContent);
        if (proposal.Brief is not { } review) return proposal;
        if (proposal.TemplateText.Length != 0 || input.Interpretation!.Goal!.HasAuthoringStructure(proposal.TemplateText))
            throw new InvalidOperationException("An unconfirmed brief cannot register final content.");
        // The fixture explicitly elects the provider's controlled suggestion;
        // production leaves the established contract selected by default.
        var chosen = review.Suggested ?? review.Proposed;
        var references = chosen.References.Select(item => item with {
            Role = input.Interpretation.Goal.ReferenceCoverage is { } bound
                ? bound.Uses[bound.References.IndexOf(item.Uri)].Role : referenceRole
        }).ToArray();
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen with { References = references }, true));
        return await generator.Generate(input with { ConfirmedBrief = confirmed }, understanding, candidates, ct, diagnostic);
    }
}
