using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class AuthoringTranslationComposition
{
    private static readonly Regex Words = new(@"\p{L}[\p{L}\p{M}]*(?:[-'’]\p{L}[\p{L}\p{M}]*)*",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static string MaskField(string fieldId, int ordinal, string source, string? resourceId, JsonElement? resource,
        Dictionary<string, Slot> slots, CancellationToken cancellation)
    {
        var masked = new System.Text.StringBuilder();
        var cursor = 0;
        var inputs = Inputs.Matches(source).ToArray();
        foreach (Match word in Words.Matches(source)) {
            cancellation.ThrowIfCancellationRequested();
            if (inputs.Any(input => word.Index >= input.Index && word.Index < input.Index + input.Length)) continue;
            var slotId = "s" + ordinal + "_" + word.Index + "_" + word.Length;
            var candidates = Candidates(slotId, word.Value, resource, cancellation);
            if (candidates.Length == 0) continue;
            var token = WordPrefix + slotId + "⟫";
            candidates = [.. candidates, new("KEEP_SOURCE", word.Value, "SOURCE_LITERAL", [])];
            slots.Add(slotId, new(fieldId, token, new(word.Index, word.Length), word.Value, resourceId!, candidates));
            masked.Append(source[cursor..word.Index]).Append(token);
            cursor = word.Index + word.Length;
        }
        cancellation.ThrowIfCancellationRequested();
        return masked.Append(source[cursor..]).ToString();
    }
    private static Candidate[] Candidates(string slotId, string word, JsonElement? resource, CancellationToken cancellation)
    {
        if (resource is not { } data) return [];
        try {
            if (data.GetProperty("sourceLanguage").GetString() != "es" || data.GetProperty("targetLanguage").GetString() != "en") return [];
            var term = word.Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
            if (!data.GetProperty("lookup").TryGetProperty(term, out var matches) || matches.GetArrayLength() == 0) return [];
            var found = new Dictionary<string, (string Headword, List<object[]> Edges)>(StringComparer.Ordinal);
            var onlyVerbs = true;
            foreach (var address in matches.EnumerateArray()) {
                cancellation.ThrowIfCancellationRequested();
                if (address.GetArrayLength() != 5) throw new ValidationException("La arista léxica no conserva su dirección completa.");
                var entryId = address[0].GetInt32(); var senseId = address[1].GetInt32();
                var headwordIndex = address[2].GetInt32(); var translationIndex = address[3].GetInt32();
                var literal = address[4].GetString() ?? throw new ValidationException("La arista léxica no conserva su literal fuente.");
                var entry = data.GetProperty("entries").GetProperty(entryId.ToString(CultureInfo.InvariantCulture));
                var pos = entry[1];
                if (pos.GetArrayLength() == 0 || pos.EnumerateArray().Any(value => value.GetString() != "v")) onlyVerbs = false;
                if (entryId < 1 || senseId < 1 || headwordIndex < 0 || translationIndex < 0
                    || literal.Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant() != term
                    || !entry[2].TryGetProperty(senseId.ToString(CultureInfo.InvariantCulture), out _))
                    throw new ValidationException("La acepción léxica no pertenece al término consultado.");
                var headword = entry[0][headwordIndex].GetString()!;
                if (string.IsNullOrWhiteSpace(headword)) throw new ValidationException("La acepción léxica no tiene palabra de origen.");
                var id = slotId + "/e" + entryId + "s" + senseId + "w" + headwordIndex;
                if (!found.TryGetValue(id, out var candidate)) {
                    candidate = (headword, []); found.Add(id, candidate);
                }
                candidate.Edges.Add([entryId, senseId, headwordIndex, translationIndex, literal]);
            }
            return onlyVerbs ? found.Select(item => new Candidate(item.Key, item.Value.Headword, "LEXICAL_EDGE", item.Value.Edges.ToArray())).ToArray() : [];
        } catch (Exception failure) when (failure is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or ArgumentNullException) {
            throw new ValidationException("La evidencia léxica tiene direcciones o tipos inválidos.");
        }
    }
}
