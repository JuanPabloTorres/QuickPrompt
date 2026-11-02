using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>Read-only source-language observations, with exact field and source bindings. No English draft or effect authority.</summary>
public interface IAuthoringTranslationSourceViews
{
    Task<JsonElement> Read(IReadOnlyDictionary<string, string> fields, CancellationToken cancellation);
}

/// <summary>Uses the existing pinned finite-state resource without selecting a language or a dictionary meaning.</summary>
public interface IAuthoringTranslationSourceLexicon : IAuthoringTranslationSourceViews
{
    JsonElement ReadLexical(JsonElement observed, string[] ids, CancellationToken cancellation);
}

public sealed class ApertiumTranslationSourceViews : IAuthoringTranslationSourceLexicon
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly Regex Literals = new(@"⟪literal:[0-9]+:[0-9]+⟫", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Inputs = new(@"⟦[0-9]+⟧", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Units = new(@"\^([^^$]*)\$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly ApertiumFrameRunner runner;
    private ApertiumTranslationSourceViews(ApertiumFrameRunner runner) => this.runner = runner;
    public static ApertiumTranslationSourceViews Docker(string image) => new(ApertiumFrameRunner.Docker(image));
    public static ApertiumTranslationSourceViews Native() => new(ApertiumFrameRunner.Native());

    public async Task<JsonElement> Read(IReadOnlyDictionary<string, string> fields, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (fields.Count is < 1 or > 64 || fields.Any(field => field.Value is null || field.Value.Contains('\0'))
            || fields.Sum(field => Encoding.UTF8.GetByteCount(field.Value)) > 64000)
            throw new ValidationException("La fuente lingüística excede el formato o alcance admitido.");
        var restore = new Dictionary<string, string>(StringComparer.Ordinal);
        var number = 1000000;
        var masked = fields.Values.Select(text => Literals.Replace(text, match => {
            var token = "⟦" + number++ + "⟧";
            restore.Add(token, match.Value);
            return token;
        })).ToArray();
        if (fields.Values.Any(text => restore.Keys.Any(token => text.Contains(token, StringComparison.Ordinal))))
            throw new ValidationException("La fuente usa un enlace lingüístico reservado.");
        var portuguese = await runner.Run(ApertiumOperation.PortugueseAnalysis, masked, cancellation);
        var spanish = await runner.Run(ApertiumOperation.SpanishAnalysis, masked, cancellation);
        var pivot = await runner.Run(ApertiumOperation.PortugueseSpanish, masked, cancellation);
        var pivotAnalysis = await runner.Run(ApertiumOperation.SpanishAnalysis, pivot, cancellation);
        var keys = fields.Keys.ToArray();
        var lemmas = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var observations = new List<object>();
        for (var index = 0; index < keys.Length; index++) {
            cancellation.ThrowIfCancellationRequested();
            var bound = Inputs.Matches(masked[index]).Select(match => match.Value).SequenceEqual(
                Inputs.Matches(pivot[index]).Select(match => match.Value), StringComparer.Ordinal);
            var text = bound ? Inputs.Replace(pivot[index], match => restore.GetValueOrDefault(match.Value) ?? match.Value) : null;
            lemmas.Add(keys[index], ContentLemmas(spanish[index]).Concat(ContentLemmas(pivotAnalysis[index])).Distinct(StringComparer.Ordinal).ToArray());
            observations.Add(new { id = keys[index], originalText = fields[keys[index]],
                sourceSha256 = SourceHash(fields[keys[index]]),
                portugueseToSpanish = new { status = bound ? "OBSERVED" : "BINDING_MISMATCH_NO_VIEW", text },
                morphology = new { pt = portuguese[index], es = spanish[index], pivotEs = pivotAnalysis[index] },
                coverage = new { pt = Coverage(portuguese[index]), es = Coverage(spanish[index]) } });
        }
        return JsonSerializer.SerializeToElement(new { status = "OBSERVED", engine = runner.Identity, fields = observations,
            lexicalCatalog = lemmas.Values.SelectMany(value => value).Distinct(StringComparer.Ordinal)
                .Select((term, index) => new { id = "w" + index, term, fields = lemmas.Where(field => field.Value.Contains(term, StringComparer.Ordinal)).Select(field => field.Key).ToArray() }).ToArray(),
            opaqueLiteralMap = restore.Select(item => new { toolToken = item.Key, sourceToken = item.Value }).ToArray(),
            scope = "SOURCE_LANGUAGE_HYPOTHESES_NOT_LANGUAGE_CERTIFICATION_ENGLISH_TRANSLATION_SEMANTIC_APPROVAL_OR_EFFECTS" }, Json);
    }

    public JsonElement ReadLexical(JsonElement observed, string[] ids, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var catalog = observed.GetProperty("lexicalCatalog").EnumerateArray().ToDictionary(
            row => row.GetProperty("id").GetString()!, row => row.GetProperty("term").GetString()!, StringComparer.Ordinal);
        if (ids.Length > 6 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !catalog.ContainsKey(id)))
            throw new ValidationException("La consulta no corresponde al catálogo observado o excede seis lecturas.");
        return JsonSerializer.SerializeToElement(new { selectedIds = ids,
            evidence = LocalTranslationLexicon.Default.ReadContext("es", "en", ids.Select(id => catalog[id]).ToArray(), cancellation) }, Json);
    }

    private static string[] ContentLemmas(string analysis) => Units.Matches(analysis)
        .SelectMany(unit => unit.Groups[1].Value.Split('/').Skip(1))
        .Where(option => Regex.IsMatch(option, @"<(?:n|vblex|adj|adv)>", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        .Select(option => option.Split('<', 2)[0]).Where(lemma => !string.IsNullOrWhiteSpace(lemma)).Distinct(StringComparer.Ordinal).ToArray();

    internal static string SourceHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static object Coverage(string analysis)
    {
        var known = 0; var total = 0;
        foreach (Match unit in Units.Matches(analysis)) {
            var value = unit.Groups[1].Value; var slash = value.IndexOf('/');
            if (slash < 1) continue;
            var letters = value[..slash].Count(char.IsLetter);
            total += letters;
            if (value[(slash + 1)..].Split('/').Any(option => !option.StartsWith('*'))) known += letters;
        }
        return new { knownLetters = known, totalLetters = total };
    }
}

public sealed partial class AuthoringRevisionEditor
{
    private static void ValidateSourceViews(IReadOnlyDictionary<string, string> source, JsonElement views)
    {
        if (views.ValueKind != JsonValueKind.Object || !views.TryGetProperty("status", out var status)
            || status.GetString() != "OBSERVED" || !views.TryGetProperty("fields", out var fields)
            || fields.ValueKind != JsonValueKind.Array || fields.GetArrayLength() != source.Count)
            throw new ValidationException("Las observaciones lingüísticas no corresponden a todos los campos.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields.EnumerateArray()) {
            if (!field.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || !seen.Add(id.GetString()!) || !source.TryGetValue(id.GetString()!, out var text)
                || !field.TryGetProperty("originalText", out var original) || original.GetString() != text
                || !field.TryGetProperty("sourceSha256", out var hash)
                || hash.GetString() != ApertiumTranslationSourceViews.SourceHash(text))
                throw new ValidationException("Una observación lingüística perdió su enlace a la fuente original.");
        }
    }
}
