using System.Text.RegularExpressions;

namespace QuickPrompt.Local;

// Conservative, independently checkable prohibitions and explicit preservation clauses. No model quality score.
public static class TemplateConstraints
{
    public static string[] Required(string source) => Regex.Matches(source,
        @"(?:^|(?<=[.;\r\n]))\s*((?:No |Nunca |Sin |Never |Do not |Conserva |Mant[eé]n |Preserva |Preserve |Keep )[^.;\r\n]{1,32000}[.;]?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
        .Select(match => match.Groups[1].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string[] Missing(string source, string proposed)
    {
        var normalized = PromptSearch.Normalize(proposed);
        return Required(source).Where(clause => !normalized.Contains(PromptSearch.Normalize(clause), StringComparison.Ordinal)).ToArray();
    }
    public static bool EchoedEdit(string source, string proposed, string request)
    {
        var original = source.Trim(); var draft = proposed.Trim();
        if (original.Length == 0 || !draft.StartsWith(original, StringComparison.Ordinal)) return false;
        var remaining = PromptSearch.Normalize(draft[original.Length..]).Trim().TrimEnd('.', '!', '?');
        return remaining.Length > 0 && remaining == PromptSearch.Normalize(request).Trim().TrimEnd('.', '!', '?');
    }
}
