using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    // Discovery can inspect author decisions. The writer receives only the
    // established future fields and explicit editing preferences. Original
    // requests, source provenance and answers stay out of its instruction body.
    private record FutureSource(string Title, string Description, string TemplateText);
    private record ContentReferences(string[] Future, string[] Writing);
    private record LiteralTextChoice(int Id, string Text);
    private record ContentPreferences(string? Language, string? Format);
    private record ContentConstraints(bool Shortening, string? Language, string? Format);
    private record ContentCorrection(string[] Inputs, ContentReferences References);
    private record FutureTaskMaterial(string OriginalRequest, AuthoringClarification[] UserClarifications, FutureSource? SelectedSource, string[] Inputs,
        LiteralTextChoice[] LiteralBodyCandidates, ContentReferences References, ContentPreferences? ApprovedPreferences,
        ContentConstraints ContentConstraints, ContentCorrection? CorrectionRequirements, FutureInstruction? EstablishedTask);
    private record FieldRevisionMaterial(string Title, string Description, Dictionary<string, string> Fields, string[] Inputs,
        LiteralTextChoice[] LiteralBodyCandidates, ContentReferences References, ContentPreferences? ApprovedPreferences,
        ContentConstraints ContentConstraints, ContentCorrection? CorrectionRequirements);

    private static FieldRevisionMaterial RevisionMaterial(FutureTaskMaterial discovery, AuthoringBriefContent established) =>
        new(established.Title, established.Description, EstablishedFields(established), discovery.Inputs,
            // Discovery sees the source inventory. A quote becomes a writer
            // copy capability only when the human established that exact task.
            discovery.LiteralBodyCandidates.Where(item => item.Text == established.Task).ToArray(), discovery.References, discovery.ApprovedPreferences,
            discovery.ContentConstraints, discovery.CorrectionRequirements);

    private static FutureTaskMaterial ContentMaterial(PrepareInput input, DraftProposalContract contract, bool correction)
    {
        var coverage = input.Interpretation!.Goal!.ReferenceCoverage;
        var established = contract.ConfirmedBrief?.Review.Stage == AuthoringBriefs.EstablishTask ? contract.ConfirmedBrief.Content : null;
        var future = established is not null ? established.References.Where(item => item.Role != AuthoringReferenceRole.CURRENT_SESSION).Select(item => item.Uri).ToArray()
            : coverage?.References.Where((_, index) => coverage.Uses[index].Role != AuthoringReferenceRole.CURRENT_SESSION).ToArray() ?? [];
        var writing = established is not null ? established.References.Where(item => item.Role == AuthoringReferenceRole.CURRENT_SESSION).Select(item => item.Uri).ToArray()
            : coverage?.References.Where((_, index) => coverage.Uses[index].Role == AuthoringReferenceRole.CURRENT_SESSION).ToArray() ?? [];
        var references = new ContentReferences(future, writing);
        var inputs = contract.FieldContract.Names.Select(name => "<" + name + ">").ToArray();
        var source = contract.SourceSnapshot;
        var constraints = input.Interpretation.Constraints;
        var preferences = input.ApprovedPreferences;
        return new(contract.AuthoringRequest, contract.UserClarifications, source is null ? null : new(source.Title, source.Description, source.TemplateText),
            inputs, contract.Literals.Select(item => new LiteralTextChoice(item.Id, item.Text)).ToArray(), references,
            preferences is null ? null : new(preferences.Language, preferences.Format),
            new(constraints.Shortening, constraints.Language, constraints.Format), correction ? new(inputs, references) : null,
            established is null ? null : new(established.Task, established.Steps, established.Constraints, established.OutputRequirements));
    }
}
