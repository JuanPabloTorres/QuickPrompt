using QuickPrompt.Local;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class TaskGoalApprovalTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-goal-approval-" + Guid.NewGuid());
    private readonly PromptLibrary library;
    private readonly LiveRuns live;
    private readonly PromptDocument source;
    public TaskGoalApprovalTests() {
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite")); var adapter = new NovaPromptStore(store);
        library = new(adapter, adapter); live = new(new LiveRunStore(store), library);
        source = library.Create(new("Fuente sintética", "Describe el resultado con detalle. No inventes datos."));
    }
    private (Guid Id, UiVisibleState State) Start(SemanticOperation? persistence, bool withGoal = true) {
        var id = Guid.NewGuid(); const string request = "Simplifica el texto. Solicita revisión del guardado.";
        live.Create(new(id, Guid.NewGuid(), request));
        var constraints = new SemanticConstraints(persistence == SemanticOperation.REQUEST_SAVE_VERSION ? [SemanticOperation.EXECUTE_LOCAL] : [SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.EXECUTE_LOCAL], persistence != SemanticOperation.REQUEST_SAVE_VERSION, null, null, [], [], false);
        var plan = SemanticPlanCompiler.Compile(new(persistence is { } effect ? [SemanticOperation.PROPOSE_ADAPTATION, effect] : [SemanticOperation.PROPOSE_ADAPTATION],
            TargetReference.SELECTED, source.Title, source.Id, "", "Simplifica el texto.", constraints, SemanticOutcome.DRAFT, [], persistence is not null, "", null, "Revisar un cambio."));
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.CONTENT, "Simplifica el texto."), new(TaskClauseRole.WORKFLOW, "Solicita revisión del guardado.")]) };
        live.Active(id).Semantic = withGoal ? plan : plan with { Goal = null };
        return (id, new("create", [], source.Id, source.Revision, source.Title, "Resume. No inventes datos.", [], [], true, true, "Resume. No inventes datos.", null, false, false));
    }
    [Fact] public void DraftOnlyGoalCannotBeTurnedIntoAPersistenceProposal() {
        var task = Start(null); Assert.Throws<ValidationException>(() => live.ProposeSave(task.Id, task.State));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(library.Get(source.Id))); Assert.Null(live.View(task.Id).Proposal);
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void ExplicitCopyReviewCreatesANewIdentityAfterApprovalAndProtectsTheSource(bool withGoal) {
        var task = Start(SemanticOperation.REQUEST_SAVE_COPY, withGoal); live.ProposeSave(task.Id, task.State); var proposal = live.View(task.Id).Proposal!;
        Assert.Null(proposal.PromptId); Assert.Null(proposal.Revision); Assert.Equal(0, proposal.Draft.Revision);
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        live.Approve(task.Id, new(proposal.Id, proposal.Hash, null, true));
        var saved = live.Commit(task.Id, proposal.Draft); Assert.NotEqual(source.Id, saved.Id); Assert.Equal(1, saved.Revision);
        Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText); Assert.Equal(source.Revision, library.Get(source.Id).Revision);
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void AChangedSourceInvalidatesAnApprovedCopyAndCancellationPreventsAnyCommit(bool withGoal) {
        var task = Start(SemanticOperation.REQUEST_SAVE_COPY, withGoal); live.ProposeSave(task.Id, task.State); var proposal = live.View(task.Id).Proposal!;
        live.Approve(task.Id, new(proposal.Id, proposal.Hash, null, true));
        library.Update(source.Id, new(source.Title, "Nueva revisión manual. No inventes datos.", Revision: source.Revision));
        Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        live.Cancel(task.Id); Assert.Throws<ConflictException>(() => live.Commit(task.Id, proposal.Draft));
        Assert.False(live.Get(task.Id).EffectCommitted);
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void ChangedSourceAlsoRejectsCopyBeforeApproval(bool withGoal) {
        var task = Start(SemanticOperation.REQUEST_SAVE_COPY, withGoal); live.ProposeSave(task.Id, task.State); var proposal = live.View(task.Id).Proposal!;
        Assert.DoesNotContain("sourceBasis", JsonSerializer.Serialize(proposal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        library.Update(source.Id, new(source.Title, "Revisión posterior del origen.", Revision: source.Revision));
        Assert.Throws<ConflictException>(() => live.Approve(task.Id, new(proposal.Id, proposal.Hash, null, true)));
        Assert.Equal("PENDING", live.Get(task.Id).Approval); Assert.False(live.Get(task.Id).EffectCommitted);
    }
    public void Dispose() {
        var full = Path.GetFullPath(directory); var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("nova-goal-approval-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected fixture path.");
        Directory.Delete(full, true);
    }
}
