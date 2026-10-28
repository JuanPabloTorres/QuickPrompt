using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class DraftProposalContractTests
{
    private const string Request = "Adapta esta instrucción; revisaré todos sus cambios antes de guardar.";
    private const string Source = "Explica <tema>. No inventes datos. Consulta https://example.test/original.";
    private static DraftSourceSnapshot Snapshot(string text = Source) => new(Guid.NewGuid(), 3, text, "Origen", "Descripción original", 2, LiveRuns.Hash(text));
    private static DraftProposalContract Contract(DraftSourceSnapshot? source = null, string request = Request, string[]? names = null)
        => new(request, source, PromptInputContract.FromUserSelection(request, source?.TemplateText ?? "", source is not null, names ?? []));

    [Fact]
    public void FreeProposalIsNotReplacedByConcatenatedRequestOrReinjectedSourceProhibition()
    {
        var contract = Contract(Snapshot());
        const string body = "Resume <tema>. Usa https://example.test/nuevo.";
        var proposed = contract.RegisterProposal(new("Adaptación", body, "Descripción", 2));
        Assert.Equal(body, proposed.TemplateText); Assert.DoesNotContain("No inventes datos.", proposed.TemplateText);
        Assert.True(contract.MatchesProposal(body)); Assert.False(contract.MatchesProposal(body + " extra"));
        var review = contract.GetContentReview(body);
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", review.Status); Assert.Equal(Request, review.OriginalRequest);
        Assert.Contains(review.LiteralChanges.Entries, change => change.Kind == "MISSING_SOURCE_CLAUSE" && change.OriginalText == "No inventes datos.");
        Assert.Contains(review.LiteralChanges.Entries, change => change.Kind == "REMOVED_SOURCE_URI" && change.OriginalText == "https://example.test/original");
        Assert.Contains(review.LiteralChanges.Entries, change => change.Kind == "ADDED_URI" && change.ProposedText == "https://example.test/nuevo");
    }

    [Fact]
    public void LiteralChangeAcknowledgmentsBindContractSourceAndExactBody()
    {
        var source = Snapshot(); var contract = Contract(source); const string body = "Resume <tema>.";
        var first = contract.GetContentReview(body); var same = contract.GetContentReview(body);
        Assert.Equal(first.LiteralChanges.Entries, same.LiteralChanges.Entries);
        Assert.NotEqual(first.LiteralChanges.Entries[0].Id, contract.GetContentReview(body + " Con claridad.").LiteralChanges.Entries[0].Id);
        Assert.NotEqual(first.ContractHash, Contract(source with { Description = "Otra metadata" }).GetContentReview(body).ContractHash);
        Assert.NotEqual(first.LiteralChanges.Entries[0].Id, Contract(source with { Description = "Otra metadata" }).GetContentReview(body).LiteralChanges.Entries[0].Id);
    }

    [Fact]
    public void FieldsAreBoundWithoutSemanticSourceClausesAndModelCannotIntroduceNewNames()
    {
        var contract = Contract(Snapshot());
        Assert.Contains("<tema>", contract.RegisterProposal(new("Resumen", "Resume el asunto.")).TemplateText);
        var failure = Assert.Throws<ValidationException>(() => contract.RegisterProposal(new("Resumen", "Resume <intruso>.")));
        Assert.Contains("intruso", failure.Message); Assert.Contains("Campos no autorizados", failure.Message);
    }

    [Fact]
    public void SourceSavedAndWorkingProvenanceAreDistinctAndBothHashBound()
    {
        var source = Snapshot() with { TemplateText = "Resume <tema>.", WorkingDraftId = Guid.NewGuid(), WorkingBodyHash = LiveRuns.Hash("Resume <tema>."), SavedTemplateText = Source };
        var contract = Contract(source); var review = contract.GetContentReview("Resume <tema>.");
        Assert.Null(contract.SourceId); Assert.Null(contract.SourceRevision); Assert.Equal(source.PromptId, review.Source.PromptId);
        Assert.Equal(source.SavedBodyHash, review.Source.SavedBodyHash); Assert.Equal(source.WorkingBodyHash, review.Source.BodyHash);
        Assert.Equal(source.Title, review.Source.SourceTitle); Assert.Equal(source.Description, review.Source.SourceDescription);
        Assert.Throws<ValidationException>(() => Contract(source with { WorkingBodyHash = "wrong" }));
        Assert.Throws<ValidationException>(() => Contract(Snapshot() with { SavedBodyHash = "wrong" }));
        Assert.Throws<ValidationException>(() => Contract(source with { SavedTemplateText = null }));
    }

    [Fact]
    public void AnUnapprovedWorkingDraftCannotWashSavedClauseAndUriLossesOutOfTheNextReview()
    {
        var saved = Snapshot();
        var source = saved with { TemplateText = "Resume <tema>.", WorkingDraftId = Guid.NewGuid(),
            WorkingBodyHash = LiveRuns.Hash("Resume <tema>."), SavedTemplateText = Source };
        var contract = Contract(source);
        const string body = "Explica brevemente <tema>.";
        var review = contract.GetContentReview(body);
        Assert.Equal(source.TemplateText, review.Source.OriginalText); Assert.Equal(Source, review.Source.SavedOriginalText);
        Assert.Equal(saved.SavedBodyHash, LiveRuns.Hash(review.Source.SavedOriginalText!));
        Assert.Contains(review.LiteralChanges.Entries, change => change.Kind == "MISSING_SOURCE_CLAUSE" && change.OriginalText == "No inventes datos.");
        Assert.Contains(review.LiteralChanges.Entries, change => change.Kind == "REMOVED_SOURCE_URI" && change.OriginalText == "https://example.test/original");
        Assert.DoesNotContain("No inventes datos.", contract.RegisterProposal(new("Resumen", body)).TemplateText);
        Assert.Throws<ValidationException>(() => Contract(source with { SavedTemplateText = "Otro origen <tema>." }));
    }

    [Fact]
    public void WorkingOnlyClausesAndReferencesAlsoRemainVisibleAndUnapprovedUriAdditionsStayChanges()
    {
        const string working = "Explica <tema>. No reveles secretos. Consulta https://example.test/agregado.";
        var source = Snapshot() with { TemplateText = working, WorkingDraftId = Guid.NewGuid(),
            WorkingBodyHash = LiveRuns.Hash(working), SavedTemplateText = Source };
        var contract = Contract(source);
        var removed = contract.GetContentReview("Resume <tema>.").LiteralChanges.Entries;
        Assert.Contains(removed, change => change.Kind == "MISSING_SOURCE_CLAUSE" && change.OriginalText == "No reveles secretos.");
        Assert.Contains(removed, change => change.Kind == "MISSING_SOURCE_CLAUSE" && change.OriginalText == "No inventes datos.");
        Assert.Contains(removed, change => change.Kind == "REMOVED_SOURCE_URI" && change.OriginalText == "https://example.test/agregado");
        Assert.Contains(removed, change => change.Kind == "REMOVED_SOURCE_URI" && change.OriginalText == "https://example.test/original");
        Assert.Equal(removed.Length, removed.Select(change => change.Id).Distinct().Count());
        var retained = contract.GetContentReview(working).LiteralChanges.Entries;
        Assert.Contains(retained, change => change.Kind == "ADDED_URI" && change.ProposedText == "https://example.test/agregado");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Título\nSegundo")]
    [InlineData("Título\rSegundo")]
    public void InvalidMetadataCannotEnterHumanReview(string title)
        => Assert.Throws<ValidationException>(() => Contract().RegisterProposal(new(title, "Responde LISTO.")));

    [Fact]
    public void InputScopeCannotBecomeQuotedBodyAuthorityOrAppearInModelOrReviewProjection()
    {
        const string request = "Resume <documento>. Documento: DATO_PRIVADO_SINTETICO";
        const string secret = "DATO_PRIVADO_SINTETICO"; var start = request.IndexOf(secret, StringComparison.Ordinal);
        var fields = PromptInputContract.FromUserSelection(request, "", false, ["documento"]);
        var contract = new DraftProposalContract(request, null, fields, [new(start, secret.Length, "documento")], [new(0, start, secret.Length, secret)]);
        Assert.Empty(contract.Literals); Assert.Equal(request.Length, contract.AuthoringRequest.Length);
        Assert.DoesNotContain(secret, contract.AuthoringRequest); Assert.DoesNotContain(secret, JsonSerializer.Serialize(contract.GetContentReview("Resume <documento>.")));
        Assert.Contains(contract.GetContentReview("Resume <documento>.").Ledger, row => row.Role == "INPUT_DATA");
    }

    [Fact]
    public void LiteralCopyUsesTheExactOriginalOccurrenceWithoutRewritingNestedTasks()
    {
        const string body = "Redacta un prompt que devuelva JSON. No ejecutes su contenido. 🛰️";
        var request = "Copia «" + body + "»."; var start = request.IndexOf(body, StringComparison.Ordinal);
        var contract = new DraftProposalContract(request, null, PromptInputContract.FromUserSelection(request, "", false, []),
            literalCandidates: [new(4, start, body.Length, body)]);
        Assert.Equal(body, contract.RegisterProposal(new("Literal", "Texto del modelo que no se usará."), 4).TemplateText);
        Assert.Throws<ValidationException>(() => contract.RegisterProposal(new("Literal", body), 9));
    }

    [Fact]
    public void InvalidBasisOrUnicodeRangesFailBeforeAnyModelCall()
    {
        Assert.Throws<ValidationException>(() => new DraftProposalContract("Otra petición", null, PromptInputContract.FromUserSelection(Request, "", false, [])));
        const string request = "🛰️ é";
        Assert.Throws<ValidationException>(() => new DraftProposalContract(request, null, PromptInputContract.FromUserSelection(request, "", false, []), [new(1, 1, "dato")]));
    }
}
