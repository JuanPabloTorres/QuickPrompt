using System.Text;
using System.Text.Json;
using System.Numerics;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class AuthoringRevisionEditor
{
    private sealed record SourcePreview(string Id, string Text, string[] RemovedTexts, AuthoringUnitRemoval[] Removals);
    private sealed record PreviewCatalog(string BasisHash, bool Available, Dictionary<string, SourcePreview[]> Fields)
    {
        internal static PreviewCatalog Unavailable(string hash) => new(hash, false, new(StringComparer.Ordinal));
    }

    private static PreviewCatalog SourcePreviews(AuthoringRevisionBasis basis, CancellationToken cancellation)
    {
        var fields = new Dictionary<string, SourcePreview[]>(StringComparer.Ordinal);
        var bytes = 0;
        foreach (var field in basis.Fields.Where(field => field.Units.Length > 1)) {
            var options = new List<SourcePreview>();
            var fieldBytes = 0;
            var unitNames = field.Units.ToDictionary(unit => unit.Id, unit => TemplateCompiler.Names(unit.Text));
            var owners = unitNames.Values.SelectMany(names => names).GroupBy(name => name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var unitNumbers = field.Units.ToDictionary(unit => unit.Id, unit => AuthoringNumberLiterals.Read(unit.Text));
            var numberOwners = unitNumbers.Values.SelectMany(numbers => numbers).GroupBy(number => number, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var sourceNames = owners.Keys.ToArray();
            var sourceText = string.Concat(field.Units.Select(unit => unit.Text));
            // A sole owner cannot be omitted in any admissible combination.
            // Prune these units before enumeration, including numeric literals:
            // otherwise an entirely rejected power set could consume the deadline.
            var optional = field.Units.Skip(1).Where(unit => unitNames[unit.Id].All(name => owners[name] > 1)
                && unitNumbers[unit.Id].All(number => numberOwners[number] > 1)).ToArray();
            // BigInteger avoids a unit-count cutoff or shift overflow. The
            // actual serialized menu and the caller's cancellation bound work.
            for (var mask = BigInteger.Zero; mask < BigInteger.One << optional.Length; mask++) {
                cancellation.ThrowIfCancellationRequested();
                var omitted = optional.Where((_, index) => (mask & (BigInteger.One << index)) != 0).ToHashSet();
                var kept = field.Units.Where(unit => !omitted.Contains(unit)).ToArray();
                var text = string.Concat(kept.Select(unit => unit.Text));
                // Preserve markers per field, so separately selected variants
                // cannot jointly remove an input that spans multiple fields.
                if (!TemplateCompiler.Names(text).ToHashSet(StringComparer.Ordinal).SetEquals(sourceNames)) continue;
                if (!AuthoringNumberLiterals.Conserved(sourceText, text)) continue;
                var removals = field.Units.Where(unit => !kept.Contains(unit)).Select(unit =>
                    new AuthoringUnitRemoval(unit.Id, kept.TakeWhile(prior => prior.Start < unit.Start).Select(prior => prior.Id).ToArray())).ToArray();
                var preview = new SourcePreview(field.Id + "/" + options.Count, EncodeText(text, basis.InputNames),
                    field.Units.Where(unit => !kept.Contains(unit)).Select(unit => EncodeText(unit.Text, basis.InputNames)).ToArray(), removals);
                fieldBytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new { preview.Id, preview.Text, preview.RemovedTexts }, Json));
                options.Add(preview);
                if (fieldBytes > PacketBytes || options.Count > 1 && bytes + fieldBytes > PacketBytes)
                    return PreviewCatalog.Unavailable(basis.BasisHash);
            }
            if (options.Count > 1) { fields.Add(field.Id, options.ToArray()); bytes += fieldBytes; }
        }
        return new(basis.BasisHash, true, fields);
    }

    private static object PreviewMaterial(PreviewCatalog catalog) => new {
        catalog.BasisHash, status = catalog.Available ? "AVAILABLE" : "UNAVAILABLE_COMPLETE_CATALOG_EXCEEDS_BUDGET",
        semanticStatus = "UNREVIEWED_PROPOSALS_NOT_APPROVALS",
        fields = catalog.Fields.ToDictionary(item => item.Key, item => item.Value.Select(preview => new { preview.Id, preview.Text, preview.RemovedTexts }).ToArray())
    };

    private static JsonElement PlanSchema(PreviewCatalog catalog, AuthoringRevisionField[] fields, bool requiresPresentationEdit, string? requiredLanguage, bool compressionAvailable)
    {
        object Choices(bool originalOnly) => ContractSchema.Object(catalog.Fields.ToDictionary(item => item.Key,
            item => (object)new { type = "string", @enum = originalOnly ? new[] { item.Value[0].Id } : item.Value.Select(preview => preview.Id).ToArray() }), catalog.Fields.Keys.ToArray());
        var translation = ContractSchema.Object(new() {
            ["targetLanguage"] = requiredLanguage is null ? new { type = "string", minLength = 2, maxLength = 80 }
                : (object)new { type = "string", @const = requiredLanguage }
        }, "targetLanguage");
        JsonElement Ready(string[] kinds, bool originalOnly = false, bool translating = false, bool planningCompression = false) => ContractSchema.Object(new() {
            ["outcome"] = ContractSchema.Text(5, ["READY"]), ["decisionText"] = new { type = "null" },
            ["revisionKind"] = ContractSchema.Text(9, kinds), ["previewChoices"] = planningCompression ? new { type = "null" } : Choices(originalOnly),
            ["translationPreparation"] = translating ? translation : (object)new { type = "null" }
        }, "outcome", "decisionText", "revisionKind", "previewChoices", "translationPreparation");
        var branches = new List<JsonElement>();
        if (requiredLanguage is null) {
            if (!requiresPresentationEdit && compressionAvailable) branches.Add(Ready([AuthoringEdits.Compress], planningCompression: true));
            branches.Add(Ready([AuthoringEdits.Rewrite]));
        }
        branches.Add(Ready([Translation], translating: true));
        if (requiredLanguage is null && !requiresPresentationEdit) branches.Add(Ready([AuthoringEdits.Keep], originalOnly: true));
        branches.Add(ContractSchema.Object(new() { ["outcome"] = ContractSchema.Text(5, ["ASK"]),
            ["decisionText"] = new { type = "string", minLength = 1, maxLength = 340 },
            ["revisionKind"] = new { type = "null" }, ["previewChoices"] = new { type = "null" },
            ["translationPreparation"] = new { type = "null" }
        }, "outcome", "decisionText", "revisionKind", "previewChoices", "translationPreparation"));
        return JsonSerializer.SerializeToElement(new { type = "object", anyOf = branches });
    }

    private static AuthoringBriefContent SelectPreviews(AuthoringRevisionBasis basis, PreviewCatalog catalog, Dictionary<string, string> choices)
    {
        if (catalog.BasisHash != basis.BasisHash) throw new ConflictException("Las alternativas pertenecen a otra base de contenido.");
        if (choices.Count != catalog.Fields.Count || !choices.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(catalog.Fields.Keys))
            throw new ValidationException("Selecciona todos y sólo los campos del catálogo de alternativas.");
        var removals = catalog.Fields.SelectMany(item => item.Value.SingleOrDefault(preview => preview.Id == choices[item.Key])?.Removals
            ?? throw new ValidationException("La alternativa no pertenece al campo y origen actuales.")).ToArray();
        return AuthoringEdits.Apply(basis, removals.Length == 0
            ? new(basis.BasisHash, AuthoringEdits.Keep, null, null)
            : new(basis.BasisHash, AuthoringEdits.Compress, removals, null));
    }
}
