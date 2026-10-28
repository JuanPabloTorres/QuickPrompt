using System.Text.RegularExpressions;

namespace QuickPrompt.Local;

public record BoundSourceTemplate(string Text, string[] RetainedConstraints, string[] BoundInputs);

// The generator owns task wording; the domain owns the actual source's input contract.
// This composes data, never infers an intent, grants effects, or invents a variable/value.
public static class TemplateSourceBinding
{
    // This validates the boundary of a code-composed artifact, not natural-language
    // intent. A core cannot repeat a protected clause or end with its truncated prefix.
    public static void ValidateEditableCore(string core, string[] protectedClauses)
    {
        static string[] Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim().TrimEnd('.', ',', ';', ':', '!', '?', '…').ToUpperInvariant()).Where(word => word.Length > 0).ToArray();
        var coreWords = Words(core);
        foreach (var clause in protectedClauses) {
            var occurrence = core.IndexOf(clause, StringComparison.OrdinalIgnoreCase);
            if (occurrence >= 0 && core.IndexOf(clause, occurrence + clause.Length, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new ValidationException("El núcleo repite una restricción protegida. Devuelve una sola instrucción completa, sin duplicar restricciones.");
            var words = Words(clause);
            for (var length = Math.Min(coreWords.Length, words.Length - 1); length >= 2; length--)
                if (coreWords.TakeLast(length).SequenceEqual(words.Take(length)))
                    throw new ValidationException("El núcleo termina con un fragmento de una restricción protegida. Devuelve una instrucción completa, sin copiar ni abreviar las restricciones que compone el dominio.");
        }
    }
    public static BoundSourceTemplate Bind(string source, string candidate) => Bind(source, candidate, null);
    public static BoundSourceTemplate Bind(string source, string candidate, IReadOnlyCollection<string>? authorizedInputs)
    {
        var names = authorizedInputs?.ToArray() ?? TemplateCompiler.Names(source);
        var addsAuthorizedInputs=source.Length>0&&authorizedInputs is not null&&names.Except(TemplateCompiler.Names(source),StringComparer.Ordinal).Any();
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
            if (!addsAuthorizedInputs && TaskWords(source) == TaskWords(candidate))
                throw new ValidationException("Conservar campos o restricciones por sí solo no constituye una adaptación / Binding source inputs or constraints alone is not an adaptation.");
        }
        if (missingInputs.Length > 0)
            text += "\n\nEntradas / Inputs:\n" + string.Join("\n", missingInputs.Select(name => $"{name}: <{name}>"));
        if (text.Length > 32000) throw new ValidationException("La propuesta con su contrato de origen excede la longitud permitida.");
        return new(text, missingClauses, missingInputs);
    }
}
