using System.Text;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>A host-owned snapshot and its exact editing addresses. The hash is a binding, not effect approval.</summary>
public sealed record AuthoringRevisionBasis(string BasisHash, AuthoringBriefContent Content,
    AuthoringRevisionField[] Fields, string[] InputNames);
public sealed record AuthoringRevisionField(string Id, AuthoringRevisionUnit[] Units, AuthoringTextSegment[] Segments);
public sealed record AuthoringRevisionUnit(string Id, string Text, int Start, int Length);

/// <summary>One whole transaction: KEEP preserves the snapshot, COMPRESS removes units, REWRITE replaces complete fields.</summary>
public sealed record AuthoringRevisionProposal(string BasisHash, string Operation,
    AuthoringUnitRemoval[]? Removals, AuthoringFieldRewrite[]? Changes);
public sealed record AuthoringUnitRemoval(string UnitId, string[] RetainedWitnessIds);
public sealed record AuthoringFieldRewrite(string FieldId, AuthoringTextSegment[] Segments);

/// <summary>Exactly one member is present. InputId is the zero-based index in the host's InputNames.</summary>
public sealed record AuthoringTextSegment(string? Text, int? InputId);

/// <summary>
/// Applies a bounded proposal to a copied source. It proves source addresses, composition, input conservation
/// and reference-role identity, never semantic fidelity, human confirmation or permission to save.
/// Inline URI edits remain subject to the existing literal-change review before effects.
/// </summary>
public static class AuthoringEdits
{
    public const string Keep = "KEEP";
    public const string Compress = "COMPRESS";
    public const string Rewrite = "REWRITE";

    public static AuthoringRevisionBasis Create(AuthoringBriefContent content, string[]? allowedInputs = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Steps is null || content.Constraints is null || content.OutputRequirements is null || content.References is null)
            throw new ValidationException("La fuente necesita todas sus listas de contenido y referencias.");
        var copied = AuthoringBriefs.Copy(content);
        var existingInputs = TemplateCompiler.Names(Body(copied));
        var inputs = allowedInputs?.ToArray() ?? existingInputs;
        PromptInputContract.ValidateSelection(inputs);
        if (existingInputs.Except(inputs, StringComparer.Ordinal).Any())
            throw new ValidationException("Los campos presentes deben pertenecer a la selección humana de variables.");
        ValidateContent(copied, inputs);
        var fields = FieldValues(copied).Select(field => new AuthoringRevisionField(field.Key,
            Units(field.Key, field.Value), Segments(field.Value, inputs))).ToArray();
        return new(Hash(copied, fields, inputs), copied, fields, inputs);
    }

    public static AuthoringBriefContent Apply(AuthoringRevisionBasis basis, AuthoringRevisionProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(proposal);
        var canonical = Create(basis.Content, basis.InputNames);
        if (proposal.BasisHash != basis.BasisHash || canonical.BasisHash != basis.BasisHash
            || Hash(basis.Content, basis.Fields, basis.InputNames) != basis.BasisHash)
            throw new ConflictException("La base o sus direcciones de edición cambiaron; revisa otra propuesta.");
        var values = FieldValues(canonical.Content);
        if (proposal.Operation == Keep && proposal.Removals is null && proposal.Changes is null)
            return AuthoringBriefs.Copy(canonical.Content);
        if (proposal.Operation == Compress && proposal.Removals is { Length: > 0 } && proposal.Changes is null)
            ApplyRemovals(canonical, proposal.Removals, values);
        else if (proposal.Operation == Rewrite && proposal.Changes is { Length: > 0 } && proposal.Removals is null)
            ApplyRewrites(canonical, proposal.Changes, values);
        else throw new ValidationException("Elige una sola operación de edición y su tipo de cambios.");

        // Source order, metadata and reference roles are not writable by this protocol.
        var original = canonical.Content;
        var revised = original with { Task = values["task"], Steps = Read("step", original.Steps.Length),
            Constraints = Read("constraint", original.Constraints.Length), OutputRequirements = Read("output", original.OutputRequirements.Length) };
        ValidateContent(revised, canonical.InputNames);
        if (TemplateCompiler.Names(Body(original)).Except(TemplateCompiler.Names(Body(revised)), StringComparer.Ordinal).Any())
            throw new ValidationException("La edición no puede perder variables presentes del contenido original.");
        return revised;

        string[] Read(string kind, int count) => Enumerable.Range(1, count).Select(index => values[$"{kind}_{index}"]).ToArray();
    }

    private static void ApplyRemovals(AuthoringRevisionBasis basis, AuthoringUnitRemoval[] removals, Dictionary<string, string> values)
    {
        var units = basis.Fields.SelectMany(field => field.Units.Select(unit => (field.Id, Unit: unit)))
            .ToDictionary(item => item.Unit.Id, StringComparer.Ordinal);
        if (removals.Any(item => item is null || item.UnitId is null || !units.ContainsKey(item.UnitId))
            || removals.Select(item => item.UnitId).Distinct(StringComparer.Ordinal).Count() != removals.Length)
            throw new ValidationException("Cada eliminación debe identificar una unidad original distinta.");
        var removed = removals.Select(item => item.UnitId).ToHashSet(StringComparer.Ordinal);
        foreach (var removal in removals)
        {
            var witnesses = removal.RetainedWitnessIds;
            if (witnesses is null || witnesses.Length == 0 || witnesses.Distinct(StringComparer.Ordinal).Count() != witnesses.Length
                || witnesses.Any(id => id is null || !units.TryGetValue(id, out var witness)
                    || witness.Id != units[removal.UnitId].Id || removed.Contains(id)))
                throw new ValidationException("La redundancia necesita unidades testigo distintas y conservadas del mismo campo.");
        }
        foreach (var field in basis.Fields)
        {
            values[field.Id] = string.Concat(field.Units.Where(unit => !removed.Contains(unit.Id)).Select(unit => unit.Text));
            if (string.IsNullOrWhiteSpace(values[field.Id])) throw new ValidationException("No se puede vaciar un campo establecido.");
            if (!AuthoringNumberLiterals.Conserved(string.Concat(field.Units.Select(unit => unit.Text)), values[field.Id]))
                throw new ValidationException("La compresión conserva los literales numéricos reconocidos de cada campo. Un cambio de cantidades requiere reformulación y revisión.");
        }
    }

    private static void ApplyRewrites(AuthoringRevisionBasis basis, AuthoringFieldRewrite[] changes, Dictionary<string, string> values)
    {
        if (changes.Any(item => item is null || item.FieldId is null || !values.ContainsKey(item.FieldId))
            || changes.Select(item => item.FieldId).Distinct(StringComparer.Ordinal).Count() != changes.Length)
            throw new ValidationException("Cada reemplazo debe identificar un campo original distinto.");
        foreach (var change in changes)
        {
            if (change.Segments is null || change.Segments.Length > 16000)
                throw new ValidationException("El reemplazo necesita segmentos acotados.");
            var text = new StringBuilder();
            foreach (var segment in change.Segments)
            {
                if (segment is null || (segment.Text is null) == (segment.InputId is null))
                    throw new ValidationException("Un segmento contiene texto o una variable, nunca ambos.");
                if (segment.InputId is { } id)
                {
                    if (id < 0 || id >= basis.InputNames.Length) throw new ValidationException("La variable no pertenece al contrato original.");
                    text.Append('<').Append(basis.InputNames[id]).Append('>');
                }
                else
                {
                    if (segment.Text!.Length == 0 || TemplateCompiler.Names(segment.Text).Length != 0)
                        throw new ValidationException("Usa los IDs del anfitrión para variables; no escribas marcadores dentro del texto.");
                    text.Append(segment.Text);
                }
                if (text.Length > 8000) throw new ValidationException("El campo editado excede su límite; no se truncará.");
            }
            values[change.FieldId] = text.ToString();
        }
    }

    private static AuthoringRevisionUnit[] Units(string fieldId, string text)
    {
        // Conservative lexical boundaries only. Ambiguous punctuation locks the field
        // into one unit; it never becomes a semantic decision or an intent router.
        if (text.Contains('`') || text.Contains("://", StringComparison.Ordinal)
            || text.IndexOfAny(['"', '\'', '“', '”', '«', '»']) >= 0
            || Enumerable.Range(1, Math.Max(0, text.Length - 2)).Any(index => text[index] == '.'
                && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1])))
            return [new(fieldId + ":1", text, 0, text.Length)];
        var cuts = new List<int>();
        var placeholders = TemplateEditor.Spans(text);
        for (var index = 0; index < text.Length - 1; index++)
        {
            if (text[index] is not ('.' or '?' or '!') || !char.IsWhiteSpace(text[index + 1])) continue;
            if (placeholders.Any(span => index >= span.Start && index < span.Start + span.Length)) continue;
            if (text[index] == '.' && AmbiguousPeriod(text, index)) return [new(fieldId + ":1", text, 0, text.Length)];
            var next = index + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
            if (next < text.Length && char.IsUpper(text, next)) cuts.Add(next);
        }
        cuts.Add(text.Length);
        var result = new List<AuthoringRevisionUnit>();
        var start = 0;
        foreach (var end in cuts)
        {
            result.Add(new(fieldId + ":" + (result.Count + 1), text[start..end], start, end - start));
            start = end;
        }
        return result.ToArray();
    }

    private static bool AmbiguousPeriod(string text, int index)
    {
        if (index == 0 || text[index - 1] == '.') return true;
        if (text[index - 1] == '>') return false;
        var start = index;
        while (start > 0 && char.IsLetter(text, start - 1)) start--;
        var word = text[start..index];
        // Short tokens and capitalized endings may be abbreviations or initials.
        return word.Length < 4 || word.Any(char.IsUpper);
    }

    private static AuthoringTextSegment[] Segments(string text, string[] inputs)
    {
        var result = new List<AuthoringTextSegment>();
        var start = 0;
        foreach (var span in TemplateEditor.Spans(text))
        {
            if (span.Start > start) result.Add(new(text[start..span.Start], null));
            result.Add(new(null, Array.IndexOf(inputs, span.Name)));
            start = span.Start + span.Length;
        }
        if (start < text.Length) result.Add(new(text[start..], null));
        return result.ToArray();
    }

    private static Dictionary<string, string> FieldValues(AuthoringBriefContent content)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["task"] = content.Task };
        Add("step", content.Steps); Add("constraint", content.Constraints); Add("output", content.OutputRequirements);
        return fields;

        void Add(string kind, string[] items)
        {
            for (var index = 0; index < items.Length; index++) fields.Add($"{kind}_{index + 1}", items[index]);
        }
    }

    private static string Body(AuthoringBriefContent content) =>
        string.Join("\n", new[] { content.Task }.Concat(content.Steps).Concat(content.Constraints).Concat(content.OutputRequirements));

    private static void ValidateContent(AuthoringBriefContent content, string[] inputs)
    {
        // Reuse the existing composition limits and reference-role validation only.
        // This temporary value is never returned as human confirmation or effect authority.
        var review = new AuthoringBriefReview("", "", null, inputs, content);
        AuthoringBriefs.Validate(content, review);
        new ConfirmedAuthoringBrief(review, content).ValidateComposition();
    }

    private static string Hash(AuthoringBriefContent content, AuthoringRevisionField[] fields, string[] inputs) =>
        LiveRuns.Hash(new { Content = content, Fields = fields, InputNames = inputs });
}
