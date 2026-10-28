using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// These tests exercise app-bound proposal and approval contracts. Controlled
// approvals are not evidence that a real person accepted the content.
public sealed class AuthoringContentApprovalTests : IDisposable
{
    private const string Request = "Simplifica el texto. Solicita revisión del guardado.";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-authoring-approval-" + Guid.NewGuid());
    private readonly PromptLibrary library;
    private readonly LiveRuns live;
    private readonly PromptDocument source;

    public AuthoringContentApprovalTests()
    {
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
        var adapter = new NovaPromptStore(store);
        library = new(adapter, adapter);
        live = new(new LiveRunStore(store), library);
        source = library.Create(new("Fuente sintética", "Describe el resultado con detalle. No inventes datos."));
    }

    private DraftProposalContract Contract(string request = Request, Guid? sourceId = null, int? revision = null) =>
        new(request, new(sourceId ?? source.Id, revision ?? source.Revision, source.TemplateText,
            source.Title, source.Description, source.CategoryId, LiveRuns.Hash(source.TemplateText)),
            PromptInputContract.FromUserSelection(request, source.TemplateText, true, []));

    private (Guid Id, TaskGoal Goal, UiVisibleState State) Start(bool save = true, PreparedDraft? working = null, bool confirmed = false)
    {
        var id = Guid.NewGuid();
        live.Create(new(id, Guid.NewGuid(), Request));
        var plan = SemanticPlanCompiler.Compile(new(save
                ? [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.REQUEST_SAVE_VERSION]
                : [SemanticOperation.PROPOSE_ADAPTATION],
            TargetReference.SELECTED, source.Title, source.Id, "", "Simplifica el texto.",
            new([SemanticOperation.EXECUTE_LOCAL], false, null, null, [], [], false),
            SemanticOutcome.DRAFT, [], save, "", null, "Revisar un cambio."));
        var goal = TaskGoal.Create(Request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.CONTENT, "Simplifica el texto."), new(TaskClauseRole.WORKFLOW, "Solicita revisión del guardado.")]);
        if (working is not null) goal = goal.BindWorking(working);
        var contract = working is null ? Contract() : new DraftProposalContract(Request,
            new(source.Id, source.Revision, working.TemplateText, working.Title, working.Description,
                working.CategoryId, LiveRuns.Hash(source.TemplateText), working.Id, LiveRuns.Hash(working.TemplateText), source.TemplateText),
            PromptInputContract.FromUserSelection(Request, working.TemplateText, true, []));
        var description = confirmed ? "Descripción confirmada de la plantilla." : source.Description;
        if (confirmed) {
            var review = AuthoringBriefs.Propose(contract, new(source.Title, description, "Describe el caso de forma clara. No inventes datos.", [], [], [], []));
            contract.BindConfirmedBrief(AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true)));
        }
        goal.SetInputContract(contract.FieldContract);
        goal.BindAuthoringContract(contract);
        var body = contract.RegisterProposal(new(source.Title, "Describe el caso de forma clara. No inventes datos.", description)).TemplateText;
        var state = new UiVisibleState("create", [], source.Id, source.Revision, source.Title, body, [], [], true,
            false, null, null, false, false) { Description = description };
        live.Active(id).Semantic = plan with { Goal = goal };
        live.Active(id).Observation = state;
        live.Active(id).WorkingDraft = working;
        live.Active(id).GeneratedDraft = new DeterministicPromptEvaluator().Evaluate(new(id, Request),
            new(PreparationIntent.IMPROVE_PROMPT, "", ""),
            new(PreparationDecision.ADAPT_EXISTING, source.Id, source.Title, description, body, [], "", "Propuesta controlada."),
            [new(source, 10, "Origen guardado real")], []);
        return (id, goal, state);
    }

    [Fact]
    public void AManualApprovalRefreshCannotEraseConfirmedMetadataOrReplaceThePendingProposal()
    {
        var task = Start(confirmed: true);
        var original = Propose(task.Id, task.State);
        Assert.Equal("Descripción confirmada de la plantilla.", original.Draft.Description);
        Assert.Throws<ValidationException>(() => live.RefreshProposal(task.Id, original.Draft with { Description = "" }));
        Assert.Equal(original.Hash, live.View(task.Id).Proposal!.Hash);
        Assert.Equal(original.Draft.Description, live.View(task.Id).Proposal!.Draft.Description);
        Assert.Equal("PENDING", live.Get(task.Id).Approval); Assert.False(live.Get(task.Id).EffectCommitted);
        live.Approve(task.Id, Reviewed(original));
        Assert.Equal(original.Draft.Description, live.Commit(task.Id, original.Draft).Description);
    }

    private LiveApproval Propose(Guid id, UiVisibleState state)
    {
        live.ProposeSave(id, state);
        return live.View(id).Proposal!;
    }

    private static LiveApprovalInput Reviewed(LiveApproval proposal) => new(proposal.Id, proposal.Hash, proposal.Revision, true)
    {
        ContentReviewed = true,
        ContentReviewHash = proposal.ContentReview!.ContractHash,
        ApprovedLiteralChangeIds = proposal.ContentReview.LiteralChanges.Entries.Select(change => change.Id).ToArray()
    };

    [Fact]
    public void RegisteredProposalDoesNotCreateAModelOrHumanSemanticApproval()
    {
        var task = Start();
        Assert.True(task.Goal.HasAuthoringStructure(task.State.TemplateText!));
        Assert.False(task.Goal.HasContentAssessment(task.State.TemplateText!));
        Assert.False(task.Goal.HasHumanContentApproval(task.State.TemplateText!));
        Assert.Throws<ValidationException>(() => task.Goal.RecordContentAssessment(task.State.TemplateText!));

        var plan = live.Active(task.Id).Semantic!;
        GoalCompletion Check(bool allowPending, UiVisibleState state) => GoalCompletionValidator.Validate(task.Goal, plan, state,
            source, source, ["ui_prepare_update"], new Dictionary<string, string>(), true, false,
            allowPendingContentReview: allowPending);
        Assert.DoesNotContain("CONTENT_VALID", Check(true, task.State).Missing);
        Assert.Contains("CONTENT_VALID", Check(false, task.State).Missing);
        Assert.Contains("CONTENT_VALID", Check(true, task.State with { TemplateText = "Otro cuerpo." }).Missing);
        Assert.Null(live.View(task.Id).Proposal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EffectConfirmationCannotSilentlyApproveUnreviewedContent(bool reviewed, bool correctInventory)
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        var input = new LiveApprovalInput(proposal.Id, proposal.Hash, proposal.Revision, true)
        {
            ContentReviewed = reviewed,
            ContentReviewHash = correctInventory ? proposal.ContentReview!.ContractHash : "OTHER_INVENTORY"
        };
        Assert.Throws<ValidationException>(() => live.Approve(task.Id, input));
        Assert.Equal("PENDING", live.Get(task.Id).Approval);
        Assert.False(task.Goal.HasHumanContentApproval(proposal.Draft));
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
    }

    [Fact]
    public void ExactReviewExposesOriginalAuthorityAndPermitsOneMatchingCommit()
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        var review = proposal.ContentReview!;
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", review.Status);
        Assert.Equal(Request, review.OriginalRequest);
        Assert.Equal(source.TemplateText, review.Source.OriginalText);
        Assert.Equal(source.Id, review.Source.PromptId);
        Assert.Equal(source.Revision, review.Source.Revision);
        Assert.Equal(LiveRuns.Hash(source.TemplateText), review.Source.BodyHash);
        Assert.Equal(LiveRuns.Hash(proposal.Draft.TemplateText), review.BodyHash);
        Assert.Equal(task.Goal.AuthoringContract!.Fingerprint, review.ContractHash);
        Assert.DoesNotContain("contentAuthorityHash", JsonSerializer.Serialize(proposal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        live.Approve(task.Id, Reviewed(proposal));
        Assert.True(task.Goal.HasHumanContentApproval(proposal.Draft));
        Assert.False(task.Goal.HasContentAssessment(proposal.Draft.TemplateText));
        var saved = live.Commit(task.Id, proposal.Draft);
        Assert.Equal(proposal.Draft.TemplateText, saved.TemplateText);
        Assert.Equal(source.Revision + 1, saved.Revision);
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("category")]
    [InlineData("body")]
    public void ContentReceiptBindsTheWholeFormInsteadOfOnlyTheBody(string field)
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        live.Approve(task.Id, Reviewed(proposal));
        var changed = field switch
        {
            "title" => proposal.Draft with { Title = "Otro título" },
            "description" => proposal.Draft with { Description = "Otra descripción" },
            "category" => proposal.Draft with { CategoryId = 123 },
            _ => proposal.Draft with { TemplateText = proposal.Draft.TemplateText + " Cambio." }
        };
        Assert.False(task.Goal.HasHumanContentApproval(changed));
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, changed));
        Assert.False(live.Get(task.Id).EffectCommitted);
    }

    [Fact]
    public void EditingBeforeApprovalReplacesProposalHashAndRequiresReviewOfTheCurrentBody()
    {
        var task = Start();
        var prior = Propose(task.Id, task.State);
        var edited = prior.Draft with { TemplateText = "Texto corregido por quien revisa. No inventes datos." };
        live.RefreshProposal(task.Id, edited);
        var current = live.View(task.Id).Proposal!;
        Assert.NotEqual(prior.Hash, current.Hash);
        Assert.Equal(prior.ContentReview!.ContractHash, current.ContentReview!.ContractHash);
        Assert.NotEqual(prior.ContentReview.BodyHash, current.ContentReview.BodyHash);
        Assert.False(task.Goal.HasAuthoringStructure(edited.TemplateText));
        Assert.Throws<ValidationException>(() => live.Approve(task.Id, Reviewed(prior)));
        live.Approve(task.Id, Reviewed(current));
        Assert.True(task.Goal.HasHumanContentApproval(edited));
        Assert.False(task.Goal.HasHumanContentApproval(prior.Draft));
        Assert.Equal(edited.TemplateText, live.Commit(task.Id, edited).TemplateText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingFrozenFieldsIsRejectedWithoutChangingTheReviewedAuthority(bool approveFirst)
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        if (approveFirst) live.Approve(task.Id, Reviewed(proposal));
        var original = task.Goal.InputContract;
        Assert.Throws<ValidationException>(() => task.Goal.SetInputContract(
            PromptInputContract.FromUserSelection(Request, source.TemplateText, true, ["nuevo_campo"])));
        Assert.Same(original, task.Goal.InputContract);
        if (!approveFirst) live.Approve(task.Id, Reviewed(proposal));
        Assert.True(task.Goal.HasHumanContentApproval(proposal.Draft));
        Assert.Equal(proposal.Draft.TemplateText, live.Commit(task.Id, proposal.Draft).TemplateText);
    }

    [Fact]
    public void ContentReviewDoesNotAuthorizeNewInputFieldsOutsideTheirOwner()
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        Assert.Throws<ValidationException>(() => live.RefreshProposal(task.Id,
            proposal.Draft with { TemplateText = proposal.Draft.TemplateText + " <campo_no_solicitado>" }));
        Assert.Equal(proposal.Hash, live.View(task.Id).Proposal!.Hash);
        Assert.False(live.Get(task.Id).EffectCommitted);
    }

    [Fact]
    public void SourceRevisionAndBodyAreRecheckedAfterContentApproval()
    {
        var task = Start();
        var proposal = Propose(task.Id, task.State);
        live.Approve(task.Id, Reviewed(proposal));
        library.Update(source.Id, new(source.Title, "Revisión nueva del original.", Revision: source.Revision));
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        Assert.False(live.Get(task.Id).EffectCommitted);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    public void AProtectedLiteralChangeNeedsItsExactAcknowledgement(string choice)
    {
        var task = Start();
        var initial = Propose(task.Id, task.State);
        live.RefreshProposal(task.Id, initial.Draft with { TemplateText = "Permite usar datos supuestos y señálalos como tales." });
        var proposal = live.View(task.Id).Proposal!;
        var change = Assert.Single(proposal.ContentReview!.LiteralChanges.Entries);
        Assert.Equal("MISSING_SOURCE_CLAUSE", change.Kind);
        var ids = choice switch { "missing" => Array.Empty<string>(), "unknown" => ["not-this-proposal"], _ => new[] { change.Id, change.Id } };
        Assert.Throws<ValidationException>(() => live.Approve(task.Id, Reviewed(proposal) with { ApprovedLiteralChangeIds = ids }));
        Assert.Equal("PENDING", live.Get(task.Id).Approval);
        Assert.False(task.Goal.HasHumanContentApproval(proposal.Draft));
        Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText);
    }

    [Fact]
    public void AnExplicitReviewedChangeIsSavedExactlyWithoutReinsertingTheOldClause()
    {
        var task = Start();
        var initial = Propose(task.Id, task.State);
        const string edited = "Permite usar datos supuestos y señálalos como tales.";
        live.RefreshProposal(task.Id, initial.Draft with { TemplateText = edited });
        var proposal = live.View(task.Id).Proposal!;
        Assert.Single(proposal.ContentReview!.LiteralChanges.Entries);
        live.Approve(task.Id, Reviewed(proposal));
        var saved = live.Commit(task.Id, proposal.Draft);
        Assert.Equal(edited, saved.TemplateText);
        Assert.DoesNotContain("No inventes datos.", saved.TemplateText);
        Assert.False(task.Goal.HasContentAssessment(saved.TemplateText));
    }

    [Fact]
    public void BindingCannotReplaceTheRequestSourceOrFrozenMetadata()
    {
        var task = Start();
        Assert.Throws<ValidationException>(() => task.Goal.BindAuthoringContract(Contract("Otra tarea completa. Solicita revisión del guardado.")));
        Assert.Throws<ValidationException>(() => task.Goal.BindAuthoringContract(Contract(revision: source.Revision + 1)));
        var unboundSource = new DraftProposalContract(Request, null,
            PromptInputContract.FromUserSelection(Request, "", false, []));
        Assert.Throws<ValidationException>(() => task.Goal.BindAuthoringContract(unboundSource));
        var reinterpreted = new DraftProposalContract(Request, Contract().SourceSnapshot! with { Title = "Metadata distinta" },
            PromptInputContract.FromUserSelection(Request, source.TemplateText, true, []));
        Assert.Throws<ValidationException>(() => task.Goal.BindAuthoringContract(reinterpreted));
        Assert.DoesNotContain("authoringContract", JsonSerializer.Serialize(task.Goal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void DraftOnlyPreparationDoesNotAuthorizeSavingOrPretendContentWasApproved()
    {
        var task = Start(save: false);
        Assert.Throws<ValidationException>(() => live.ProposeSave(task.Id, task.State));
        Assert.Null(live.View(task.Id).Proposal);
        Assert.False(task.Goal.HasHumanContentApproval(task.State.TemplateText!));
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        live.ProposeContentReview(task.Id, task.State);
        var proposal = live.View(task.Id).Proposal!;
        Assert.Equal(LiveApprovalPurpose.CONTENT_ONLY, proposal.Purpose);
        live.Approve(task.Id, Reviewed(proposal));
        Assert.Equal("CONSUMED", live.Get(task.Id).Approval);
        Assert.True(task.Goal.HasHumanContentApproval(proposal.Draft));
        Assert.False(live.Get(task.Id).EffectCommitted);
        Assert.Throws<ValidationException>(() => live.Commit(task.Id, proposal.Draft));
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
    }

    [Fact]
    public void ContentReviewAndRequestedSaveHaveSeparateDecisionsAndRefreshKeepsTheSaveWaiter()
    {
        var task = Start();
        live.ProposeContentReview(task.Id, task.State);
        var content = live.View(task.Id).Proposal!;
        var contentDecision = live.Active(task.Id).ApprovalDecision.Task;
        Assert.Equal(LiveApprovalPurpose.CONTENT_ONLY, content.Purpose);
        Assert.Throws<ConflictException>(() => live.ProposeSave(task.Id, task.State));
        live.Approve(task.Id, Reviewed(content));
        Assert.True(contentDecision.IsCompletedSuccessfully);
        Assert.False(live.Get(task.Id).EffectCommitted);
        Assert.Throws<ValidationException>(() => live.Commit(task.Id, content.Draft));

        live.ProposeSave(task.Id, task.State);
        var save = live.View(task.Id).Proposal!;
        var saveDecision = live.Active(task.Id).ApprovalDecision.Task;
        Assert.Equal(LiveApprovalPurpose.SAVE, save.Purpose);
        Assert.NotSame(contentDecision, saveDecision);
        Assert.False(saveDecision.IsCompleted);
        Assert.Throws<ValidationException>(() => live.Approve(task.Id, Reviewed(content)));
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, save.Draft));

        live.RefreshProposal(task.Id, save.Draft with { TemplateText = "Revisión humana del contenido actual. No inventes datos." });
        Assert.Same(saveDecision, live.Active(task.Id).ApprovalDecision.Task);
        var refreshed = live.View(task.Id).Proposal!;
        live.Approve(task.Id, Reviewed(refreshed));
        Assert.True(saveDecision.IsCompletedSuccessfully);
        Assert.Equal(refreshed.Draft.TemplateText, live.Commit(task.Id, refreshed.Draft).TemplateText);
        Assert.True(live.Get(task.Id).EffectCommitted);
    }

    private PreparedDraft WorkingDraft(string body = "Describe el caso activo. No inventes datos.") =>
        new DeterministicPromptEvaluator().Evaluate(new(Guid.NewGuid(), Request), new(PreparationIntent.IMPROVE_PROMPT, "", ""),
            new(PreparationDecision.ADAPT_EXISTING, source.Id, source.Title, source.Description, body, [], "", "Borrador de trabajo controlado."),
            [new(source, 10, "Origen guardado real")], []);

    [Fact]
    public void WorkingContentReviewCreatesANewVersionOfTheRealSavedSource()
    {
        var working = WorkingDraft();
        var task = Start(working: working);
        var proposal = Propose(task.Id, task.State);
        Assert.Equal(source.Id, proposal.PromptId);
        Assert.Equal(source.Revision, proposal.Revision);
        Assert.Equal(source.TemplateText, proposal.OriginalText);
        Assert.Equal(source.Id, proposal.ContentReview!.Source.PromptId);
        Assert.Equal(source.Revision, proposal.ContentReview.Source.Revision);
        Assert.Equal(working.TemplateText, proposal.ContentReview.Source.OriginalText);
        Assert.Equal(source.TemplateText, proposal.ContentReview.Source.SavedOriginalText);
        Assert.Equal(working.Id, proposal.ContentReview.Source.WorkingDraftId);
        Assert.Equal(LiveRuns.Hash(working.TemplateText), task.Goal.WorkingTarget!.BodyHash);
        live.Approve(task.Id, Reviewed(proposal));
        var saved = live.Commit(task.Id, proposal.Draft);
        Assert.Equal(source.Id, saved.Id);
        Assert.Equal(source.Revision + 1, saved.Revision);
        Assert.Equal(proposal.Draft.TemplateText, saved.TemplateText);
        Assert.Equal("Describe el caso activo. No inventes datos.", working.TemplateText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AChangedWorkingRevisionInvalidatesItsPendingOrApprovedContentReceipt(bool approveFirst)
    {
        var task = Start(working: WorkingDraft());
        var proposal = Propose(task.Id, task.State);
        if (approveFirst) live.Approve(task.Id, Reviewed(proposal));
        var current = live.Active(task.Id).Semantic!;
        live.Active(task.Id).Semantic = current with { Goal = task.Goal.BindWorking(WorkingDraft("Otro texto de trabajo vigente."), revision: 2) };
        if (approveFirst) Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        else Assert.Throws<ConflictException>(() => live.Approve(task.Id, Reviewed(proposal)));
        Assert.False(live.Get(task.Id).EffectCommitted);
        Assert.Equal(source.Revision, library.Get(source.Id).Revision);
    }

    [Fact]
    public void AChangedSavedRevisionInvalidatesAReviewOfItsWorkingContent()
    {
        var task = Start(working: WorkingDraft());
        var proposal = Propose(task.Id, task.State);
        library.Update(source.Id, new(source.Title, "Otra versión guardada.", Revision: source.Revision));
        Assert.Throws<ConflictException>(() => live.Approve(task.Id, Reviewed(proposal)));
        Assert.False(live.Get(task.Id).EffectCommitted);
        Assert.Equal("PENDING", live.Get(task.Id).Approval);
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(directory);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("nova-authoring-approval-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected fixture path.");
        Directory.Delete(full, true);
    }
}
