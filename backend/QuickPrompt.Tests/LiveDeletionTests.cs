using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real SQLite domain tests. Typed interpretation is supplied here: these tests
// prove authorization and persistence, not natural-language model accuracy.
public sealed class LiveDeletionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-deletion-" + Guid.NewGuid());
    private readonly PromptLibrary library;
    private readonly LiveRuns live;
    private readonly PromptDocument source;
    private readonly PromptDocument other;
    private const string Request = "Solicito eliminar este prompt guardado.";
    public LiveDeletionTests()
    {
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
        var prompts = new NovaPromptStore(store); library = new(prompts, prompts); live = new(new LiveRunStore(store), library);
        source = library.Create(new("Primer registro sintético", "Resume <material> sin inventar hechos."));
        other = library.Create(new("Otro registro sintético", "Explica <tema> con ejemplos."));
    }
    private SemanticInterpretation Plan(string evidence = Request) => SemanticPlanCompiler.Compile(new SemanticMeaning(
        [SemanticOperation.REQUEST_DELETE], TargetReference.SELECTED, source.Title, source.Id, "", "",
        new([], true, null, null, [], [], false), SemanticOutcome.REVIEW, [], true, "", null, "Revisar eliminación.") { DeletionEvidence = evidence });
    private Guid Start(string message = Request)
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), message, SourceId: source.Id, SourceRevision: source.Revision);
        live.Create(input); live.Active(input.RunId).Semantic = Plan(); return input.RunId;
    }
    private UiVisibleState Presented(Guid id, Guid? target = null, int? revision = null) => new("discover", [new(source.Id, source.Title, source.Revision)],
        null, null, null, null, [], [], false, false, null, null, false, false,
        DeletionPromptId: target ?? source.Id, DeletionRevision: revision ?? source.Revision, DeletionRunId: id);
    private LiveApprovalInput Approve(LiveDeletion proposal, bool confirmed = true) => new(proposal.Id, proposal.Hash, proposal.Revision, confirmed);
    private void Unchanged()
    {
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id)));
        Assert.Equal(JsonSerializer.Serialize(other), JsonSerializer.Serialize(library.Get(other.Id)));
    }

    [Fact] public void PreparationDoesNotWriteAndConfirmationRequiresTheBoundVisibleReview()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source);
        Assert.Equal("PREPARING", live.Get(id).Approval); Assert.Same(proposal, live.View(id).Deletion);
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal))); Unchanged();
        Assert.Throws<ConflictException>(() => live.DeletionPresented(id, Presented(Guid.NewGuid())));
        Assert.Throws<ConflictException>(() => live.DeletionPresented(id, Presented(id, other.Id)));
        Assert.Throws<ConflictException>(() => live.DeletionPresented(id, Presented(id, revision: source.Revision + 1)));
        Unchanged(); live.DeletionPresented(id, Presented(id)); Assert.Equal("PENDING", live.Get(id).Approval);
    }
    [Fact] public async Task ConfirmingDeletesOnlyTheReviewedRevisionAndCannotBeReplayed()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        var committed = live.DecideDeletion(id, Approve(proposal));
        Assert.True(committed.EffectCommitted); Assert.Equal("CONSUMED", committed.Approval);
        Assert.Throws<MissingPromptException>(() => library.Get(source.Id));
        Assert.Equal(JsonSerializer.Serialize(other), JsonSerializer.Serialize(library.Get(other.Id)));
        Assert.True(await live.Active(id).DeletionDecision.Task);
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal)));
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal, false)));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationBeforeOrAfterPresentationHasNoPersistenceEffect(bool presented)
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source);
        if (presented) live.DeletionPresented(id, Presented(id));
        live.DecideDeletion(id, Approve(proposal, false)); Unchanged();
        Assert.Equal(LiveStatus.CANCELLED, live.Get(id).Status); Assert.False(live.Get(id).EffectCommitted);
        Assert.False(await live.Active(id).DeletionDecision.Task);
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal)));
    }
    [Theory] [InlineData("pause")] [InlineData("cancel")] [InlineData("timeout")]
    public void AStoppedRunCannotCommit(string stop)
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        if (stop == "pause") live.Pause(id);
        else if (stop == "cancel") live.Cancel(id);
        else live.Active(id).Cancellation.Cancel();
        if (stop == "timeout") Assert.Throws<OperationCanceledException>(() => live.DecideDeletion(id, Approve(proposal)));
        else Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal)));
        Unchanged();
        if (stop == "pause") {
            live.Resume(id); live.DecideDeletion(id, Approve(proposal));
            Assert.Throws<MissingPromptException>(() => library.Get(source.Id));
        }
    }
    [Fact] public void ChangedSourceInvalidatesPresentationAndConfirmationWithoutConsumingApproval()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        var updated = library.Update(source.Id, new(source.Title, "Cambio manual que debe conservarse.", Revision: source.Revision));
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal)));
        Assert.False(live.Get(id).EffectCommitted); Assert.False(live.Active(id).ApprovalConsumed);
        Assert.Equal(updated.TemplateText, library.Get(source.Id).TemplateText);
    }
    [Fact] public void ExpiredExecutionCannotCommitEvenBeforeTheCoordinatorPublishesItsTimeout()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        live.Active(id).DeletionDeadlineTick = Environment.TickCount64 - 1;
        Assert.Equal(LiveStatus.AWAITING_APPROVAL, live.Get(id).Status); Assert.False(live.Active(id).Cancellation.IsCancellationRequested);
        Assert.Throws<ConflictException>(() => live.DecideDeletion(id, Approve(proposal))); Unchanged();
        Assert.False(live.Active(id).ApprovalConsumed); Assert.False(live.Get(id).EffectCommitted);
        live.DecideDeletion(id, Approve(proposal, false)); Assert.Equal(LiveStatus.CANCELLED, live.Get(id).Status);
    }
    [Fact] public void WrongApprovalIdentityHashOrRevisionNeverDeletes()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        Assert.Throws<ValidationException>(() => live.DecideDeletion(id, Approve(proposal) with { ApprovalId = Guid.NewGuid() }));
        Assert.Throws<ValidationException>(() => live.DecideDeletion(id, Approve(proposal) with { Hash = "different" }));
        Assert.Throws<ValidationException>(() => live.DecideDeletion(id, Approve(proposal) with { Revision = proposal.Revision + 1 })); Unchanged();
    }
    [Fact] public void ReadOnlyOrMixedEffectPlansCannotPrepareDeletion()
    {
        var id = Start(); live.Active(id).Semantic = Plan() with { Intentions = [SemanticOperation.OPEN_PROMPT] };
        Assert.Throws<ValidationException>(() => live.ProposeDeletion(id, source));
        live.Active(id).Semantic = Plan() with { Intentions = [SemanticOperation.REQUEST_DELETE, SemanticOperation.CREATE_DRAFT] };
        Assert.Throws<ValidationException>(() => live.ProposeDeletion(id, source)); Unchanged();
    }
    [Fact] public void FieldDataDoesNotGrantDeletionEvenWhenItContainsTheEvidenceQuote()
    {
        var id = Start("Completa los campos. material: " + Request);
        Assert.Throws<ValidationException>(() => live.ProposeDeletion(id, source)); Unchanged();
    }
    [Fact] public void AnotherBoundSourceOrGoalCannotBeDeleted()
    {
        var id = Start(); Assert.Throws<ValidationException>(() => live.ProposeDeletion(id, other));
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = Plan(); var goal = TaskGoal.Create(Request, context, plan, [], [SemanticOperation.REQUEST_DELETE]);
        live.Active(id).Semantic = plan with { Goal = goal with { TargetReferences = [new(other.Id, other.Revision)] } };
        Assert.Throws<ValidationException>(() => live.ProposeDeletion(id, source)); Unchanged();
    }
    [Fact] public void PendingDeletionCannotBeReplacedWithASaveProposal()
    {
        var id = Start(); live.ProposeDeletion(id, source);
        Assert.Throws<ConflictException>(() => live.ProposeSave(id, Presented(id) with {
            Mode = "create", PromptId = source.Id, Revision = source.Revision, Title = source.Title, TemplateText = "Otra instrucción.", DraftChanged = true
        })); Unchanged();
    }
    [Fact] public void DeleteCompilerResolvesTheSourceWithoutOpeningOrChangingIt()
    {
        var plan = Plan(); Assert.Equal([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.REQUEST_DELETE], plan.Intentions);
        SemanticPlanValidator.Validate(plan, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, Request);
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(plan, "ui_prepare_update"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.AuthorizeTool(plan, "ui_commit_deletion"));
        Assert.Throws<PreparationToolException>(() => SemanticPlanValidator.Validate(plan with { DeletionEvidence = null }, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, Request));
    }
    [Fact] public async Task ConcurrentConfirmationsProduceOneEffectAndOneConsumedApproval()
    {
        var id = Start(); var proposal = live.ProposeDeletion(id, source); live.DeletionPresented(id, Presented(id));
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => {
            try { live.DecideDeletion(id, Approve(proposal)); return true; } catch (ConflictException) { return false; }
        })));
        Assert.Single(results, result => result); Assert.Single(live.Events(id, 0), item => item.Stage == "DeletionCommit");
        Assert.Throws<MissingPromptException>(() => library.Get(source.Id));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task VisibleWorkflowWaitsForTheHumanAndNeverAsksTheModelToCommit(bool confirm)
    {
        var id = Start(); var input = live.Active(id).Input;
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = Plan(); plan = plan with { Goal = TaskGoal.Create(Request, context, plan, [], [SemanticOperation.REQUEST_DELETE]) };
        var semantic = new FixedSemantic(plan); var unused = new UnusedInference();
        var retrieverStore = new LocalStore(Path.Combine(directory, "synthetic.sqlite")); var retriever = new SqlitePromptRetriever(retrieverStore);
        var sessions = new AgentSessions(); sessions.Put(new(input.SessionId, other, null, [], DateTime.UtcNow));
        var browser = new DeletionBrowser(live, id, Presented(id), confirm);
        var engine = new AgentOrchestrator(unused, retriever, unused, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(retrieverStore), new(), sessions: sessions, taskPlanner: new NovaTaskPlanner(semantic, retriever, sessions, semantic));
        await engine.RunVisible(input, "http://127.0.0.1:5241", live, unused, browser, library);
        Assert.Equal(confirm ? LiveStatus.COMPLETED : LiveStatus.CANCELLED, live.Get(id).Status);
        Assert.DoesNotContain("ui_open_prompt", browser.Tools); Assert.True(browser.Disposed);
        if (confirm) {
            Assert.Equal(new[] { "ui_review_deletion", "ui_enable_deletion_confirmation", "ui_verify_deletion" }, browser.Tools);
            Assert.Throws<MissingPromptException>(() => library.Get(source.Id)); Assert.Null(sessions.Get(input.SessionId));
        } else { Unchanged(); Assert.Equal(other.Id, sessions.Get(input.SessionId)?.Source?.Id); }
    }
    private sealed class UnusedInference : IIntentInterpreter, IPromptGenerator, IUiDecisionModel
    {
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new NotSupportedException();
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct) => throw new InvalidOperationException("Deletion must not delegate human confirmation to the model.");
    }
    private sealed class DeletionBrowser(LiveRuns live, Guid id, UiVisibleState review, bool confirm) : IVisibleBrowser, IVisibleBrowserFactory
    {
        public List<string> Tools = []; public bool Disposed;
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "") => Task.FromResult(review with { DeletionPromptId = null, DeletionRevision = null, DeletionRunId = null });
        public Task<UiVisibleState> Execute(string tool, JsonElement args, CancellationToken ct)
        {
            Tools.Add(tool);
            if (tool == "ui_enable_deletion_confirmation") {
                var proposal = live.View(id).Deletion!;
                live.DecideDeletion(id, new(proposal.Id, proposal.Hash, proposal.Revision, confirm));
                return Task.FromResult(review with { DeletionConfirmationReady = true });
            }
            return Task.FromResult(tool == "ui_verify_deletion" ? review with { Candidates = [], DeletionPromptId = null, DeletionRevision = null, DeletionRunId = null } : review);
        }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
