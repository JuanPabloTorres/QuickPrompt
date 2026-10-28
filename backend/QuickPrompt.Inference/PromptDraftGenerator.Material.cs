using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    // Only content reaches the writer. Identity, revision, permissions, approval,
    // diagnostic codes and registration remain in the existing host contract.
    private record FutureSource(string Title, string Description, string TemplateText);
    private record ContentReferences(string[] Future, string[] Writing);
    private record LiteralTextChoice(int Id, string Text);
    private record ContentPreferences(string? Language, string? Format);
    private record ContentConstraints(bool Shortening, string? Language, string? Format);
    private record ContentCorrection(string[] Inputs, ContentReferences References);
    private record FutureTaskMaterial(string OriginalRequest, AuthoringClarification[] UserClarifications, FutureSource? SelectedSource, string[] Inputs,
        LiteralTextChoice[] LiteralBodyCandidates, ContentReferences References, ContentPreferences? ApprovedPreferences,
        ContentConstraints ContentConstraints, ContentCorrection? CorrectionRequirements);

    private static FutureTaskMaterial ContentMaterial(PrepareInput input, DraftProposalContract contract, bool correction)
    {
        var coverage = input.Interpretation!.Goal!.ReferenceCoverage;
        var future = coverage?.References.Where((_, index) => coverage.Uses[index].Role != AuthoringReferenceRole.CURRENT_SESSION).ToArray() ?? [];
        var writing = coverage?.References.Where((_, index) => coverage.Uses[index].Role == AuthoringReferenceRole.CURRENT_SESSION).ToArray() ?? [];
        var references = new ContentReferences(future, writing);
        var inputs = contract.FieldContract.Names.Select(name => "<" + name + ">").ToArray();
        var source = contract.SourceSnapshot;
        var constraints = input.Interpretation.Constraints;
        var preferences = input.ApprovedPreferences;
        return new(contract.AuthoringRequest, contract.UserClarifications, source is null ? null : new(source.Title, source.Description, source.TemplateText),
            inputs, contract.Literals.Select(item => new LiteralTextChoice(item.Id, item.Text)).ToArray(), references,
            preferences is null ? null : new(preferences.Language, preferences.Format),
            new(constraints.Shortening, constraints.Language, constraints.Format), correction ? new(inputs, references) : null);
    }
}
