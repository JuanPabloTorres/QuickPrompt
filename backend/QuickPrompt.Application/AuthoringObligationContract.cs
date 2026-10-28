using System.Text.RegularExpressions;
using System.Globalization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record AuthoringLexicalUnit(int Id, int Start, int Length, string Text);
public record AuthoringDisposition(int First, int Last, string Role);
public record AuthoringReplacement(int SourceFirst, int SourceLast, int? RequestFirst, int? RequestLast, int AuthorityFirst, int AuthorityLast);
public record AuthoringReduction(int First, int Last);
public record AuthoringCaseRange(int Start, int Length, string Name);
public record AuthoringInventory(AuthoringDisposition[] Request, AuthoringDisposition[] Source, AuthoringReplacement[] Replacements,
    int? VerbatimBodyReference, string Question)
{
    public AuthoringReduction? Reduction { get; init; }
}
public record AuthoringLiteralCandidate(int Id, int Start, int Length, string Text);
public record AuthoringClause(int Id, string Text);
public record AuthoringAuthoritySpan(int Start, int Length, string Text);
public record AuthoringLedgerEntry(int Id, string Source, int Start, int Length, string Text, string Role,
    string? ReplacementText, AuthoringAuthoritySpan[] ChangeAuthority);
public record AuthoringReviewSource(Guid? PromptId, int? Revision, string? BodyHash, string? OriginalText)
{
    public string? SourceTitle { get; init; }
    public string? SourceDescription { get; init; }
    public int? SourceCategoryId { get; init; }
    public string? SavedBodyHash { get; init; }
    public string? SavedOriginalText { get; init; }
    public Guid? WorkingDraftId { get; init; }
    public string? WorkingBodyHash { get; init; }
}
public record AuthoringContentReview(string Status, string ContractHash, string BodyHash, string OriginalRequest,
    AuthoringReviewSource Source, AuthoringLedgerEntry[] Ledger)
{
    public LiteralChanges LiteralChanges { get; init; } = new([]);
    public AuthoringClarification[] UserClarifications { get; init; } = [];
}

/// <summary>
/// Source-owned obligations, fixed before generation. Lexical coverage proves
/// provenance and disposition, not that the model's semantic roles are correct.
/// Only source spans can become mandatory clauses; model prose cannot replace them.
/// </summary>
public sealed class AuthoringObligationContract
{
    public string OriginalRequest { get; }
    public string? SourceText { get; }
    public Guid? SourceId { get; }
    public int? SourceRevision { get; }
    public string? SourceTitle { get; }
    public string? SourceDescription { get; }
    private readonly AuthoringCaseRange[] caseRanges;
    public string AuthoringRequest
    {
        get {
            var text = OriginalRequest.ToCharArray();
            foreach (var range in caseRanges) Array.Fill(text, '*', range.Start, range.Length);
            return new(text);
        }
    }
    public int[] InputDataUnitIds => requestUnits.Where(unit => caseRanges.Any(range => unit.Start >= range.Start
        && unit.Start + unit.Length <= range.Start + range.Length)).Select(unit => unit.Id).ToArray();
    private readonly AuthoringLexicalUnit[] requestUnits;
    public AuthoringLexicalUnit[] RequestUnits => [.. requestUnits];
    private readonly AuthoringLexicalUnit[] sourceUnits;
    public AuthoringLexicalUnit[] SourceUnits => [.. sourceUnits];
    private readonly AuthoringLiteralCandidate[] literals;
    public AuthoringLiteralCandidate[] Literals => [.. literals];
    private AuthoringLedgerEntry[] ledger = [];
    public AuthoringLedgerEntry[] Ledger => ledger.Select(row => row with { ChangeAuthority = [.. row.ChangeAuthority] }).ToArray();
    public string Question { get; private set; } = "";
    public bool Resolved { get; private set; }
    public bool HasLiteralBody => literalBody is not null;
    public bool ShorteningRequested => reductionAuthority is not null;
    public string Fingerprint => LiveRuns.Hash(new { OriginalRequest, SourceText, SourceId, SourceRevision, Ledger, literalBody, literalStart, reductionAuthority, caseRanges });
    public int[] ObligationIds => Ledger.Where(row => row.Role is "FUTURE_TASK" or "REPLACED" or "CONTEXT").Select(row => row.Id).ToArray();
    /// <summary>Constraint basis: only bound deltas change the original; CONTEXT never removes deterministic protections.</summary>
    public string EffectiveSource => string.Concat(Ledger.Where(row => row.Source == "SOURCE")
        .Select(row => row.Role == "REPLACED" ? row.ReplacementText : row.Text));
    private string? literalBody;
    private int literalStart;
    private AuthoringAuthoritySpan? reductionAuthority;
    private string? composedBody;
    public string MetadataAuthorityRequest => literalBody is null ? AuthoringRequest
        : new VerbatimBodyBinding(LiveRuns.Hash(AuthoringRequest), literalStart, literalBody.Length).MetadataAuthority(AuthoringRequest, literalBody);

    public AuthoringObligationContract(string request, string? sourceText = null, Guid? sourceId = null, int? sourceRevision = null,
        AuthoringLiteralCandidate[]? literals = null, string? sourceTitle = null, string? sourceDescription = null,
        AuthoringCaseRange[]? inputData = null)
    {
        if (string.IsNullOrWhiteSpace(request) || request.Length > 32000 || sourceText?.Length > 32000)
            throw Invalid("El encargo y la fuente deben caber completos en el contrato de autoría.");
        OriginalRequest = request; SourceText = sourceText; SourceId = sourceId; SourceRevision = sourceRevision;
        SourceTitle = sourceTitle; SourceDescription = sourceDescription;
        caseRanges = (inputData ?? []).OrderBy(range => range.Start).ToArray();
        var characterBoundaries = StringInfo.ParseCombiningCharacters(request).Append(request.Length).ToHashSet();
        var cursor = 0;
        foreach (var range in caseRanges) {
            if (range.Start < cursor || range.Length <= 0 || range.Start > request.Length - range.Length || string.IsNullOrWhiteSpace(range.Name)
                || !characterBoundaries.Contains(range.Start) || !characterBoundaries.Contains(range.Start + range.Length))
                throw Invalid("El ámbito de datos no pertenece a rangos originales no superpuestos.");
            cursor = range.Start + range.Length;
        }
        requestUnits = Units(request, caseRanges); sourceUnits = string.IsNullOrWhiteSpace(sourceText) ? [] : Units(sourceText);
        this.literals = (literals ?? []).Where(literal => !caseRanges.Any(range => Overlaps(literal.Start, literal.Length, range.Start, range.Length))).ToArray();
        if (Literals.Any(item => item.Start < 0 || item.Length <= 0 || item.Start > request.Length - item.Length
            || request.Substring(item.Start, item.Length) != item.Text) || Literals.Select(item => item.Id).Distinct().Count() != Literals.Length)
            throw Invalid("Las referencias literales no corresponden a ocurrencias del encargo original.");
    }

    public AuthoringObligationContract WithInventory(AuthoringInventory inventory)
    {
        if (Resolved || Ledger.Length > 0) throw Invalid("El inventario no puede reinterpretarse después de fijarse.");
        if (!string.IsNullOrWhiteSpace(inventory.Question)) {
            if (inventory.Request.Length != 0 || inventory.Source.Length != 0 || inventory.Replacements.Length != 0 || inventory.VerbatimBodyReference is not null || inventory.Reduction is not null)
                throw Invalid("Una aclaración suspende el inventario completo; no debe confirmar una selección parcial.");
            Question = inventory.Question; return this;
        }
        ValidateDisposition(inventory.Request, RequestUnits, source: false);
        ValidateDisposition(inventory.Source, SourceUnits, source: true);
        var dataIds = InputDataUnitIds.ToHashSet();
        foreach (var item in inventory.Request) {
            var units = Enumerable.Range(item.First, item.Last - item.First + 1).ToArray();
            if (item.Role == "INPUT_DATA" ? units.Any(id => !dataIds.Contains(id)) : units.Any(dataIds.Contains))
                throw Invalid("Los datos ligados por el propietario de campos no pueden reinterpretarse como instrucciones o autorización.");
        }
        if (inventory.Reduction is { } reduction)
            reductionAuthority = RequestAuthority(reduction.First, reduction.Last);
        var rows = new List<AuthoringLedgerEntry>();
        foreach (var item in inventory.Request) {
            var span = Span(OriginalRequest, RequestUnits, item.First, item.Last);
            rows.Add(new(rows.Count + 1, "REQUEST", span.Start, span.Length, item.Role == "INPUT_DATA" ? "[dato del caso excluido]" : span.Text, item.Role, null, []));
        }
        var used = new HashSet<int>();
        foreach (var item in inventory.Source) {
            var span = Span(SourceText!, SourceUnits, item.First, item.Last);
            string? replacement = null; AuthoringAuthoritySpan[] authority = [];
            if (item.Role == "REPLACED") {
                var matches = inventory.Replacements.Select((edit, index) => (edit, index))
                    .Where(pair => pair.edit.SourceFirst == item.First && pair.edit.SourceLast == item.Last).ToArray();
                if (matches.Length != 1) throw Invalid("Cada cambio del origen necesita un delta único ligado a ese rango.");
                var (edit, index) = matches[0]; used.Add(index);
                authority = [RequestAuthority(edit.AuthorityFirst, edit.AuthorityLast)];
                if ((edit.RequestFirst is null) != (edit.RequestLast is null)) throw Invalid("El reemplazo debe definir ambos extremos o una eliminación explícita.");
                var replacementValue = edit.RequestFirst is { } first ? RequestAuthority(first, edit.RequestLast!.Value).Text : "";
                replacement = ReplacementSeams(span.Text, replacementValue);
            }
            rows.Add(new(rows.Count + 1, "SOURCE", span.Start, span.Length, span.Text, item.Role, replacement, authority));
        }
        if (used.Count != inventory.Replacements.Length) throw Invalid("Un delta no puede alterar un rango no declarado del origen.");
        if (inventory.VerbatimBodyReference is { } id) {
            var literal = Literals.SingleOrDefault(item => item.Id == id) ?? throw Invalid("El cuerpo literal no pertenece a este encargo.");
            if (SourceUnits.Length > 0 || inventory.Replacements.Length > 0) throw Invalid("Un cuerpo literal completo no admite deltas de una fuente diferente.");
            literalBody = literal.Text;
            literalStart = literal.Start;
        }
        ledger = rows.ToArray();
        if (Ledger.Any(row => row.Role == "UNRESOLVED")) {
            var first = Ledger.First(row => row.Role == "UNRESOLVED").Text;
            Question = "Aclara qué debe conservar la instrucción futura en este fragmento: " + first[..Math.Min(300, first.Length)];
            return this;
        }
        if (ObligationIds.Length == 0 && literalBody is null) throw Invalid("El inventario no identifica ninguna tarea futura; aclara el encargo.");
        Resolved = true; return this;
    }

    public string Compose(PromptInputContract? inputs = null, AuthoringClause[]? clauses = null, AuthoringReferenceCoverage? references = null)
    {
        if (!Resolved) throw Invalid("Falta resolver el inventario antes de componer una instrucción.");
        if (clauses is not null && (clauses.Length != ObligationIds.Length || clauses.Select(item => item.Id).Distinct().Count() != clauses.Length
            || !clauses.Select(item => item.Id).ToHashSet().SetEquals(ObligationIds) || clauses.Any(item => item.Text is null || item.Text.Length > 32000)))
            throw Invalid("Cada obligación y contexto necesita una propuesta única, sin IDs inventados ni omitidos.");
        var proposed = clauses?.ToDictionary(item => item.Id, item => item.Text) ?? new Dictionary<int, string>();
        string Render(AuthoringLedgerEntry row)
        {
            var elaboration = proposed.GetValueOrDefault(row.Id, "");
            if (row.Role == "CONTEXT") {
                if (string.IsNullOrWhiteSpace(elaboration)) throw Invalid("La redacción reformulable necesita una propuesta de contenido, no una omisión.");
                return elaboration.TrimEnd() + "\n";
            }
            var fixedText = row.Role == "REPLACED" ? row.ReplacementText! : row.Text;
            return fixedText;
        }
        var source = string.Concat(Ledger.Where(row => row.Source == "SOURCE").Select(Render));
        var request = string.Concat(Ledger.Where(row => row.Source == "REQUEST" && row.Role is "FUTURE_TASK" or "CONTEXT").Select(Render));
        var additions = Ledger.Where(row => row.Role == "FUTURE_TASK")
            .Select(row => proposed.GetValueOrDefault(row.Id, ""))
            .Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var body = literalBody ?? string.Join("\n\n", new[] { source.Trim(), request.Trim() }.Concat(additions).Where(text => text.Length > 0));
        if (inputs is { Enforced: true }) {
            if (literalBody is not null && !inputs.Matches(body)) throw Invalid("Los campos solicitados son incompatibles con el cuerpo literal completo.");
            body = inputs.Bind(EffectiveSource, body);
        }
        else if (SourceText is not null && literalBody is null)
            body = TemplateSourceBinding.Bind(EffectiveSource, body).Text;
        if (string.IsNullOrWhiteSpace(body) || body.Length > 32000) throw Invalid("La composición no cabe completa o no contiene una instrucción.");
        var sourceReferences = new AuthoringReferenceCoverage(EffectiveSource);
        sourceReferences = sourceReferences.WithScopes(new(sourceReferences.References.Select((uri, index) =>
            new AuthoringReferenceUse(index + 1, AuthoringReferenceRole.FUTURE_TASK, uri)).ToArray(), ""));
        sourceReferences.ValidateBody(body);
        references?.ValidateBody(body);
        // A bounded generation repair may propose another exact body under the
        // SAME frozen inventory. This receipt verifies composition, never meaning.
        composedBody = body; return body;
    }

    public bool IsComposed(string body) => composedBody is not null && body == composedBody;
    public AuthoringContentReview GetContentReview(string body) => new("REQUIRES_HUMAN_CONTENT_REVIEW", Fingerprint, LiveRuns.Hash(body),
        AuthoringRequest, new(SourceId, SourceRevision, SourceText is null ? null : LiveRuns.Hash(SourceText), SourceText), Ledger);

    private static AuthoringLexicalUnit[] Units(string text, AuthoringCaseRange[]? data = null)
    {
        var units = new List<AuthoringLexicalUnit>(); var boundaries = new[] { 0, text.Length }
            .Concat((data ?? []).SelectMany(range => new[] { range.Start, range.Start + range.Length })).Distinct().Order().ToArray();
        for (var part = 0; part < boundaries.Length - 1; part++) {
            var offset = boundaries[part]; var endPart = boundaries[part + 1]; var segment = text[offset..endPart];
            if ((data ?? []).Any(range => range.Start == offset && range.Start + range.Length == endPart)) {
                units.Add(new(units.Count + 1, offset, segment.Length, "[dato del caso excluido]")); continue;
            }
            var matches = Regex.Matches(segment, @"\S+");
            if (matches.Count == 0 && segment.Length > 0) { units.Add(new(units.Count + 1, offset, segment.Length, segment)); continue; }
            for (var index = 0; index < matches.Count; index++) {
                var start = index == 0 ? 0 : matches[index].Index;
                var end = index + 1 < matches.Count ? matches[index + 1].Index : segment.Length;
                units.Add(new(units.Count + 1, offset + start, end - start, segment[start..end]));
            }
        }
        return units.ToArray();
    }
    private static void ValidateDisposition(AuthoringDisposition[] spans, AuthoringLexicalUnit[] units, bool source)
    {
        var cursor = 1;
        foreach (var span in spans) {
            if (span.First != cursor || span.Last < span.First || span.Last > units.Length)
                throw Invalid("Cada unidad original debe tener exactamente una disposición contigua, sin omisiones ni solapes.");
            var allowed = source ? new[] { "FUTURE_TASK", "CONTEXT", "REPLACED", "UNRESOLVED" }
                : new[] { "FUTURE_TASK", "CONTEXT", "INPUT_DATA", "CURRENT_WORKFLOW", "METADATA", "UNRESOLVED" };
            if (!allowed.Contains(span.Role, StringComparer.Ordinal)) throw Invalid("El papel del tramo no pertenece al contrato de su fuente.");
            cursor = span.Last + 1;
        }
        if (cursor != units.Length + 1) throw Invalid("El inventario omite parte del encargo o del origen.");
    }
    private static AuthoringAuthoritySpan Span(string text, AuthoringLexicalUnit[] units, int first, int last)
    {
        if (first < 1 || last < first || last > units.Length) throw Invalid("El rango no pertenece al material original.");
        var start = units[first - 1].Start; var end = units[last - 1].Start + units[last - 1].Length;
        return new(start, end - start, text[start..end]);
    }
    private AuthoringAuthoritySpan RequestAuthority(int first, int last)
    {
        var span = Span(OriginalRequest, requestUnits, first, last);
        if (caseRanges.Any(range => Overlaps(span.Start, span.Length, range.Start, range.Length)))
            throw Invalid("Un dato del caso no puede autorizar una transformación ni convertirse en contenido reutilizable.");
        return span;
    }
    private static bool Overlaps(int first, int length, int other, int otherLength) => first < other + otherLength && other < first + length;
    private static string ReplacementSeams(string original, string replacement)
    {
        var leading = original.Length - original.TrimStart().Length;
        var trailing = original.Length - original.TrimEnd().Length;
        var prefix = leading > 0 && (replacement.Length == 0 || !char.IsWhiteSpace(replacement[0])) ? original[..leading] : "";
        var suffix = trailing > 0 && (replacement.Length == 0 || !char.IsWhiteSpace(replacement[^1])) ? original[^trailing..] : "";
        return prefix + replacement + suffix;
    }
    private static ValidationException Invalid(string message)
    {
        var failure = new ValidationException(message); failure.Data["NovaValidationCode"] = "AUTHORING_OBLIGATION_CONTRACT_INVALID";
        failure.Data["NovaStageRepairExhausted"] = true; return failure;
    }
}
