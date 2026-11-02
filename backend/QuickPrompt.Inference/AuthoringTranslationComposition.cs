using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>
/// Translates whole masked fields with closed lexical bindings. Marker provenance
/// and order are structural checks, never proof of grammar, meaning or approval.
/// </summary>
public sealed partial class AuthoringTranslationComposition
{
    private const string WordPrefix = "⟪lex:";
    private static readonly Regex WordMarkers = new(@"⟪lex:s[0-9]+_[0-9]+_[0-9]+⟫", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Inputs = new(@"⟦(?:0|[1-9][0-9]*)⟧", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SequenceMarkers = new(@"⟪lex:s[0-9]+_[0-9]+_[0-9]+⟫|⟦(?:0|[1-9][0-9]*)⟧", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly string basisHash;
    private readonly Dictionary<string, string> originalFields;
    private readonly Dictionary<string, string> maskedFields;
    private readonly Dictionary<string, Slot> slots;
    private readonly Dictionary<string, Slot> slotsByToken;
    private readonly HashSet<string> originalInputs;

    private AuthoringTranslationComposition(string basisHash, Dictionary<string, string> originalFields,
        Dictionary<string, string> maskedFields, Dictionary<string, Slot> slots)
    {
        this.basisHash = basisHash; this.originalFields = originalFields; this.maskedFields = maskedFields; this.slots = slots;
        slotsByToken = slots.Values.ToDictionary(slot => slot.Token, StringComparer.Ordinal);
        originalInputs = originalFields.Values.SelectMany(source => Inputs.Matches(source).Select(input => input.Value)).ToHashSet(StringComparer.Ordinal);
        Schema = BuildSchema();
        Material = JsonSerializer.SerializeToElement(new { basisHash,
            fields = maskedFields.Select(field => new { id = field.Key, text = field.Value }).ToArray(), slots,
            scope = "MASKED_FIELD_PROPOSAL_NOT_SEMANTIC_APPROVAL" }, Json);
    }

    public JsonElement Schema { get; }
    public JsonElement Material { get; }

    /// <summary>
    /// Captures the selected encoded source and masks only occurrence-bound lexical
    /// choices. Resources are ReadContext data. The exact namespace ⟪lex: is reserved;
    /// unrelated mathematical delimiters remain literal. No inference or E/S occurs.
    /// </summary>
    public static AuthoringTranslationComposition Create(string basisHash, Dictionary<string, string> encodedFields,
        Dictionary<string, JsonElement> resources, Dictionary<string, string> resourceIdsByField, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(basisHash) || basisHash.Length > 128 || encodedFields is null || encodedFields.Count == 0
            || encodedFields.Any(field => string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 100 || string.IsNullOrWhiteSpace(field.Value) || field.Value.Length > 16000)
            || resources is null || resourceIdsByField is null || resourceIdsByField.Values.Any(string.IsNullOrWhiteSpace)
            || resourceIdsByField.Keys.Except(encodedFields.Keys, StringComparer.Ordinal).Any())
            throw new ValidationException("La composición necesita la base y los campos completos del origen seleccionado.");
        var original = new Dictionary<string, string>(encodedFields, StringComparer.Ordinal);
        foreach (var source in original.Values) {
            cancellation.ThrowIfCancellationRequested(); ValidateInputs(source);
            if (source.Contains(WordPrefix, StringComparison.Ordinal)) throw new ValidationException("El origen usa el namespace reservado de marcadores léxicos.");
        }
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        var ordinal = 0;
        foreach (var field in original) {
            cancellation.ThrowIfCancellationRequested(); JsonElement? resource = null; string? resourceId = null;
            if (resourceIdsByField.TryGetValue(field.Key, out resourceId)) {
                if (!resources.TryGetValue(resourceId, out var found)) throw new ValidationException("El recurso léxico del campo no existe.");
                resource = found;
            }
            fields.Add(field.Key, MaskField(field.Key, ordinal++, field.Value, resourceId, resource, slots, cancellation));
        }
        var composition = new AuthoringTranslationComposition(basisHash, original, fields, slots);
        cancellation.ThrowIfCancellationRequested(); return composition;
    }

    /// <summary>
    /// Reads transport message.content, verifies every whole-field rewrite and
    /// correlated binding, then resolves declared markers only. New canonical input
    /// IDs pass to the existing Decode/AuthoringEdits authorization owner.
    /// </summary>
    public Dictionary<string, string> Compile(JsonDocument response, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested(); Wire wire;
        try {
            using var content = JsonDocument.Parse(response.RootElement.GetProperty("message").GetProperty("content").GetString()!,
                new JsonDocumentOptions { AllowDuplicateProperties = false });
            wire = AgentResponseContent.Parse<Wire>(content.RootElement, Schema);
        } catch (Exception failure) when (failure is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentNullException) {
            throw new ValidationException("La traducción contiene JSON inválido o claves repetidas.");
        }
        cancellation.ThrowIfCancellationRequested();
        if (wire.BasisHash != basisHash || wire.Bindings is null || wire.Rewrites is null
            || !wire.Bindings.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(slots.Keys)
            || !wire.Rewrites.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(originalFields.Keys))
            throw new ValidationException("La traducción no corresponde a la base, ocurrencias y campos completos del origen.");
        var bound = Bind(wire.Bindings, cancellation);
        foreach (var field in originalFields) {
            cancellation.ThrowIfCancellationRequested();
            var text = wire.Rewrites[field.Key];
            if (string.IsNullOrWhiteSpace(text) || text.Length > 16000) throw new ValidationException("La traducción contiene un campo vacío o excesivo.");
            ValidateInputs(text); ValidateMarkers(field.Key, text);
            if (slots.Values.Any(slot => slot.FieldId == field.Key)) {
                if (!MarkerSequence(text).SequenceEqual(MarkerSequence(maskedFields[field.Key]), StringComparer.Ordinal))
                    throw new ValidationException("La traducción cambió el orden o la cantidad de predicados y entradas originales.");
            } else {
                var original = InputCounts(field.Value); var proposed = InputCounts(text);
                if (originalInputs.Any(input => proposed.GetValueOrDefault(input) != original.GetValueOrDefault(input)))
                    throw new ValidationException("Las entradas originales no pueden copiarse, perderse o transferirse entre campos.");
            }
        }
        // All fields are validated before resolving any marker; ordinary words
        // are never searched, substituted, corrected or treated as execution.
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in originalFields) {
            cancellation.ThrowIfCancellationRequested();
            var text = WordMarkers.Replace(wire.Rewrites[field.Key], match => bound[match.Value]);
            ValidateInputs(text);
            if (text.Length > 16000) throw new ValidationException("El campo compilado excede su límite.");
            result.Add(field.Key, text);
        }
        cancellation.ThrowIfCancellationRequested(); return result;
    }

    private Dictionary<string, string> Bind(Dictionary<string, Binding> bindings, CancellationToken cancellation)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, slot) in slots) {
            cancellation.ThrowIfCancellationRequested(); var binding = bindings[id];
            var candidate = slot.Candidates.SingleOrDefault(candidate => candidate.CandidateId == binding?.CandidateId)
                ?? throw new ValidationException("La acepción no pertenece a esta ocurrencia.");
            if (binding!.Word != candidate.Headword) throw new ValidationException("El eco léxico no corresponde a la acepción elegida.");
            var text = candidate.CandidateId == "KEEP_SOURCE"
                ? binding.Casing == "AS_SOURCE" ? slot.SourceWord : throw new ValidationException("KEEP_SOURCE conserva el literal exacto.")
                : binding.Casing switch {
                    "AS_ENTRY" => candidate.Headword, "CAPITALIZED" => Capitalized(candidate.Headword),
                    "UPPERCASE" => candidate.Headword.ToUpperInvariant(), _ => throw new ValidationException("Mayúsculas no admitidas.")
                };
            ValidateInputs(text);
            if (InputCounts(text).Count > 0 || text.Contains(WordPrefix, StringComparison.Ordinal))
                throw new ValidationException("Una acepción léxica no puede crear entradas ni marcadores.");
            result.Add(slot.Token, text);
        }
        return result;
    }

    private void ValidateMarkers(string fieldId, string text)
    {
        var matches = WordMarkers.Matches(text).ToArray();
        if (WordMarkers.Replace(text, "").Contains(WordPrefix, StringComparison.Ordinal))
            throw new ValidationException("La traducción contiene un marcador léxico malformado.");
        if (matches.Any(match => !slotsByToken.TryGetValue(match.Value, out var slot) || slot.FieldId != fieldId))
            throw new ValidationException("La traducción contiene un marcador desconocido o de otro campo.");
        var expected = slots.Values.Where(slot => slot.FieldId == fieldId).Select(slot => slot.Token).ToArray();
        if (matches.Length != expected.Length || expected.Any(token => matches.Count(match => match.Value == token) != 1))
            throw new ValidationException("Cada marcador léxico debe aparecer exactamente una vez en su campo.");
    }
    private string[] MarkerSequence(string text) => SequenceMarkers.Matches(text).Select(match => match.Value)
        .Where(token => token.StartsWith(WordPrefix, StringComparison.Ordinal) || originalInputs.Contains(token)).ToArray();

    private JsonElement BuildSchema() => ContractSchema.Object(new() {
        ["basisHash"] = new { type = "string", @const = basisHash },
        ["bindings"] = ContractSchema.Object(slots.ToDictionary(item => item.Key,
            item => (object)new { anyOf = item.Value.Candidates.Select(candidate => ContractSchema.Object(new() {
                ["candidateId"] = new { type = "string", @const = candidate.CandidateId },
                ["word"] = new { type = "string", @const = candidate.Headword },
                ["casing"] = ContractSchema.Text(20, candidate.CandidateId == "KEEP_SOURCE" ? ["AS_SOURCE"] : ["AS_ENTRY", "CAPITALIZED", "UPPERCASE"])
            }, "candidateId", "word", "casing")).ToArray() }, StringComparer.Ordinal), slots.Keys.ToArray()),
        ["rewrites"] = ContractSchema.Object(originalFields.ToDictionary(field => field.Key,
            _ => (object)new { type = "string", minLength = 1, maxLength = 16000 }, StringComparer.Ordinal), originalFields.Keys.ToArray())
    }, "basisHash", "bindings", "rewrites");

    private static void ValidateInputs(string text)
    {
        var literal = Inputs.Replace(text, "");
        if (literal.IndexOfAny(['⟦', '⟧']) >= 0 || TemplateCompiler.Names(literal).Length > 0)
            throw new ValidationException("La traducción necesita entradas canónicas, sin variables nuevas redactadas literalmente.");
        foreach (Match token in Inputs.Matches(text))
            if (!int.TryParse(token.Value.AsSpan(1, token.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                throw new ValidationException("El índice de entrada canónica no está admitido.");
    }
    private static Dictionary<string, int> InputCounts(string text) => Inputs.Matches(text).Select(match => match.Value)
        .GroupBy(value => value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    private static string Capitalized(string text) {
        var first = StringInfo.GetNextTextElement(text); return first.ToUpperInvariant() + text[first.Length..];
    }
    private sealed record Wire(string BasisHash, Dictionary<string, Binding>? Bindings, Dictionary<string, string>? Rewrites);
    private sealed record Binding(string CandidateId, string Word, string Casing);
    private sealed record SourceRange(int Start, int Length);
    private sealed record Slot(string FieldId, string Token, SourceRange SourceRange, string SourceWord, string ResourceId, Candidate[] Candidates);
    private sealed record Candidate(string CandidateId, string Headword, string Kind, object[][] SourceEdges);
}
