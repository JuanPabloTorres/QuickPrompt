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

    internal static object CurrentAuthorDecisionReady(JsonElement packet, string revisionKind = AuthoringEdits.Rewrite,
        Dictionary<string, string>? choices = null) => new {
            outcome = "READY", decisionText = (string?)null, revisionKind,
            previewChoices = choices ?? OriginalPreviewChoices(packet), translationPreparation = (object?)null
        };

    internal static object CurrentAuthorDecisionAsk(string quote) => new {
        outcome = "ASK", decisionText = quote, revisionKind = (string?)null, previewChoices = (object?)null,
        translationPreparation = (object?)null
    };

    internal sealed record TranslationField(string SourceLanguage);

    // Every language is supplied explicitly by the individual test. The host
    // owns extraction; never repair a retired Terms reply inside a transport.
    internal static object CurrentAuthorDecisionTranslate(JsonElement packet, string targetLanguage,
        Dictionary<string, TranslationField> fields, Dictionary<string, string>? choices = null) => new {
            outcome = "READY", decisionText = (string?)null, revisionKind = "TRANSLATE",
            previewChoices = choices ?? OriginalPreviewChoices(packet),
            translationPreparation = new { targetLanguage, fields }
        };

    private static Dictionary<string, string> OriginalPreviewChoices(JsonElement packet) =>
        Material(packet).GetProperty("sourcePreviews").GetProperty("fields").EnumerateObject()
            .ToDictionary(field => field.Name, field => field.Value[0].GetProperty("id").GetString()!);

    internal sealed record TranslationBinding(string CandidateId, string Word, string Casing);

    // Explicit fixtures for the composition wire. Preserve exact maps, including
    // malformed ones supplied by negative tests; never convert an old reply.
    internal static object Translation(JsonElement packet, Dictionary<string, TranslationBinding> exactBindings,
        Dictionary<string, string> exactRewrites) => new {
            basisHash = Material(packet).GetProperty("basisHash").GetString(), bindings = exactBindings, rewrites = exactRewrites
        };

    internal static object TranslateTask(JsonElement packet, string body)
    {
        var material = Material(packet);
        if (material.GetProperty("translationSlots").EnumerateObject().Any())
            throw new InvalidOperationException("Choose every lexical occurrence explicitly; TranslateTask supports only a zero-slot frame.");
        var rewrites = material.GetProperty("fields").EnumerateArray()
            .ToDictionary(field => field.GetProperty("id").GetString()!, field => field.GetProperty("text").GetString()!);
        rewrites["task"] = body;
        return Translation(packet, [], rewrites);
    }

    // Explicit fixture construction shared by both test assemblies. This is
    // never a transport converter: legacy and malicious replies stay raw.
    internal static object Revision(JsonElement packet, Dictionary<string, string?> exactMap)
    {
        var material = Material(packet);
        return new { basisHash = material.GetProperty("basisHash").GetString(),
            rewrites = exactMap.ToDictionary(item => item.Key, item => item.Value is null ? null : Encode(material, item.Value)) };
    }

    internal static object RewriteTask(JsonElement packet, string body)
    {
        var fields = Material(packet).GetProperty("fields").EnumerateArray()
            .ToDictionary(field => field.GetProperty("id").GetString()!, _ => (string?)null);
        fields["task"] = body;
        return Revision(packet, fields);
    }

    internal static Dictionary<string, string?> WriterFields(JsonElement packet, string fieldProperty = "fields")
    {
        var material = Material(packet);
        return material.GetProperty(fieldProperty).EnumerateArray().ToDictionary(field => field.GetProperty("id").GetString()!, field => {
            var text = field.GetProperty("text").GetString()!;
            foreach (var input in material.GetProperty("inputs").EnumerateArray())
                text = text.Replace(input.GetProperty("token").GetString()!, "<" + input.GetProperty("label").GetString() + ">", StringComparison.Ordinal);
            return (string?)text;
        });
    }

    private static JsonElement Material(JsonElement packet) => JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);

    private static string Encode(JsonElement material, string text)
    {
        foreach (var input in material.GetProperty("inputs").EnumerateArray())
            text = text.Replace("<" + input.GetProperty("label").GetString() + ">", input.GetProperty("token").GetString(), StringComparison.Ordinal);
        return text;
    }

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
