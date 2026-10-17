using System.Text.RegularExpressions;

namespace QuickPrompt.Local;

public record BoundSourceTemplate(string Text, string[] RetainedConstraints, string[] BoundInputs);

// The generator owns task wording; the domain owns the actual source's input contract.
// This composes data, never infers an intent, grants effects, or invents a variable/value.
public static class TemplateSourceBinding
{
    public static BoundSourceTemplate Bind(string source, string candidate)
    {
        var names = TemplateCompiler.Names(source);
        if (TemplateCompiler.Names(candidate).Except(names, StringComparer.Ordinal).Any())
            throw new ValidationException("La propuesta introdujo variables ajenas al origen / Proposal introduced unknown source inputs.");
        var missingClauses = TemplateConstraints.Missing(source, candidate);
        var text = candidate.TrimEnd();
        if (missingClauses.Length > 0) text += "\n\n" + string.Join("\n", missingClauses);
        var missingInputs = names.Except(TemplateCompiler.Names(text), StringComparer.Ordinal).ToArray();
        if (missingClauses.Length > 0 || missingInputs.Length > 0) {
            var clauses = TemplateConstraints.Required(source);
            string TaskWords(string value) {
                foreach (var clause in clauses) value = value.Replace(clause, "", StringComparison.Ordinal);
                value = string.Concat(TemplateCompiler.Parse(value).Where(token => token.Name is null).Select(token => token.Text));
                return Regex.Replace(PromptSearch.Normalize(value), @"[\p{P}\s]+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Trim();
            }
            if (TaskWords(source) == TaskWords(candidate))
                throw new ValidationException("Conservar campos o restricciones por sí solo no constituye una adaptación / Binding source inputs or constraints alone is not an adaptation.");
        }
        if (missingInputs.Length > 0)
            text += "\n\nEntradas / Inputs:\n" + string.Join("\n", missingInputs.Select(name => $"{name}: <{name}>"));
        if (text.Length > 32000) throw new ValidationException("La propuesta con su contrato de origen excede la longitud permitida.");
        return new(text, missingClauses, missingInputs);
    }
}
