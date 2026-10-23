using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Tests the shared state contract; real language interpretation is a separate gate.
public sealed class ConversationArtifactTests
{
    [Fact] public void WorkingReferenceRejectsForeignStaleChangedAndExpiredState()
    {
        var first = Observe(Draft(), kind: DraftObservationKind.NewArtifact);
        var reference = first.WorkingDraftReference!;
        Assert.Equal(reference, sessions.RequireWorkingDraft(reference));
        var invalid = new[] { reference with { SessionId = Guid.NewGuid() }, reference with { DraftId = Guid.NewGuid() },
            reference with { DraftRevision = 2 }, reference with { CurrentContent = "Otro contenido" },
            reference with { SourceRef = new(Guid.NewGuid(), 1) } };
        foreach (var candidate in invalid) Assert.Throws<ConflictException>(() => sessions.RequireWorkingDraft(candidate));
        var changed = Observe(first.Draft! with { TemplateText = "Resume <tema>." }, kind: DraftObservationKind.Authored);
        Assert.Throws<ConflictException>(() => sessions.RequireWorkingDraft(reference));
        Assert.Equal(changed.WorkingDraftReference, sessions.RequireWorkingDraft(changed.WorkingDraftReference!));
        sessions.Put(changed with { UpdatedAt = DateTime.UtcNow.AddHours(-3) });
        Assert.Throws<ConflictException>(() => sessions.RequireWorkingDraft(changed.WorkingDraftReference!));
    }
    private readonly AgentSessions sessions = new();
    private readonly Guid sessionId = Guid.NewGuid();
    private static PromptDocument Source() => new(Guid.NewGuid(), "Ficha", "", "Explica <tema>.", 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
    private PreparedDraft Draft(PromptDocument? source = null, SemanticInterpretation? semantic = null) => new(Guid.NewGuid(),
        PreparationState.NEEDS_INFORMATION, PreparationIntent.CREATE_PROMPT,
        source is null ? PreparationDecision.CREATE_NEW : PreparationDecision.ADAPT_EXISTING, "Ficha", "", "Explica <tema>.", null,
        [new("tema", true)], [], ["tema"], "", "Borrador sintético", source is null ? [] : [new(source, 10, "Origen")],
        source?.Id, source?.Revision, 0, false, DateTime.UtcNow, sessionId, Semantic: semantic);
    private AgentSession Observe(PreparedDraft draft, PromptDocument? source = null, DraftObservationKind kind = DraftObservationKind.Snapshot)
        => sessions.ObserveDraft(new(sessionId, source, draft, [], DateTime.UtcNow, LastProposal: draft.Semantic), kind);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FieldAndCompilationSnapshotsPreserveIdentityRevisionAndAuthorship(bool sourceBound)
    {
        var source = sourceBound ? Source() : null;
        var provenance = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION]);
        var first = Observe(Draft(source, provenance), source, DraftObservationKind.Authored);
        var operation = first.Draft! with { Id = Guid.NewGuid(), Semantic = SemanticFixture.Plan([SemanticOperation.FILL_VARIABLES]),
            Values = [new("tema", "SQLite", "USER_INPUT", "VALIDATED")], CompiledText = "Explica SQLite.", MissingVariables = [], State = PreparationState.READY };
        var next = Observe(operation, source);
        Assert.Equal(first.WorkingDraftReference, next.WorkingDraftReference);
        Assert.Same(provenance, next.LastProposal); Assert.Same(provenance, next.Draft!.Semantic);
        Assert.Equal("SQLite", Assert.Single(next.Draft.Values).Value);
        Assert.Equal("NOT_GRANTED", next.Approval);
        Assert.NotEqual(operation.Id, next.Draft.Id); Assert.NotEqual(first.Draft!.Id, operation.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualChangesClearOldRequirementsAndIncrementOnce(bool titleOnly)
    {
        var first = Observe(Draft(semantic: SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION])), kind: DraftObservationKind.Authored);
        var manuallyEdited = first.Draft! with { Id = Guid.NewGuid(), Title = titleOnly ? "Título nuevo" : first.Draft.Title,
            TemplateText = titleOnly ? first.Draft.TemplateText : "Resume <tema>." };
        var next = Observe(manuallyEdited);
        Assert.Equal(first.Draft.Id, next.Draft!.Id); Assert.Equal(2, next.DraftRevision);
        Assert.Null(next.LastProposal); Assert.Null(next.Draft.Semantic);
        var repeated = Observe(manuallyEdited);
        Assert.Equal(next.WorkingDraftReference, repeated.WorkingDraftReference);
        Assert.Null(repeated.LastProposal);
    }

    [Fact]
    public void VerifiedRevisionReplacesProvenanceWithoutReplacingWorkingIdentity()
    {
        var first = Observe(Draft(semantic: SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT])), kind: DraftObservationKind.NewArtifact);
        var nextProvenance = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION]);
        var next = Observe(first.Draft! with { TemplateText = "Resume <tema>.", Semantic = nextProvenance }, kind: DraftObservationKind.Authored);
        Assert.Equal(first.Draft!.Id, next.Draft!.Id); Assert.Equal(2, next.DraftRevision);
        Assert.Same(nextProvenance, next.LastProposal); Assert.Same(nextProvenance, next.Draft.Semantic);
    }

    [Fact]
    public void IndependentCreationWithIdenticalTextStillStartsANewArtifact()
    {
        var first = Observe(Draft(), kind: DraftObservationKind.NewArtifact);
        var next = Observe(first.Draft!, kind: DraftObservationKind.NewArtifact);
        Assert.NotEqual(first.Draft!.Id, next.Draft!.Id); Assert.Equal(1, next.DraftRevision);
    }

    [Fact]
    public void RestoredOperationKeepsItsProvenanceAndCanDifferFromTheSelectedSource()
    {
        var source = Source(); var selected = Source();
        var provenance = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION]);
        var operation = Draft(source, provenance);
        var restored = sessions.ObserveDraft(new(sessionId, selected, operation, new() { ["tema"] = "Valor de la selección" }, DateTime.UtcNow), DraftObservationKind.Restored);
        Assert.Equal(selected.Id, restored.Source!.Id); Assert.Equal(source.Id, restored.Draft!.SourceId);
        Assert.NotEqual(operation.Id, restored.Draft.Id); Assert.Same(provenance, restored.LastProposal);
        Assert.Equal("Valor de la selección", restored.Values["tema"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherSourceOrRevisionStartsNewIdentityAndDropsStaleProvenance(bool sameSource)
    {
        var source = Source(); var first = Observe(Draft(source, SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION])), source, DraftObservationKind.Authored);
        var other = sameSource ? source with { Revision = 2 } : Source();
        var next = Observe(Draft(other), other);
        Assert.NotEqual(first.Draft!.Id, next.Draft!.Id); Assert.Equal(1, next.DraftRevision); Assert.Null(next.LastProposal);
    }

    [Fact]
    public void ObservationDoesNotMutateAnOperationOrInheritItsApprovalAndPendingEffects()
    {
        var operation = Draft(semantic: SemanticFixture.Plan([SemanticOperation.EXECUTE_LOCAL]));
        var before = operation.Id;
        sessions.Put(new(sessionId, null, null, [], DateTime.UtcNow, PendingPlan: operation.Semantic,
            PendingAction: SemanticOperation.EXECUTE_LOCAL, PendingRequest: "Petición anterior", Approval: "GRANTED"));
        var snapshot = Observe(operation);
        Assert.Equal(before, operation.Id); Assert.NotEqual(before, snapshot.Draft!.Id);
        Assert.Null(snapshot.Draft.Semantic); Assert.Null(snapshot.PendingPlan); Assert.Null(snapshot.PendingAction); Assert.Null(snapshot.PendingRequest);
        Assert.Equal("NOT_GRANTED", snapshot.Approval);
    }

    [Fact]
    public void IndependentSessionsDoNotShareWorkingIdentityOrProvenance()
    {
        var first = Observe(Draft(semantic: SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT])), kind: DraftObservationKind.NewArtifact);
        var otherId = Guid.NewGuid();
        var other = sessions.ObserveDraft(new(otherId, null, first.Draft! with { SessionId = otherId }, [], DateTime.UtcNow));
        Assert.NotEqual(first.Draft!.Id, other.Draft!.Id); Assert.Null(other.LastProposal);
        Assert.Same(first, sessions.Get(sessionId));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("source")]
    [InlineData("revision")]
    public void InvalidObservationLeavesPreviousContextIntact(string invalid)
    {
        var source = Source(); var first = Observe(Draft(source), source);
        var bad = first.Draft! with { SessionId = invalid == "session" ? Guid.NewGuid() : sessionId,
            SourceId = invalid == "source" ? Guid.NewGuid() : source.Id, SourceRevision = invalid == "revision" ? 2 : source.Revision };
        Assert.Throws<ValidationException>(() => Observe(bad, source));
        Assert.Same(first, sessions.Get(sessionId));
    }

    [Fact]
    public async Task ConcurrentIdenticalObservationsDoNotRestartOrAdvanceTheWorkingRevision()
    {
        var first = Observe(Draft(), kind: DraftObservationKind.NewArtifact);
        var copies = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Observe(first.Draft! with { Id = Guid.NewGuid() }))));
        Assert.All(copies, item => Assert.Equal(first.WorkingDraftReference, item.WorkingDraftReference));
        Assert.Equal(first.WorkingDraftReference, sessions.Get(sessionId)!.WorkingDraftReference);
    }
}
