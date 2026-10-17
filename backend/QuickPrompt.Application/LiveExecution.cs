using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveStatus { CREATED, INTERPRETING, TOOL_PROPOSED, TOOL_APPROVED, TOOL_RUNNING, UI_UPDATED, VERIFYING, AWAITING_APPROVAL, PAUSED, COMPLETED, FAILED, CANCELLED, BLOCKED }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveActor { MODEL_SELECTED, RULE_SELECTED, USER_SELECTED, SYSTEM_VALIDATION }
public record LiveEvent(Guid RunId, Guid EventId, long Sequence, DateTime Timestamp, string Stage, LiveActor Actor,
    string? ToolName, Guid? TargetId, LiveStatus Status, double? DurationMs, string? ErrorCode, string SafeDisplayMessage, Guid? ActionId = null, long? ActionSequence = null);
public record LiveRecord(Guid RunId, Guid SessionId, string Fingerprint, string Mode, LiveStatus Status, long Sequence,
    DateTime UpdatedAt, Guid? TargetId = null, int? Revision = null, bool PauseRequested = false, bool BrowserOpen = false,
    string? ErrorCode = null, string Approval = "NONE", bool EffectCommitted = false, string Model = "qwen3:8b", string ContractVersion = "nova-same-tab-1.0");
public record LiveStartInput(Guid RunId, Guid SessionId, string Message, string Mode = "SAME_TAB", Guid? SourceId = null,
    int? SourceRevision = null, Dictionary<string, string>? Values = null, string Model = "qwen3:8b")
{
    [JsonIgnore] public string? AuthorizedPendingRequest { get; init; }
    [JsonIgnore] public string DisplayRequest => AuthorizedPendingRequest is null || AuthorizedPendingRequest == Message ? Message : AuthorizedPendingRequest + "\n\nTu aclaración: " + Message;
    [JsonIgnore] public string TaskRequest => new PrepareInput(RunId, Message) { AuthorizedPendingRequest = AuthorizedPendingRequest }.TaskRequest;
}
public record LiveApproval(Guid Id, string Hash, Guid? PromptId, int? Revision, PromptInput Draft, string OriginalText);
public record LiveApprovalInput(Guid ApprovalId, string Hash, int? Revision, bool Confirmed);
public record LiveView(LiveRecord Run, string? Request, string? Question, UiVisibleState? Observation, LiveApproval? Proposal, UiDecision[] Decisions, SemanticInterpretation? Semantic = null);
public interface ILiveRunStore
{
    bool TryCreate(LiveRecord record);
    LiveRecord? Get(Guid id);
    LiveEvent[] Events(Guid id, long after);
    LiveEvent Append(LiveRecord record, LiveEvent item);
}
public record UiPrompt(Guid Id, string Title, int Revision);
public record UiVisibleState(string Mode, UiPrompt[] Candidates, Guid? PromptId, int? Revision, string? Title,
    string? TemplateText, Dictionary<string, string> Values, string[] VariableNames, bool DraftChanged,
    bool CompiledReady, string? CompiledText, string? Response, bool ResponseComplete, bool ProviderOpen, string? Error = null, string? ProviderId = null);
public record UiDecision(string ToolName, JsonElement Arguments, string SelectionFormat = "tool_call");
public record UiDecisionContext(string Request, UiVisibleState State, string[] ExecutedTools, UiToolContract[] Tools, Guid? RequestedSourceId, string? Feedback, Dictionary<string, string>? AuthorizedValues = null,
    string? LastResult = null, string? RequestedSourceTitle = null, Action<string>? ModelEvent = null,
    bool RequiresLocalExecution = false, bool RequiresUpdate = false, Action<UiDecision>? RejectedArguments = null, SemanticInterpretation? Semantic = null);
public record UiToolContract(string Name, string Description, JsonElement ArgumentsSchema, string Permission = "READ_OR_PREPARE", int TimeoutSeconds = 120);
public interface IUiDecisionModel { Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct); }
public interface IVisibleBrowser : IAsyncDisposable
{
    Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "");
    Task<UiVisibleState> Execute(string tool, JsonElement arguments, CancellationToken ct);
    Task Focus(CancellationToken ct);
}
public interface IVisibleBrowserFactory { IVisibleBrowser Create(Action<string>? disconnected = null); }

// Observable lifetime/control only. Business operations remain in the existing library/compiler/editor.
public sealed class LiveRuns(ILiveRunStore store, PromptLibrary library)
{
    public sealed class Control(LiveStartInput input)
    {
        public readonly object Gate = new();
        public readonly CancellationTokenSource Cancellation = new();
        public TaskCompletionSource Resumed = NewSignal();
        public TaskCompletionSource Changed = NewSignal();
        public TaskCompletionSource<bool> ApprovalDecision = NewDecision();
        public LiveStartInput Input = input;
        public UiVisibleState? Observation;
        public SemanticInterpretation? Semantic;
        public PreparedDraft? WorkingDraft;
        public PreparedDraft? GeneratedDraft;
        public string? AuthorizedTaskRequest;
        public string? VisibleTaskRequest;
        public LiveApproval? Proposal;
        public string? Question;
        public List<UiDecision> Decisions = [];
        public IVisibleBrowser? Browser;
        public bool ApprovalConsumed;
        public bool Started;
        public DateTime Touched = DateTime.UtcNow;
    }
    private readonly ConcurrentDictionary<Guid, Control> controls = new();
    private readonly object creationGate = new();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<bool> NewDecision() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static bool Terminal(LiveStatus state) => state is LiveStatus.COMPLETED or LiveStatus.CANCELLED or LiveStatus.FAILED or LiveStatus.BLOCKED;
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public LiveRecord Get(Guid id) => store.Get(id) ?? throw new MissingPromptException();
    public LiveEvent[] Events(Guid id, long after) { Get(id); return store.Events(id, after); }
    public LiveRecord Create(LiveStartInput input)
    {
        lock (creationGate) return CreateInside(input);
    }
    private LiveRecord CreateInside(LiveStartInput input)
    {
        if (input.RunId == Guid.Empty || input.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 16000
            || input.Mode is not ("BROWSER" or "LIVE" or "SAME_TAB") || input.Values?.Count > 60 || input.Values?.Any(v => v.Key.Length > 80 || v.Value is null || v.Value.Length > 8000) == true)
            throw new ValidationException("Revisa la solicitud de ejecución visible.");
        if ((input.SourceId is null) != (input.SourceRevision is null) || input.SourceRevision is < 1) throw new ValidationException("El contexto requiere un origen y su revisión vigente.");
        if (input.Model is not ("qwen3:8b" or "qwen2.5:7b-instruct") || input.Mode is "BROWSER" or "SAME_TAB" && input.Model != "qwen3:8b") throw new ValidationException("Modelo no permitido para este modo.");
        var fingerprint = Hash(input with { Values = input.Values?.OrderBy(v => v.Key).ToDictionary(v => v.Key, v => v.Value) });
        var prior = store.Get(input.RunId);
        if (prior is not null) { if (prior.Fingerprint != fingerprint) throw new ConflictException("RunId ya utilizado."); return prior; }
        foreach (var removed in controls.Where(c => store.Get(c.Key) is null).ToArray()) controls.TryRemove(removed.Key, out _);
        if (controls.Count(c => store.Get(c.Key) is { } run && !Terminal(run.Status)) >= 1) throw new ConflictException("Detén o termina la ejecución actual antes de iniciar otra.");
        foreach (var old in controls.Where(c => Terminal(Get(c.Key).Status) && c.Value.Touched < DateTime.UtcNow.AddHours(-2)).ToArray()) controls.TryRemove(old.Key, out _);
        if (controls.Count >= 100) foreach (var old in controls.Where(c => Terminal(Get(c.Key).Status)).OrderBy(c => c.Value.Touched).Take(controls.Count - 99).ToArray()) controls.TryRemove(old.Key, out _);
        var record = new LiveRecord(input.RunId, input.SessionId, fingerprint, input.Mode, LiveStatus.CREATED, 0, DateTime.UtcNow, Model: input.Model);
        if (!store.TryCreate(record)) throw new ConflictException("Esta ejecución ya se inició; no se repetirá.");
        controls[input.RunId] = new(input);
        Emit(input.RunId, LiveStatus.CREATED, LiveActor.USER_SELECTED, "Solicitud recibida."); return Get(input.RunId);
    }
    public Control Active(Guid id) => controls.TryGetValue(id, out var control) ? control : throw new ConflictException("La ejecución ya no está disponible. Puedes revisar sus eventos, sin repetir acciones.");
    public bool TryLaunch(Guid id) { var control = Active(id); lock (control.Gate) { if (control.Started || Terminal(Get(id).Status)) return false; control.Started = true; return true; } }
    public LiveView View(Guid id)
    {
        var record = Get(id);
        if (!controls.TryGetValue(id, out var control)) return new(record, null, null, null, null, []);
        if (Terminal(record.Status) && control.Touched < DateTime.UtcNow.AddHours(-2)) { controls.TryRemove(id, out _); return new(record, null, null, null, null, []); }
        lock (control.Gate) return new(record, control.VisibleTaskRequest ?? control.Input.Message, control.Question, control.Observation, control.Proposal, control.Decisions.ToArray(), control.Semantic);
    }
    public LiveEvent Emit(Guid id, LiveStatus status, LiveActor actor, string message, string stage = "Execution", string? tool = null,
        Guid? target = null, double? duration = null, string? error = null, Func<LiveRecord, LiveRecord>? update = null, Guid? actionId = null, long? actionSequence = null)
    {
        var control = Active(id);
        lock (control.Gate)
        {
            var record = Get(id);
            var uiReceipt = Terminal(record.Status) && status == LiveStatus.UI_UPDATED && stage == "UiReceipt";
            if (Terminal(record.Status) && status != record.Status && !uiReceipt) return store.Events(id, Math.Max(0, record.Sequence - 1)).Last();
            if (update is not null) record = update(record);
            record = record with { Status = uiReceipt ? record.Status : record.PauseRequested && !Terminal(status) ? LiveStatus.PAUSED : status, UpdatedAt = DateTime.UtcNow, ErrorCode = error ?? record.ErrorCode };
            var item = store.Append(record, new(id, Guid.NewGuid(), 0, DateTime.UtcNow, stage, actor, tool, target, status, duration, error, message, actionId, actionSequence));
            var changed = control.Changed; control.Changed = NewSignal(); changed.TrySetResult(); control.Touched = DateTime.UtcNow; return item;
        }
    }
    public async Task Boundary(Guid id, CancellationToken ct)
    {
        var control = Active(id);
        while (true)
        {
            Task pending;
            lock (control.Gate) { ct.ThrowIfCancellationRequested(); control.Cancellation.Token.ThrowIfCancellationRequested(); if (!Get(id).PauseRequested) return; pending = control.Resumed.Task; }
            await pending.WaitAsync(ct);
        }
    }
    public Task Changed(Guid id)
    { if (!controls.TryGetValue(id, out var control)) return Task.Delay(TimeSpan.FromSeconds(15)); lock (control.Gate) return control.Changed.Task; }
    public void Pause(Guid id, string? reason = null)
    {
        var control = Active(id); lock (control.Gate)
        { if (Terminal(Get(id).Status) || Get(id).PauseRequested) return; Emit(id, LiveStatus.PAUSED, LiveActor.USER_SELECTED, reason ?? "En pausa. No comenzarán nuevas acciones de interfaz.", update: r => r with { PauseRequested = true }); }
    }
    public void Resume(Guid id)
    {
        var control = Active(id); lock (control.Gate)
        {
            var run = Get(id); if (!run.PauseRequested || Terminal(run.Status)) return;
            var target = run.TargetId ?? control.Input.SourceId; var expectedRevision = run.Revision ?? control.Input.SourceRevision;
            var stale = false;
            try { stale = target is { } selected && library.Get(selected).Revision != expectedRevision; } catch (MissingPromptException) { stale = true; }
            if (stale) { Emit(id, LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION, "El prompt cambió durante la pausa. Inicia una nueva revisión.", error: "STALE_REVISION"); control.Cancellation.Cancel(); control.Resumed.TrySetResult(); return; }
            Emit(id, control.Proposal is not null ? LiveStatus.AWAITING_APPROVAL : LiveStatus.INTERPRETING, LiveActor.USER_SELECTED, "Continuando desde el último paso verificado.", update: r => r with { PauseRequested = false });
            var resumed = control.Resumed; control.Resumed = NewSignal(); resumed.TrySetResult();
        }
    }
    public void Cancel(Guid id)
    {
        var control = Active(id); lock (control.Gate)
        {
            var run = Get(id); if (Terminal(run.Status)) return;
            Emit(id, LiveStatus.CANCELLED, LiveActor.USER_SELECTED, run.EffectCommitted ? "Detenido. La versión ya confirmada se conserva." : "Detenido. No se aplicarán cambios pendientes.", error: "USER_CANCELLED");
            control.Cancellation.Cancel(); control.Resumed.TrySetResult(); control.ApprovalDecision.TrySetResult(false);
        }
    }
    public void ProposeSave(Guid id, UiVisibleState state, PromptInput? manualDraft = null)
    {
        var control = Active(id); lock (control.Gate)
        {
            if (state.PromptId is null) {
                var generated = control.GeneratedDraft ?? throw new ValidationException("Falta un borrador generado y verificado.");
                if (state.Mode != "create" || manualDraft is null && (state.Title != generated.Title || state.TemplateText != generated.TemplateText)) throw new ConflictException("El formulario no coincide con el borrador generado.");
                var created = manualDraft ?? new PromptInput(state.Title ?? "", state.TemplateText ?? "", "", 0);
                if (created.Defaults is not null || created.Revision != 0) throw new ValidationException("El borrador nuevo no puede cambiar valores por defecto ni una revisión guardada.");
                new Template { Title = created.Title, Template = created.TemplateText, Description = created.Description }.Validate();
                var proposalIdNew = Guid.NewGuid();
                control.Proposal = new(proposalIdNew, Hash(new { id, proposalIdNew, created }), null, null, created, "");
                Emit(id, LiveStatus.AWAITING_APPROVAL, LiveActor.SYSTEM_VALIDATION, "Borrador nuevo preparado. Revisa antes de guardarlo.", "Approval", update: r => r with { Approval = "PENDING" }); return;
            }
            var source = library.Get(state.PromptId.Value);
            if (source.Revision != state.Revision || !state.DraftChanged || string.IsNullOrWhiteSpace(state.TemplateText)) throw new ConflictException("Revisa la propuesta y su origen vigente.");
            var draft = manualDraft ?? new PromptInput(state.Title ?? source.Title, state.TemplateText, source.Description, source.CategoryId, source.Revision);
            if (draft.Revision != source.Revision || draft.Defaults is not null) throw new ValidationException("La propuesta no puede cambiar valores por defecto ni otra revisión.");
            new Template { Title = draft.Title, Template = draft.TemplateText, Description = draft.Description }.Validate();
            var proposalId = Guid.NewGuid(); var hash = Hash(new { runId = id, proposalId, source.Id, source.Revision, draft });
            control.Proposal = new(proposalId, hash, source.Id, source.Revision, draft, source.TemplateText);
            Emit(id, LiveStatus.AWAITING_APPROVAL, LiveActor.SYSTEM_VALIDATION, "Borrador preparado. Revisa la propuesta antes de guardar una versión.", stage: "Approval", target: source.Id,
                update: r => r with { TargetId = source.Id, Revision = source.Revision, Approval = "PENDING" });
        }
    }
    public void RefreshProposal(Guid id, PromptInput draft)
    {
        var control = Active(id); lock (control.Gate)
        {
            var run = Get(id); var old = control.Proposal ?? throw new ConflictException("Espera a que NOVA prepare la propuesta antes de revisarla.");
            if (run.Status != LiveStatus.AWAITING_APPROVAL || run.Approval != "PENDING" || control.ApprovalConsumed || draft.Revision != (old.Revision ?? 0)) throw new ConflictException("Esta propuesta ya no admite cambios. Cancela e inicia una nueva revisión.");
            var state = control.Observation ?? throw new ConflictException("No hay un editor verificado.");
            state = state with { Title = draft.Title, TemplateText = draft.TemplateText, DraftChanged = true, CompiledReady = false, CompiledText = null };
            ProposeSave(id, state, draft); control.Observation = state;
            Emit(id, LiveStatus.AWAITING_APPROVAL, LiveActor.USER_SELECTED, "Propuesta actualizada con tus cambios. La confirmación anterior ya no es válida.", "Approval");
        }
    }
    public void Approve(Guid id, LiveApprovalInput input)
    {
        var control = Active(id); lock (control.Gate)
        {
            var proposal = control.Proposal ?? throw new ConflictException("No hay una propuesta pendiente."); var run = Get(id);
            if (!input.Confirmed || input.ApprovalId != proposal.Id || input.Hash != proposal.Hash || input.Revision != proposal.Revision) throw new ValidationException("La confirmación no corresponde a esta propuesta.");
            if (run.Status != LiveStatus.AWAITING_APPROVAL || run.PauseRequested || control.ApprovalConsumed || run.Approval != "PENDING") throw new ConflictException("La propuesta no está disponible para aprobación.");
            if (proposal.PromptId is { } existing && library.Get(existing).Revision != proposal.Revision) throw new ConflictException("El prompt cambió; revisa la nueva versión.");
            Emit(id, LiveStatus.TOOL_APPROVED, LiveActor.USER_SELECTED, "Nueva versión aprobada para esta propuesta y revisión.", "Approval", target: proposal.PromptId, update: r => r with { Approval = "APPROVED" });
            control.ApprovalDecision.TrySetResult(true);
        }
    }
    public PromptDocument Commit(Guid id, PromptInput draft)
    {
        var control = Active(id); lock (control.Gate)
        {
            var proposal = control.Proposal ?? throw new ConflictException("No hay una propuesta aprobada."); var run = Get(id);
            if (Terminal(run.Status) || run.PauseRequested || run.Approval != "APPROVED" || control.ApprovalConsumed || Hash(draft) != Hash(proposal.Draft)) throw new ConflictException("Esta acción no tiene una aprobación vigente para su contenido.");
            control.Cancellation.Token.ThrowIfCancellationRequested(); control.ApprovalConsumed = true;
            var saved = proposal.PromptId is { } existing ? library.Update(existing, proposal.Draft) : library.Create(proposal.Draft);
            Emit(id, LiveStatus.UI_UPDATED, LiveActor.USER_SELECTED, "Versión confirmada y persistida; historial conservado.", "Approval", target: saved.Id,
                update: r => r with { Approval = "CONSUMED", EffectCommitted = true, TargetId = saved.Id, Revision = saved.Revision }); return saved;
        }
    }
}
