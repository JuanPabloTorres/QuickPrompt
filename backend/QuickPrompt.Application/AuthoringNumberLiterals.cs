using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>
/// Conservative lexical conservation for source-only compression, not a numeric
/// interpreter. Digit notation and the declared ES/EN cardinal lexemes are kept
/// literally; ambiguous words may be retained conservatively. Articles un/una
/// are excluded. Variable names and their indices are never numeric case facts.
/// </summary>
public static class AuthoringNumberLiterals
{
    private static readonly Regex Tokens = new(@"[+-]?\p{Nd}+(?:[.,]\p{Nd}+)*|\p{L}+",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly HashSet<string> Cardinals = new((
        "cero uno dos tres cuatro cinco seis siete ocho nueve diez once doce trece catorce quince " +
        "dieciséis dieciseis diecisiete dieciocho diecinueve veinte veintiuno veintidós veintidos veintitrés veintitres " +
        "veinticuatro veinticinco veintiséis veintiseis veintisiete veintiocho veintinueve treinta cuarenta cincuenta " +
        "sesenta setenta ochenta noventa cien ciento cientos doscientos trescientos cuatrocientos quinientos " +
        "seiscientos setecientos ochocientos novecientos mil millón millon millones " +
        "zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen " +
        "seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred thousand million billion"
        ).Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    public static string[] Read(string text)
    {
        var variables = TemplateEditor.Spans(text).ToArray();
        return Tokens.Matches(text).Where(match => !variables.Any(span => match.Index >= span.Start && match.Index < span.Start + span.Length))
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Any(char.IsDigit) || Cardinals.Contains(token)).Distinct(StringComparer.Ordinal).ToArray();
    }

    public static bool Conserved(string original, string revised) => Read(original).ToHashSet(StringComparer.Ordinal).SetEquals(Read(revised));
}
