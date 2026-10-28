using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class SameTabUiTests : IAsyncLifetime
{
    [Fact] public void TemporaryReferenceWorksWithoutTaskGoalButCannotReplaceAnotherDraftOrSession()
    {
        var request = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Adapta este borrador.", "SAME_TAB");
        var reference = new DraftRef(Guid.NewGuid(), request.SessionId, 1, null, "Explica <tema>.");
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.PROPOSE_ADAPTATION], TargetReference.ACTIVE_DRAFT);
        var state = State with { Mode = "create", TemplateText = reference.CurrentContent };
        var decision = new UiDecision("ui_load_active_draft", JsonSerializer.SerializeToElement(new { promptId = (Guid?)null }));
        UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request, plan, reference);
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request, plan));
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request, plan, reference with { SessionId = Guid.NewGuid() }));
        Assert.Throws<ConflictException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state with { TemplateText = "Cambio manual" }, request, plan, reference));
        Assert.Throws<ConflictException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state with { PromptId = Guid.NewGuid() }, request, plan, reference));
    }
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-same-tab-tests-" + Guid.NewGuid());
    private readonly LiveRuns live;
    private readonly PromptLibrary library;
    private readonly SameTabUiDispatcher dispatcher;
    private readonly string capability = Guid.NewGuid().ToString() + Guid.NewGuid();
    private readonly Guid id = Guid.NewGuid();
    private static readonly UiVisibleState State = new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false);
    public SameTabUiTests()
    {
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite")); var prompts = new NovaPromptStore(store);
        library = new PromptLibrary(prompts, prompts); live = new(new LiveRunStore(store), library); dispatcher = new(live);
        live.Create(new(id, Guid.NewGuid(), "Solicitud sintética", "SAME_TAB")); dispatcher.Bind(id, capability);
    }
    [Fact] public async Task DispatcherWaitsForExactVerifiedReceiptAndNeverLaunchesBrowser()
    {
        await using var driver = dispatcher.Factory().Create();
        var pending = driver.Start(id, "http://127.0.0.1:5190", false, CancellationToken.None);
        Assert.False(pending.IsCompleted);
        var first = dispatcher.Poll(id, capability, true).Action!;
        Assert.Equal("start", first.Tool); Assert.Equal(1, first.Sequence);
        Assert.Equal(first, dispatcher.Poll(id, capability, true).Action);
        Assert.Throws<ConflictException>(() => dispatcher.Poll(id, "another-document-capability", true));
        Assert.Throws<ConflictException>(() => dispatcher.Acknowledge(id, capability, new(Guid.NewGuid(), 1, "EXECUTED", "VERIFIED", State)));
        Assert.Throws<ConflictException>(() => dispatcher.Acknowledge(id, capability, new(first.ActionId, 2, "EXECUTED", "VERIFIED", State)));
        var receipt = new UiActionReceipt(first.ActionId, first.Sequence, "EXECUTED", "VERIFIED", State);
        dispatcher.Acknowledge(id, capability, receipt); Assert.Equal(State, await pending.WaitAsync(TimeSpan.FromSeconds(2)));
        dispatcher.Acknowledge(id, capability, receipt);
        Assert.Throws<ConflictException>(() => dispatcher.Acknowledge(id, capability, receipt with { VerificationStatus = "UNKNOWN" }));
        Assert.Null(dispatcher.Poll(id, capability, true).Action);
    }
    [Fact] public async Task HiddenTabKeepsTheSameActionAndOnlyExplicitPauseRequiresResume()
    {
        await using var driver = dispatcher.Factory().Create(); var pending = driver.Start(id, "", false, CancellationToken.None);
        var action = dispatcher.Poll(id, capability, true).Action!;
        Assert.Equal(action, dispatcher.Poll(id, capability, false).Action); Assert.False(live.Get(id).PauseRequested);
        live.Pause(id);
        Assert.Null(dispatcher.Poll(id, capability, false).Action); Assert.True(live.Get(id).PauseRequested);
        dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", State));
        await Task.Delay(30); Assert.False(pending.IsCompleted); live.Resume(id);
        Assert.Equal(State, await pending.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    [Theory] [InlineData("MANUAL_INTERVENTION")] [InlineData("UI_TARGET_AMBIGUOUS")]
    public async Task UnknownOutcomeBlocksWithoutRetryingAndConcurrentActionIsRejected(string failureCode)
    {
        await using var driver = dispatcher.Factory().Create(); var pending = driver.Start(id, "", false, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => driver.Execute("ui_read_visible_state", UiToolCatalog.NoArguments, CancellationToken.None));
        var action = dispatcher.Poll(id, capability, true).Action!;
        dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "INTERRUPTED", "UNKNOWN", null, failureCode));
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => pending); Assert.Equal(failureCode, error.Code);
        Assert.Null(dispatcher.Poll(id, capability, true).Action);
    }
    [Fact] public async Task CancelReleasesPendingActionAndRejectsLateReceipt()
    {
        await using var driver = dispatcher.Factory().Create();
        var pending = driver.Start(id, "", false, live.Active(id).Cancellation.Token); var action = dispatcher.Poll(id, capability, true).Action!;
        live.Cancel(id); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Throws<ConflictException>(() => dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", State)));
    }
    [Fact] public async Task AuthoredMetadataRequiresAnExactBoundReceiptBeforeTheFormCanBeAccepted()
    {
        await using var driver = dispatcher.Factory().Create(); var start = driver.Start(id, "", false, CancellationToken.None);
        var first = dispatcher.Poll(id, capability, true).Action!;
        dispatcher.Acknowledge(id, capability, new(first.ActionId, first.Sequence, "EXECUTED", "VERIFIED", State)); await start;
        var pending = driver.Execute("ui_prepare_creation", JsonSerializer.SerializeToElement(new {
            title = "Guía", templateText = "Explica el procedimiento.", description = "Descripción confirmada."
        }), CancellationToken.None);
        var action = dispatcher.Poll(id, capability, true).Action!;
        var form = State with { Mode = "create", Title = "Guía", TemplateText = "Explica el procedimiento.", DraftChanged = true };
        Assert.Throws<ValidationException>(() => dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", form)));
        Assert.False(pending.IsCompleted);
        Assert.Throws<ValidationException>(() => dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", form with { Description = new string('x', 2001) })));
        dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", form with { Description = "Descripción confirmada." }));
        Assert.Equal("Descripción confirmada.", (await pending).Description);
    }
    [Fact] public async Task ClosingDispatcherReleasesAnUnacknowledgedActionAndCanBeRepeated()
    {
        await using var driver = dispatcher.Factory().Create();
        var pending = driver.Start(id, "", false, CancellationToken.None);
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await dispatcher.DisposeAsync();
    }
    [Fact] public async Task ProgressAndReceiptRequireTheCurrentActionAndVerifiedLogicalTarget()
    {
        await using var driver = dispatcher.Factory().Create(); var start = driver.Start(id, "", false, CancellationToken.None);
        var first = dispatcher.Poll(id, capability, true).Action!;
        Assert.Throws<ConflictException>(() => dispatcher.Progress(id, capability, new(first.ActionId, 2, "EXECUTED")));
        dispatcher.Progress(id, capability, new(first.ActionId, 1, "TARGET_LOCATED"));
        dispatcher.Progress(id, capability, new(first.ActionId, 1, "TARGET_LOCATED"));
        dispatcher.Progress(id, capability, new(first.ActionId, 1, "EXECUTED"));
        Assert.Throws<ConflictException>(() => dispatcher.Progress(id, capability, new(first.ActionId, 1, "TARGET_LOCATED")));
        dispatcher.Acknowledge(id, capability, new(first.ActionId, 1, "EXECUTED", "VERIFIED", State)); await start;
        var target = Guid.NewGuid(); var open = driver.Execute("ui_open_prompt", JsonSerializer.SerializeToElement(new { promptId = target }), CancellationToken.None);
        var action = dispatcher.Poll(id, capability, true).Action!;
        var wrong = State with { Mode = "focus", PromptId = Guid.NewGuid(), Revision = 1 };
        Assert.Throws<ValidationException>(() => dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", wrong)));
        dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", wrong with { PromptId = target }));
        Assert.Equal(target, (await open).PromptId);
        Assert.Contains(live.Events(id, 0), item => item.ActionId == action.ActionId && item.ActionSequence == action.Sequence);
    }
    [Fact] public void SourceLessApprovalCannotBeForgedWithoutGeneratedAndVerifiedDraft()
    {
        Assert.Throws<ValidationException>(() => live.ProposeSave(id, State with { Mode = "create", Title = "Nuevo", TemplateText = "Contenido.", DraftChanged = true }));
        Assert.Throws<ConflictException>(() => live.Commit(id, new("Nuevo", "Contenido.")));
        Assert.DoesNotContain(capability, JsonSerializer.Serialize(live.Events(id, 0)));
    }
    [Fact] public void NewDraftApprovalRequiresExactContentAndIsConsumedOnce()
    {
        var generated = new DeterministicPromptEvaluator().Evaluate(new(id, "Crea una instrucción."), new(PreparationIntent.CREATE_PROMPT, "", ""),
            new(PreparationDecision.CREATE_NEW, null, "Nuevo", "", "Evalúa <documento>.", [], "", "Nueva instrucción solicitada."), [], []);
        live.Active(id).GeneratedDraft = generated;
        var state = State with { Mode = "create", Title = generated.Title, TemplateText = generated.TemplateText, DraftChanged = true, VariableNames = ["documento"], Values = new() { ["documento"] = "" } };
        live.ProposeSave(id, state); var proposal = live.View(id).Proposal!;
        Assert.Null(proposal.PromptId); Assert.Null(proposal.Revision);
        Assert.Throws<ConflictException>(() => live.Commit(id, proposal.Draft));
        live.Approve(id, new(proposal.Id, proposal.Hash, null, true));
        Assert.Throws<ConflictException>(() => live.Commit(id, proposal.Draft with { TemplateText = "Otro contenido." }));
        var saved = live.Commit(id, proposal.Draft); Assert.Equal(1, saved.Revision); Assert.Equal(generated.TemplateText, saved.TemplateText); Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(library.Get(saved.Id)));
        Assert.Throws<ConflictException>(() => live.Commit(id, proposal.Draft)); Assert.Equal(saved.Id, live.Get(id).TargetId);
    }
    [Fact] public async Task PollingGapDoesNotCreateAPauseOrReplaceAPendingAction()
    {
        await using var driver = dispatcher.Factory().Create(); var pending = driver.Start(id, "", false, CancellationToken.None);
        var first = dispatcher.Poll(id, capability, true).Action!;
        await Task.Delay(TimeSpan.FromSeconds(16));
        Assert.False(live.Get(id).PauseRequested);
        Assert.Equal(first, dispatcher.Poll(id, capability, false).Action);
        dispatcher.Acknowledge(id, capability, new(first.ActionId, first.Sequence, "EXECUTED", "VERIFIED", State));
        Assert.Equal(State, await pending.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    [Fact] public void UnsavedDraftVariableToolsUseNullIdentityAndStillRequireARealFieldAndLiteralAuthority()
    {
        var plan = new SemanticInterpretation([SemanticOperation.CREATE_DRAFT, SemanticOperation.FILL_VARIABLES], TargetReference.NONE, "", null, "", "Nueva instrucción",
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], [], false, "", null, "Solicitud validada.");
        var state = State with { Mode = "create", Title = "Nuevo", TemplateText = "Evalúa <proyecto>.", VariableNames = ["proyecto"], Values = new() { ["proyecto"] = "" } };
        var request = new LiveStartInput(id, Guid.NewGuid(), "proyecto: una aplicación React");
        var decision = new UiDecision("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = (Guid?)null, name = "proyecto", value = "una aplicación React", evidence = "una aplicación React" }));
        UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request, plan);
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state with { PromptId = Guid.NewGuid(), Revision = 1 }, request, plan));
        Assert.Throws<PreparationToolException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request, plan with { Intentions = [SemanticOperation.CREATE_DRAFT] }));
        Assert.Throws<ValidationException>(() => UiToolCatalog.Validate(decision, UiToolCatalog.All, state, request with { Message = "No aporté ese dato." }, plan));
    }
    [Fact] public async Task ArbitraryClientErrorTextCannotBecomePersistentActivityMetadata()
    {
        await using var driver = dispatcher.Factory().Create(); var pending = driver.Start(id, "", false, CancellationToken.None);
        var action = dispatcher.Poll(id, capability, true).Action!;
        dispatcher.Acknowledge(id, capability, new(action.ActionId, action.Sequence, "INTERRUPTED", "UNKNOWN", null, "PRIVATE_ERROR_SENTINEL"));
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => pending); Assert.Equal("UI_OUTCOME_UNKNOWN", error.Code);
        live.Emit(id, LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION, "Revisa el borrador.", error: error.Code);
        Assert.DoesNotContain("PRIVATE_ERROR_SENTINEL", JsonSerializer.Serialize(live.Events(id, 0)));
    }
    [Fact] public void RestartedServerReturnsOnlyBlockedMetadataWithoutReplayingOrTakingOverTheOldSurface()
    {
        var recovered = new LiveRuns(new LiveRunStore(new LocalStore(Path.Combine(directory, "synthetic.sqlite"))), library);
        var dispatcherAfterRestart = new SameTabUiDispatcher(recovered);
        var observed = dispatcherAfterRestart.Poll(id, capability, true);
        Assert.Equal(LiveStatus.BLOCKED, observed.Run.Status); Assert.Null(observed.Action);
        Assert.Throws<ConflictException>(() => dispatcherAfterRestart.Acknowledge(id, capability, new(Guid.NewGuid(), 1, "EXECUTED", "VERIFIED", State)));
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        live.Cancel(id);
        await dispatcher.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }
}
