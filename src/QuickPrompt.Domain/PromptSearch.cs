using System.Globalization;
using System.Text;
namespace QuickPrompt.Local;
public static class PromptSearch
{
    // Recall words can occur in different fields and in any order. Each term is literal and required.
    public static string[] Terms(string query)=>Normalize(query).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
    public static bool Matches(Template template,string query)
    {
        var content=Normalize(template.Title+" "+template.Description+" "+template.Template);
        return Terms(query).All(term=>content.Contains(term,StringComparison.Ordinal));
    }
    public static string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
}
