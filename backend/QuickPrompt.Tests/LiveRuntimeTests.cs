using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LiveRuntimeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-runtime-" + Guid.NewGuid());
    private readonly LiveRuns live;
    private readonly LiveRunStore records;
    private readonly PromptLibrary library;
    public LiveRuntimeTests()
    {
        var store = new LocalStore(Path.Combine(directory, "library.sqlite"));
        var prompts = new NovaPromptStore(store); library = new(prompts, prompts);
        records = new(store); live = new(records, library);
    }
    private Guid Start()
    {
        var id = Guid.NewGuid(); live.Create(new(id, Guid.NewGuid(), "PRIVATE_REQUEST_SENTINEL")); return id;
    }
    [Fact] public void ProjectionUsesOnlyAllowlistedFactsAndActualEligibleTools()
    {
        var id = Start(); var control = live.Active(id);
        control.Observation = new("create", [], null, null, "PRIVATE_TITLE_SENTINEL", "PRIVATE_DRAFT_SENTINEL",
            new() { ["recipient"] = "PRIVATE_VALUE_SENTINEL" }, ["recipient", "topic"], true, false, null, null, false, false);
        control.Semantic = SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT], outcome: SemanticOutcome.NEW_DRAFT);
        control.Decisions.Add(new("ui_prepare_creation", JsonSerializer.SerializeToElement(new { reasoning = "PRIVATE_REASONING_SENTINEL" })));
        live.Emit(id, LiveStatus.INTERPRETING, LiveActor.MODEL_SELECTED, "PRIVATE_MESSAGE_SENTINEL", "SemanticPlan");
        live.PublishRuntimeTools(id, UiToolCatalog.All.Where(tool => tool.Name == "ui_prepare_creation"));
        var runtime = live.View(id).Runtime!;
        Assert.Equal(id, runtime.RunId); Assert.Equal(live.Get(id).Sequence, runtime.Sequence);
        Assert.Equal("Crear un prompt", runtime.Objective); Assert.True(runtime.ContextResolved);
        Assert.Equal("qwen3:8b", runtime.Model); Assert.Equal(1, runtime.Memory.ResolvedVariables); Assert.Equal(2, runtime.Memory.TotalVariables);
        Assert.True(runtime.Memory.DraftActive); Assert.False(runtime.Memory.SourceBound);
        Assert.Equal("ui_prepare_creation", Assert.Single(runtime.Tools).Id); Assert.Equal("available", runtime.Tools[0].State);
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(runtime));
        var count = live.Get(id).Sequence;
        live.PublishRuntimeTools(id, UiToolCatalog.All.Where(tool => tool.Name == "ui_prepare_creation"));
        Assert.Equal(count, live.Get(id).Sequence); // unchanged catalog creates no traffic
    }
    [Fact] public void ToolTransitionsPauseAndTerminalResultsComeFromReceipts()
    {
        var id = Start(); live.PublishRuntimeTools(id, UiToolCatalog.All.Where(tool => tool.Name == "ui_open_prompt"));
        live.Emit(id, LiveStatus.TOOL_PROPOSED, LiveActor.MODEL_SELECTED, "Selected", "Decision", "ui_open_prompt", duration: 125);
        Assert.Equal("selected", live.View(id).Runtime!.Tools[0].State);
        live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, "Running", tool: "ui_open_prompt");
        Assert.Equal("running", live.View(id).Runtime!.Tools[0].State);
        live.Pause(id); Assert.Equal("paused", live.View(id).Runtime!.Tools[0].State);
        live.Resume(id); live.Emit(id, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, "Observed", "Observation", "ui_open_prompt");
        var observed = live.View(id).Runtime!;
        Assert.Equal("completed", observed.Tools[0].State); Assert.Null(observed.ActiveTool); Assert.Equal(125, observed.LastInferenceMs);
        live.Emit(id, LiveStatus.COMPLETED, LiveActor.SYSTEM_VALIDATION, "Complete", "Completion");
        var completed = live.View(id).Runtime!;
        Assert.Null(completed.ActiveTool); Assert.Equal("ready", completed.ModelState);
        Assert.Equal("pending", completed.Flow.Single(step => step.Id == "interpret").State);
        Assert.Equal("completed", completed.Flow.Single(step => step.Id == "approval").State);
    }
    [Fact] public void UnknownToolsAndUnverifiedEffectsNeverBecomeCompletedCapabilities()
    {
        var id = Start(); live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, "Unsafe", tool: "PRIVATE_UNKNOWN_TOOL");
        Assert.Empty(live.View(id).Runtime!.Tools);
        live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, "Running", tool: "ui_open_prompt");
        live.Cancel(id);
        var runtime = live.View(id).Runtime!;
        Assert.Null(runtime.ActiveTool); Assert.Equal("unavailable", Assert.Single(runtime.Tools).State);
        Assert.Equal("stopped", runtime.Flow.Single(step => step.Id == "execute").State);
    }
    [Fact] public void VerifiedReceiptCompletesAnActionAndExplicitCommandDoesNotInventInference()
    {
        var id = Guid.NewGuid();
        live.Create(new(id, Guid.NewGuid(), "Crear instrucción", Command: new(SemanticOperation.CREATE_DRAFT, "instrucción")));
        live.Emit(id, LiveStatus.INTERPRETING, LiveActor.SYSTEM_VALIDATION, "Started", "SemanticInterpretation");
        live.Emit(id, LiveStatus.INTERPRETING, LiveActor.MODEL_SELECTED, "Compiled", "SemanticPlan");
        Assert.Null(live.View(id).Runtime!.LastInferenceMs);
        live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, "Running", tool: "ui_read_visible_state");
        live.Emit(id, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Receipt", "UiActionReceipt", "ui_read_visible_state");
        Assert.Equal("completed", Assert.Single(live.View(id).Runtime!.Tools).State);
        live.Emit(id, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, "Generated", "Observation", "ui_prepare_creation", duration: 2400);
        Assert.Equal(2400, live.View(id).Runtime!.LastInferenceMs);
    }
    [Fact] public void RestartPreservesObservedHistoryWithoutInventingAvailableToolsOrMemory()
    {
        var id = Start(); live.Emit(id, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, "Observed", "Observation", "ui_open_prompt");
        var recoveredRecords = new LiveRunStore(new LocalStore(Path.Combine(directory, "library.sqlite")));
        var recovered = new LiveRuns(recoveredRecords, library).View(id).Runtime!;
        Assert.True(recovered.RecoverableOnly); Assert.False(recovered.Memory.DraftActive); Assert.False(recovered.ContextResolved);
        Assert.Equal("completed", Assert.Single(recovered.Tools).State); Assert.Null(recovered.ActiveTool);
        Assert.Equal(LiveStatus.BLOCKED, recoveredRecords.Get(id)!.Status);
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
}
