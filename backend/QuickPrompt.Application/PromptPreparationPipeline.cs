using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record PromptRequirements(string? Language, string? Format, string[] Constraints, string[]? TargetTerms = null, bool Shortening = false, string? EditInstruction = null, string? ContentTransformation = null, string? WorkingSourceText = null);
public record PromptEvaluation(bool Valid, string[] Errors);

// Kept separate from generation: structural facts are enforced by code; semantic quality is measured in evals.
public sealed class PromptPreparationPipeline(IPromptGenerator generator, IVariableResolver resolver, IPromptEvaluator evaluator)
{
    public Task<PromptProposal> DraftGeneration(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => generator.Generate(input, understanding, candidates, ct);
    public PreparedDraft PromptCompilation(PrepareInput input, Understanding understanding, PromptProposal proposal, RetrievalCandidate[] candidates)
    {
        // Validate IDs/schema before resolving any values. The compiler remains the domain compiler.
        var validated = evaluator.Evaluate(input, understanding, proposal, candidates, []);
        if (proposal.Decision == PreparationDecision.ASK_CLARIFICATION) return validated;
        proposal = proposal with { TemplateText = validated.TemplateText, RequiredInputs = validated.Variables.Select(v => v.Name).ToArray() };
        var source = candidates.SingleOrDefault(c => c.Prompt.Id == proposal.SourceId)?.Prompt;
        if (input.SourcePromptId is { } selected && proposal.SourceId != selected) throw new ValidationException("La propuesta no respeta el origen seleccionado.");
        return evaluator.Evaluate(input, understanding, proposal, candidates, resolver.Resolve(input, proposal, source));
    }
    public static PromptEvaluation Validation(PreparedDraft draft, PromptRequirements requirements)
    {
        if (draft.Decision == PreparationDecision.ASK_CLARIFICATION) return new(true, []);
        var errors = new List<string>();
        if (requirements.WorkingSourceText is { } working) {
            if (working.Replace("\r\n", "\n").Trim() == draft.TemplateText.Replace("\r\n", "\n").Trim()) errors.Add("ADAPTATION_REQUIRED");
            if (requirements.Shortening && draft.TemplateText.Length >= working.Length) errors.Add("SHORTENING_REQUIRED");
            if (requirements.ContentTransformation is { } change && TemplateConstraints.EchoedEdit(working, draft.TemplateText, change)) errors.Add("EDIT_REQUEST_ECHO");
        }
        if (draft.Intent is PreparationIntent.ADAPT_PROMPT or PreparationIntent.IMPROVE_PROMPT)
        {
            var original = draft.Candidates.SingleOrDefault(c => c.Prompt.Id == draft.SourceId)?.Prompt;
            if (original is null || draft.Decision != PreparationDecision.ADAPT_EXISTING || original.TemplateText.Replace("\r\n", "\n").Trim() == draft.TemplateText.Replace("\r\n", "\n").Trim()) errors.Add("ADAPTATION_REQUIRED");
            if (original is not null && requirements.Shortening && draft.TemplateText.Length >= original.TemplateText.Length) errors.Add("SHORTENING_REQUIRED");
            if (original is not null && requirements.EditInstruction is { } edit && TemplateConstraints.EchoedEdit(original.TemplateText, draft.TemplateText, edit)) errors.Add("EDIT_REQUEST_ECHO");
            if (original is not null && requirements.ContentTransformation is { } transformation && TemplateConstraints.EchoedEdit(original.TemplateText, draft.TemplateText, transformation)) errors.Add("EDIT_REQUEST_ECHO");
        }
        if (draft.TemplateText.Length > 32000 || draft.CompiledText?.Length > 64000) errors.Add("LENGTH");
        // Values and escaped literals may legitimately contain <html> or {{code}}. Only the
        // domain parser owns placeholder identity; never parse the filled text a second time.
        var names = TemplateCompiler.Names(draft.TemplateText);
        var values = draft.Values.ToDictionary(v => v.Name, v => v.Value);
        var missing = names.Where(name => !values.ContainsKey(name)).ToArray();
        if (!names.SequenceEqual(draft.Variables.Select(v => v.Name)) || !missing.SequenceEqual(draft.MissingVariables)) errors.Add("VARIABLE_INTEGRITY");
        if (missing.Length == 0 && draft.CompiledText != TemplateCompiler.Render(draft.TemplateText, values) || missing.Length > 0 && draft.CompiledText is not null) errors.Add("COMPILER_MISMATCH");
        var normalized = PromptSearch.Normalize(draft.TemplateText);
        if (draft.Intent is PreparationIntent.ADAPT_PROMPT or PreparationIntent.IMPROVE_PROMPT or PreparationIntent.CREATE_PROMPT)
            foreach (var target in requirements.TargetTerms ?? []) if (!normalized.Contains(PromptSearch.Normalize(target), StringComparison.Ordinal)) errors.Add("TARGET_CONTEXT");
        foreach (var constraint in requirements.Constraints) if (!normalized.Contains(PromptSearch.Normalize(constraint), StringComparison.Ordinal)) errors.Add("CONSTRAINT");
        if (requirements.Format is { } format && !normalized.Contains(format, StringComparison.Ordinal)) errors.Add("FORMAT");
        if (requirements.Language is { } language)
        {
            // This checks an explicit output-language instruction, not an unreliable language-detector score.
            var target = language == "es" ? @"\b(ESPANOL|SPANISH)\b" : @"\b(INGLES|ENGLISH)\b";
            if (!Regex.IsMatch(normalized, target, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) errors.Add("OUTPUT_LANGUAGE_INSTRUCTION");
        }
        return new(errors.Count == 0, errors.Distinct().ToArray());
    }
    public static PreparedDraft FinalPreparation(PreparedDraft draft, PromptRequirements requirements)
    {
        var evaluation = Validation(draft, requirements);
        if (!evaluation.Valid) throw new ValidationException("El resultado no conserva todos los requisitos. Ajusta la solicitud o vuelve a preparar.");
        return draft;
    }
}
