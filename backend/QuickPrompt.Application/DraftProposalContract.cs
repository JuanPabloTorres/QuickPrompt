using System.Globalization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed record DraftSourceSnapshot(Guid? PromptId, int? Revision, string TemplateText, string Title, string Description,
    int CategoryId, string? SavedBodyHash = null, Guid? WorkingDraftId = null, string? WorkingBodyHash = null,
    string? SavedTemplateText = null);
public sealed record LiteralChange(string Id, string Kind, string? OriginalText, string? ProposedText);
public sealed record LiteralChanges(LiteralChange[] Entries);

/// <summary>
/// App-owned provenance and technical bounds for one AI proposal. It interprets
/// neither user intent nor source clauses. Only an actual content approval can
/// accept the exact proposal and its host-computed literal changes.
/// </summary>
public sealed class DraftProposalContract
{
    private readonly AuthoringCaseRange[] caseRanges;
    private readonly AuthoringLiteralCandidate[] literals;
    private readonly AuthoringClarification[] clarifications;
    private PromptInput? registered;
    public string OriginalRequest { get; }
    public DraftSourceSnapshot? Source { get; }
    public DraftSourceSnapshot? SourceSnapshot => Source;
    public PromptInputContract FieldContract { get; }
    public Guid? SourceId => Source?.WorkingDraftId is null ? Source?.PromptId : null;
    public int? SourceRevision => Source?.WorkingDraftId is null ? Source?.Revision : null;
    public string? SourceText => Source?.TemplateText;
    public string? SavedSourceText => Source?.SavedTemplateText;
    public string? SourceSavedBodyHash => Source?.SavedBodyHash;
    public Guid? WorkingDraftId => Source?.WorkingDraftId;
    public string? WorkingBodyHash => Source?.WorkingBodyHash;
    public AuthoringLiteralCandidate[] Literals => [.. literals];
    public AuthoringClarification[] UserClarifications => [.. clarifications];
    public string Fingerprint => clarifications.Length == 0 ? BaseFingerprint : LiveRuns.Hash(new { original = BaseFingerprint, userClarifications = clarifications });
    private string BaseFingerprint => LiveRuns.Hash(new { OriginalRequest, Source, FieldContract, caseRanges, literals });
    public string AuthoringRequest => ProjectRequest();

    public DraftProposalContract(string request, DraftSourceSnapshot? source, PromptInputContract fieldContract,
        AuthoringCaseRange[]? inputData = null, AuthoringLiteralCandidate[]? literalCandidates = null,
        AuthoringClarification[]? userClarifications = null)
    {
        // A saved-only snapshot has one body. A working snapshot must retain the
        // actual saved baseline explicitly: an unapproved draft is not that baseline.
        if (source is { PromptId: not null, WorkingDraftId: null, SavedTemplateText: null })
            source = source with { SavedTemplateText = source.TemplateText };
        if (string.IsNullOrWhiteSpace(request) || request.Length > 32000 || source?.TemplateText.Length > 32000
            || source?.SavedTemplateText?.Length > 32000)
            throw new ValidationException("La petición y el origen deben caber completos en la preparación.");
        if (source is not null && ((source.PromptId is null) != (source.Revision is null)
            || source.PromptId is null && source.WorkingDraftId is null
            || (source.WorkingDraftId is null) != (source.WorkingBodyHash is null)
            || source.WorkingBodyHash is { } workingHash && workingHash != LiveRuns.Hash(source.TemplateText)
            || source.PromptId is not null && (source.SavedTemplateText is null || source.SavedBodyHash != LiveRuns.Hash(source.SavedTemplateText))
            || source.PromptId is null && (source.SavedTemplateText is not null || source.SavedBodyHash is not null)
            || source.WorkingDraftId is null && source.SavedTemplateText != source.TemplateText))
            throw new ValidationException("La identidad, revisión y huellas del origen no están ligadas correctamente.");
        OriginalRequest = request; Source = source; FieldContract = fieldContract ?? throw new ArgumentNullException(nameof(fieldContract));
        clarifications = AuthoringClarifications.Copy(userClarifications);
        FieldContract.ValidateBasis(request, source?.TemplateText ?? "");
        if (!FieldContract.Enforced) throw new ValidationException("Los campos de la propuesta requieren una selección explícita del usuario.");
        caseRanges = (inputData ?? []).OrderBy(range => range.Start).ToArray();
        var boundaries = StringInfo.ParseCombiningCharacters(request).Append(request.Length).ToHashSet(); var cursor = 0;
        foreach (var range in caseRanges) {
            if (range.Start < cursor || range.Length <= 0 || range.Start > request.Length - range.Length
                || !boundaries.Contains(range.Start) || !boundaries.Contains(range.Start + range.Length))
                throw new ValidationException("El ámbito de datos no corresponde a rangos originales completos y no superpuestos.");
            cursor = range.Start + range.Length;
        }
        var supplied = literalCandidates ?? [];
        if (supplied.Select(item => item.Id).Distinct().Count() != supplied.Length || supplied.Any(item => item.Start < 0 || item.Length <= 0
            || item.Start > request.Length - item.Length || request.Substring(item.Start, item.Length) != item.Text))
            throw new ValidationException("La referencia literal no pertenece a una ocurrencia original de la petición.");
        literals = supplied.Where(literal => !caseRanges.Any(range => Overlaps(literal.Start, literal.Length, range.Start, range.Length))).ToArray();
    }

    /// <summary>Bind generated text and metadata only after technical validation; no semantic approval is recorded.</summary>
    public PromptInput RegisterProposal(PromptInput proposal, int? literalReference = null, AuthoringReferenceCoverage? references = null)
    {
        if (string.IsNullOrWhiteSpace(proposal.Title) || proposal.Title.Length > 160 || proposal.Title.Contains('\r') || proposal.Title.Contains('\n'))
            throw new ValidationException("El título propuesto debe ser una única línea de hasta 160 caracteres.");
        var body = proposal.TemplateText;
        if (literalReference is { } id) {
            body = literals.SingleOrDefault(item => item.Id == id)?.Text
                ?? throw new ValidationException("La propuesta eligió una referencia literal ajena a esta petición.");
            if (!FieldContract.Matches(body)) throw new ValidationException("El cuerpo literal y los campos elegidos no coinciden; aclara la petición.");
        }
        else body = FieldContract.BindFieldsOnly(body);
        new Template { Title = proposal.Title, Description = proposal.Description, Template = body }.Validate();
        if (body.Length > 32000) throw new ValidationException("La propuesta excede el límite completo; no se truncará.");
        references?.ValidateBody(body);
        registered = proposal with { TemplateText = body };
        return registered;
    }

    public bool MatchesProposal(string body) => registered is not null && registered.TemplateText == body;

    public AuthoringContentReview GetContentReview(string body) => new("REQUIRES_HUMAN_CONTENT_REVIEW", Fingerprint, LiveRuns.Hash(body),
        AuthoringRequest, new(Source?.PromptId, Source?.Revision, SourceText is null ? null : LiveRuns.Hash(SourceText), SourceText) {
            SourceTitle = Source?.Title, SourceDescription = Source?.Description, SourceCategoryId = Source?.CategoryId,
            SavedBodyHash = SourceSavedBodyHash, SavedOriginalText = SavedSourceText,
            WorkingDraftId = WorkingDraftId, WorkingBodyHash = WorkingBodyHash
        }, BuildLedger()) { LiteralChanges = Changes(body), UserClarifications = UserClarifications };

    private LiteralChanges Changes(string body)
    {
        var entries = new List<LiteralChange>(); var bodyHash = LiveRuns.Hash(body);
        void Add(string kind, string? original, string? proposed) => entries.Add(new(
            LiveRuns.Hash(new { contract = Fingerprint, bodyHash, kind, original, proposed }), kind, original, proposed));
        var baselines = new[] { SavedSourceText, SourceText }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        foreach (var clause in baselines.SelectMany(text => TemplateConstraints.Missing(text, body)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            Add("MISSING_SOURCE_CLAUSE", clause, null);
        var before = baselines.SelectMany(text => new AuthoringReferenceCoverage(text).References).ToHashSet(StringComparer.Ordinal);
        var after = new AuthoringReferenceCoverage(body).References.ToHashSet(StringComparer.Ordinal);
        foreach (var uri in before.Except(after).Order(StringComparer.Ordinal)) Add("REMOVED_SOURCE_URI", uri, null);
        // An URI introduced in an unapproved working draft is still new relative
        // to the saved source; carrying it forward must not wash out that change.
        var additionBaseline = new AuthoringReferenceCoverage(SavedSourceText ?? SourceText ?? "").References;
        foreach (var uri in after.Except(additionBaseline, StringComparer.Ordinal).Order(StringComparer.Ordinal)) Add("ADDED_URI", null, uri);
        if (entries.Count > 200)
            throw new ValidationException("La propuesta contiene más de 200 cambios literales; reduce el alcance para revisarlos completos.");
        return new(entries.ToArray());
    }

    private AuthoringLedgerEntry[] BuildLedger()
    {
        var rows = new List<AuthoringLedgerEntry>(); var cursor = 0;
        void Add(int start, int length, string text, string role) => rows.Add(new(rows.Count + 1, "REQUEST", start, length, text, role, null, []));
        foreach (var range in caseRanges) {
            if (range.Start > cursor) Add(cursor, range.Start - cursor, OriginalRequest[cursor..range.Start], "CONTEXT");
            Add(range.Start, range.Length, "[dato del caso excluido]", "INPUT_DATA"); cursor = range.Start + range.Length;
        }
        if (cursor < OriginalRequest.Length) Add(cursor, OriginalRequest.Length - cursor, OriginalRequest[cursor..], "CONTEXT");
        if (SourceText is { } text) rows.Add(new(rows.Count + 1, "SOURCE", 0, text.Length, text, "CONTEXT", null, []));
        return rows.ToArray();
    }
    private string ProjectRequest()
    {
        var text = OriginalRequest.ToCharArray();
        foreach (var range in caseRanges) Array.Fill(text, '*', range.Start, range.Length);
        return new(text);
    }
    private static bool Overlaps(int start, int length, int other, int otherLength) => start < other + otherLength && other < start + length;
}
