using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>A logical qualifier, never source-unit membership. Arguments are ordered, literal values.</summary>
public sealed record CompressionFactScope(string Kind, string[] Arguments);
/// <summary>Proposed meaning. Dependencies refer only to logical prerequisites, not repetition or coreference.</summary>
public sealed record CompressionFact(string Id, string Predicate, string[] Arguments, string Polarity,
    string Modality, CompressionFactScope Scope, string[] Dependencies);
public sealed record CompressionLedger(string BasisHash, CompressionFact[] Facts, Dictionary<string, string[]> Units);

/// <summary>
/// Compiles a proposed obligation ledger against host-owned source units. Structural equivalence
/// is independent of fact IDs, but does not certify the model's semantic extraction or authorize effects.
/// The caller retains the original request, inference budget, human review and persistence authority.
/// </summary>
public static class AuthoringCompressionLedger
{
    private const int MaximumBytes = 26000;
    public const int MaximumFactCapacity = 128;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static object Material(AuthoringRevisionBasis basis)
    {
        VerifyBasis(basis);
        var material = new { basis.BasisHash, inputs = basis.InputNames.ToArray(),
            fields = basis.Fields.Select(field => new { field.Id,
                units = field.Units.Select(unit => new { unit.Id, unit.Text }).ToArray() }).ToArray(),
            factContract = new {
                predicate = "Canonical English predicate lemma. Reuse its symbol only for the same action or relation; the compiler performs no stemming or synonym inference.",
                arguments = "Ordered semantic roles. Preserve names, quantities and input labels exactly; resolve a repeated reference consistently only when its source meaning is the same.",
                polarityAndModality = "Keep assertion or negation separate from required, permitted or prohibited force.",
                scope = "Logical qualifier kind and ordered values: preserve conditions, exclusivity, objects and temporal extent. Not a list of source unit IDs.",
                dependencies = "Fact IDs of actual logical prerequisites only. Not witnesses, repeated mentions, coreferences or unit membership. No self-reference or cycles.",
                coverage = "Map every original unit to all its obligations, including unique continuations. Units and their text belong to the host; do not emit offsets or witness selections."
            },
            scope = "SOURCE_BOUND_OBLIGATION_PROPOSAL_NOT_SEMANTIC_APPROVAL" };
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(material, Json)) > MaximumBytes)
            throw Invalid("El origen completo no cabe en el presupuesto del registro; no se truncará.");
        return material;
    }

    /// <summary>Closed wire shape; IDs and dependency/coverage validity are rechecked by Compile.</summary>
    public static JsonElement Schema(AuthoringRevisionBasis basis, int factCapacity = MaximumFactCapacity)
    {
        VerifyBasis(basis);
        ValidateCapacity(factCapacity);
        var factIds = Enumerable.Range(0, factCapacity).Select(index => "f" + index).ToArray();
        object Text() => new { type = "string", minLength = 1, maxLength = 16000 };
        object Reference() => new { type = "string", @enum = factIds };
        object List(object item, int minimum = 0) => new { type = "array", items = item, minItems = minimum };
        object Object(Dictionary<string, object> properties) => new { type = "object", additionalProperties = false,
            properties, required = properties.Keys.ToArray() };
        var fact = Object(new() {
            ["id"] = Reference(), ["predicate"] = Text(), ["arguments"] = List(Text()),
            ["polarity"] = new { type = "string", @enum = new[] { "AFFIRMATIVE", "NEGATIVE" } },
            ["modality"] = new { type = "string", @enum = new[] { "REQUIRED", "PERMITTED", "PROHIBITED" } },
            ["scope"] = Object(new() { ["kind"] = Text(), ["arguments"] = List(Text()) }),
            ["dependencies"] = List(Reference())
        });
        return JsonSerializer.SerializeToElement(Object(new() {
            ["basisHash"] = new { type = "string", @const = basis.BasisHash },
            ["facts"] = new { type = "array", items = fact, minItems = 1, maxItems = factCapacity },
            ["units"] = Object(basis.Fields.SelectMany(field => field.Units)
                .ToDictionary(unit => unit.Id, _ => List(Reference(), 1), StringComparer.Ordinal))
        }), Json);
    }

    /// <summary>
    /// Retains the first unit and every later unit introducing a new obligation in its own field.
    /// Removes all fully covered later units using only witnesses that remain in that field.
    /// Invalid ledgers fail atomically; this method performs no inference, storage or approval.
    /// </summary>
    public static AuthoringBriefContent Compile(AuthoringRevisionBasis basis, CompressionLedger ledger,
        CancellationToken cancellation = default, int factCapacity = MaximumFactCapacity)
    {
        cancellation.ThrowIfCancellationRequested();
        VerifyBasis(basis);
        ValidateCapacity(factCapacity);
        var facts = Validate(basis, ledger, factCapacity);
        var keys = FactKeys(facts, cancellation);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ledger.Units.Values.SelectMany(ids => ids)) Visit(id);
        if (used.Count != facts.Count) throw Invalid("El registro contiene hechos sin vínculo con el origen.");

        var removals = new List<AuthoringUnitRemoval>();
        foreach (var field in basis.Fields) {
            cancellation.ThrowIfCancellationRequested();
            var covered = new HashSet<string>(StringComparer.Ordinal);
            var retained = new List<(AuthoringRevisionUnit Unit, HashSet<string> Facts)>();
            foreach (var unit in field.Units) {
                cancellation.ThrowIfCancellationRequested();
                var obligations = ledger.Units[unit.Id].Select(id => keys[id]).ToHashSet(StringComparer.Ordinal);
                if (retained.Count > 0 && obligations.IsSubsetOf(covered)) {
                    var witnesses = retained.Where(prior => prior.Facts.Overlaps(obligations)).Select(prior => prior.Unit.Id).ToArray();
                    removals.Add(new(unit.Id, witnesses));
                } else {
                    retained.Add((unit, obligations));
                    covered.UnionWith(obligations);
                }
            }
            // A shared input in another field cannot justify deleting its only occurrence here.
            var before = TemplateCompiler.Names(string.Concat(field.Units.Select(unit => unit.Text))).ToHashSet(StringComparer.Ordinal);
            var after = TemplateCompiler.Names(string.Concat(retained.Select(item => item.Unit.Text))).ToHashSet(StringComparer.Ordinal);
            if (!before.SetEquals(after)) throw Invalid("La compresión no puede perder variables presentes en un campo.");
        }
        cancellation.ThrowIfCancellationRequested();
        return AuthoringEdits.Apply(basis, removals.Count == 0
            ? new(basis.BasisHash, AuthoringEdits.Keep, null, null)
            : new(basis.BasisHash, AuthoringEdits.Compress, removals.ToArray(), null));

        void Visit(string id)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!used.Add(id)) return;
            foreach (var dependency in facts[id].Dependencies) Visit(dependency);
        }
    }

    private static Dictionary<string, CompressionFact> Validate(AuthoringRevisionBasis basis, CompressionLedger ledger, int factCapacity)
    {
        if (ledger is null || ledger.BasisHash != basis.BasisHash) throw new ConflictException("El registro no pertenece a la base actual.");
        if (ledger.Facts is null || ledger.Facts.Length == 0 || ledger.Facts.Length > factCapacity || ledger.Units is null
            || Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(ledger, Json)) > MaximumBytes)
            throw Invalid("El registro necesita hechos, unidades y un tamaño completo acotado.");
        var unitIds = basis.Fields.SelectMany(field => field.Units).Select(unit => unit.Id).ToHashSet(StringComparer.Ordinal);
        if (!unitIds.SetEquals(ledger.Units.Keys)) throw Invalid("El registro debe cubrir todas y sólo las unidades originales.");
        var facts = new Dictionary<string, CompressionFact>(StringComparer.Ordinal);
        foreach (var fact in ledger.Facts) {
            if (fact is null || !FactId(fact.Id, factCapacity) || !facts.TryAdd(fact.Id, fact)
                || !Text(fact.Predicate) || !Texts(fact.Arguments) || fact.Scope is null
                || !Text(fact.Scope.Kind) || !Texts(fact.Scope.Arguments)
                || fact.Polarity is not ("AFFIRMATIVE" or "NEGATIVE")
                || fact.Modality is not ("REQUIRED" or "PERMITTED" or "PROHIBITED")
                || fact.Dependencies is null || fact.Dependencies.Any(id => !FactId(id, factCapacity))
                || fact.Dependencies.Distinct(StringComparer.Ordinal).Count() != fact.Dependencies.Length)
                throw Invalid("El registro contiene un hecho, alcance o dependencia inválidos.");
        }
        if (facts.Values.Any(fact => fact.Dependencies.Any(id => !facts.ContainsKey(id))))
            throw Invalid("Las dependencias deben pertenecer al registro actual.");
        if (ledger.Units.Values.Any(ids => ids is null || ids.Length == 0 || ids.Any(id => id is null || !facts.ContainsKey(id))
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length))
            throw Invalid("Cada unidad necesita hechos distintos y presentes en el registro.");
        return facts;
    }

    private static Dictionary<string, string> FactKeys(Dictionary<string, CompressionFact> facts, CancellationToken cancellation)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in facts.Keys) Key(id);
        return keys;

        string Key(string id)
        {
            cancellation.ThrowIfCancellationRequested();
            if (keys.TryGetValue(id, out var key)) return key;
            if (!visiting.Add(id)) throw Invalid("Las dependencias de obligaciones no pueden formar ciclos.");
            var fact = facts[id];
            // Only protocol symbols are case-normalized. Arguments (names, quantities,
            // inputs and scope values) stay ordinal and are never interpreted as rules.
            var canonical = JsonSerializer.Serialize(new { predicate = Symbol(fact.Predicate), fact.Arguments,
                fact.Polarity, fact.Modality, scope = new { kind = Symbol(fact.Scope.Kind), fact.Scope.Arguments },
                dependencies = fact.Dependencies.Select(Key).Order(StringComparer.Ordinal).ToArray() }, Json);
            key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            keys.Add(id, key); visiting.Remove(id); return key;
        }
    }

    private static void VerifyBasis(AuthoringRevisionBasis basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        _ = AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Keep, null, null));
    }
    private static void ValidateCapacity(int capacity)
    {
        if (capacity is < 1 or > MaximumFactCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
    }
    private static bool FactId(string? id, int capacity) => id is { Length: > 1 and <= 4 } && id[0] == 'f'
        && id.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0
        && int.TryParse(id.AsSpan(1), out var index) && index < capacity && id == "f" + index;
    private static bool Text(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 16000;
    private static bool Texts(string[]? texts) => texts is not null && texts.All(Text);
    private static string Symbol(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    private static ValidationException Invalid(string message) => new(message);
}
