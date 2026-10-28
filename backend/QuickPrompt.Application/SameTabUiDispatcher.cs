using System.Collections.Concurrent;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record UiActionEnvelope(Guid RunId, Guid ActionId, long Sequence, string Tool, Guid? Target,
    JsonElement Arguments, string ExecutionStatus, string VerificationStatus, DateTime Timestamp);
public record UiActionReceipt(Guid ActionId, long Sequence, string ExecutionStatus, string VerificationStatus,
    UiVisibleState? Observation, string? ErrorCode = null);
public record UiActionPoll(LiveRecord Run, UiActionEnvelope? Action);
public record UiActionProgress(Guid ActionId, long Sequence, string ExecutionStatus);

// An ephemeral capability belongs to the requesting React document. No browser process or persistence here.
public sealed class SameTabUiDispatcher(LiveRuns live) : IAsyncDisposable
{
    private static readonly HashSet<string> FailureCodes = new(StringComparer.Ordinal) { "UI_CANCELLED", "MANUAL_INTERVENTION", "UI_TARGET_TIMEOUT", "UI_TARGET_CHANGED", "UI_TARGET_AMBIGUOUS", "UI_VALUE_MISMATCH", "UI_FIELD_UNAVAILABLE", "UI_CONTROL_DISABLED", "UI_CONCURRENT_WRITE", "UI_IDENTITY_MISMATCH", "UI_PRECONDITION", "UI_TARGET_NOT_FOUND", "UI_EXISTING_DRAFT", "UNKNOWN_VARIABLE", "LOCAL_EXECUTION_NOT_ALLOWED", "MISSING_INPUTS", "UI_WORKING_DRAFT_MISMATCH", "UI_REPORTED_ERROR", "UI_SEQUENCE_GAP", "PROVIDER_UNAVAILABLE", "UI_PROVIDER_MISMATCH", "UNKNOWN_UI_TOOL" };
    private sealed class Surface(string capability)
    {
        public readonly object Gate = new();
        public readonly string CapabilityHash = LiveRuns.Hash(capability);
        public long Sequence;
        public UiActionEnvelope? Pending;
        public TaskCompletionSource<UiVisibleState>? Completion;
        public Guid? LastAck;
        public string? LastReceiptHash;
    }
    private readonly ConcurrentDictionary<Guid, Surface> surfaces = new();
    public void Bind(Guid id, string capability)
    {
        if (capability.Length is < 32 or > 128) throw new ValidationException("Falta la identidad de la pestaña emisora.");
        if (surfaces.TryGetValue(id, out var prior)) { Authorize(prior, capability); return; }
        foreach (var old in surfaces.Where(item => LiveRuns.Terminal(live.Get(item.Key).Status)).ToArray()) surfaces.TryRemove(old.Key, out _);
        var surface = new Surface(capability);
        if (!surfaces.TryAdd(id, surface)) throw new ConflictException("La pestaña ya está vinculada.");
    }
    public ValueTask DisposeAsync()
    {
        foreach (var surface in surfaces.Values) lock (surface.Gate) surface.Completion?.TrySetCanceled();
        surfaces.Clear();
        return ValueTask.CompletedTask;
    }

    private static void Authorize(Surface surface, string capability)
    { if (surface.CapabilityHash != LiveRuns.Hash(capability)) throw new ConflictException("Esta pestaña no es la superficie autorizada."); }
    private Surface Get(Guid id, string capability)
    { if (!surfaces.TryGetValue(id, out var surface)) throw new ConflictException("La conexión del lienzo expiró; detén la ejecución e inicia otra."); Authorize(surface, capability); return surface; }
    public void RequireSurface(Guid id, string capability) => _ = Get(id, capability);
    public UiActionPoll Poll(Guid id, string capability, bool visible)
    {
        // After a server restart only persisted terminal metadata exists. Return no action,
        // so the original document releases its stale lease instead of waiting forever.
        if (!surfaces.ContainsKey(id) && LiveRuns.Terminal(live.Get(id).Status)) return new(live.Get(id), null);
        var surface = Get(id, capability);
        lock (surface.Gate) {
            // Visibility is telemetry, not authorization to pause. A missing poll
            // naturally holds the same pending action; no unknown outcome is replayed.
            return new(live.Get(id), !live.Get(id).PauseRequested && !LiveRuns.Terminal(live.Get(id).Status) ? surface.Pending : null);
        }
    }
    public void Acknowledge(Guid id, string capability, UiActionReceipt receipt)
    {
        var surface = Get(id, capability);
        lock (surface.Gate) {
            var hash = LiveRuns.Hash(receipt);
            if (surface.LastAck == receipt.ActionId) { if (surface.LastReceiptHash != hash) throw new ConflictException("Confirmación duplicada incompatible."); return; }
            if (surface.Pending is not { } action || action.ActionId != receipt.ActionId || action.Sequence != receipt.Sequence || LiveRuns.Terminal(live.Get(id).Status))
                throw new ConflictException("Confirmación fuera de secuencia o ejecución detenida.");
            if (receipt.Observation is { } observed) ValidateObservation(action, observed);
            surface.LastAck = receipt.ActionId; surface.LastReceiptHash = hash;
            if (receipt.ExecutionStatus != "EXECUTED" || receipt.VerificationStatus != "VERIFIED" || receipt.Observation is null) {
                surface.Completion!.TrySetException(new PreparationToolException(receipt.ErrorCode is { } code && FailureCodes.Contains(code) ? code : "UI_OUTCOME_UNKNOWN", action.Tool, "La pestaña no pudo verificar la acción. Revisa el borrador antes de iniciar otra ejecución."));
            } else {
                var state = receipt.Observation;
                if (action.Tool is not ("start" or "ui_read_visible_state" or "set_working_context")) NovaTelemetry.VisibleAction(id);
                if (state.Candidates.Length > 24 || state.Values.Count > 60 || state.TemplateText?.Length > 32000) throw new ValidationException("Observación fuera de límites.");
                surface.Completion!.TrySetResult(state);
                live.Emit(id, LiveStatus.VERIFYING, LiveActor.SYSTEM_VALIDATION, "Acción comprobada en esta pestaña.", "UiActionReceipt", action.Tool, state.PromptId, actionId: action.ActionId, actionSequence: action.Sequence);
            }
        }
    }
    public void Progress(Guid id, string capability, UiActionProgress progress)
    {
        var surface = Get(id, capability);
        lock (surface.Gate) {
            if (surface.Pending is not { } action || action.ActionId != progress.ActionId || action.Sequence != progress.Sequence
                || LiveRuns.Terminal(live.Get(id).Status) || progress.ExecutionStatus is not ("TARGET_LOCATED" or "EXECUTED")) throw new ConflictException("Progreso fuera de contrato.");
            if (action.ExecutionStatus == progress.ExecutionStatus) return;
            if (action.ExecutionStatus == "EXECUTED") throw new ConflictException("La acción ya informó ejecución.");
            surface.Pending = action with { ExecutionStatus = progress.ExecutionStatus };
            live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION,
                progress.ExecutionStatus == "TARGET_LOCATED" ? "NOVA encontró el control." : "Interacción realizada. Comprobando el resultado.",
                "UiActionProgress", action.Tool, action.Target, actionId: action.ActionId, actionSequence: action.Sequence);
        }
    }
    private void ValidateObservation(UiActionEnvelope action, UiVisibleState state)
    {
        if (state.Mode is not ("discover" or "focus" or "create") || state.Candidates is null || state.Values is null || state.VariableNames is null
            || state.Candidates.Length > 24 || state.Values.Count > 60 || state.VariableNames.Length > 60 || state.TemplateText?.Length > 32000
            || state.Title?.Length > 160 || state.Description?.Length > 2000 || state.CompiledText?.Length > 512000 || state.Response?.Length > 512000
            || (state.PromptId is null) != (state.Revision is null) || state.Revision is < 1
            || state.Values.Any(value => value.Key.Length > 80 || value.Value is null || value.Value.Length > 8000)) throw new ValidationException("Observación fuera de contrato.");
        if (action.Tool is "ui_review_deletion" or "ui_enable_deletion_confirmation") {
            var proposal = live.View(action.RunId).Deletion;
            if (proposal is null || action.Target != proposal.PromptId || state.DeletionRunId != action.RunId
                || state.DeletionPromptId != proposal.PromptId || state.DeletionRevision != proposal.Revision
                || action.Tool == "ui_enable_deletion_confirmation" && !state.DeletionConfirmationReady)
                throw new ValidationException("El recibo no identifica la revisión de eliminación autorizada.");
        } else if (action.Tool == "ui_verify_deletion") {
            if (!live.Get(action.RunId).EffectCommitted || action.Target is not {} deleted || state.DeletionPromptId is not null
                || state.PromptId == deleted || state.Candidates.Any(candidate => candidate.Id == deleted))
                throw new ValidationException("No se observó la eliminación confirmada.");
        } else if (action.Target is { } target && action.Tool != "set_working_context" && state.PromptId != target) throw new ValidationException("El recibo corresponde a otro prompt.");
        if (action.Tool == "ui_open_editor" && state.Mode != "create" || action.Tool == "ui_open_prompt" && state.Mode != "focus"
            || action.Tool == "ui_open_create_prompt" && (state.Mode != "create" || state.PromptId is not null)) throw new ValidationException("No se observó la navegación solicitada.");
        if ((action.Tool == "ui_prepare_creation" || action.Tool == "ui_prepare_update" && action.Arguments.TryGetProperty("templateText", out _))
            && (state.Title != action.Arguments.GetProperty("title").GetString() || state.TemplateText != action.Arguments.GetProperty("templateText").GetString())) throw new ValidationException("El formulario no coincide con el contenido enviado.");
        if (action.Tool is "ui_prepare_creation" or "ui_prepare_update" && action.Arguments.TryGetProperty("description", out var description)
            && state.Description != description.GetString()) throw new ValidationException("El formulario no conserva la descripción enviada.");
        if (action.Tool == "ui_fill_variable" && !UiToolCatalog.FieldValueMatches(state, action.Arguments)) throw new ValidationException("El campo no contiene el valor autorizado.");
    }
    private JsonElement StartContext(Guid id, bool localExecutionAllowed, string editRequest) => JsonSerializer.SerializeToElement(new { localExecutionAllowed, editRequest, providerId = live.Active(id).Semantic?.ProviderId });
    public IVisibleBrowserFactory Factory() => new FactoryAdapter(this);
    private sealed class FactoryAdapter(SameTabUiDispatcher dispatcher) : IVisibleBrowserFactory
    { public IVisibleBrowser Create(Action<string>? disconnected = null) => new DriverAdapter(dispatcher); }
    private sealed class DriverAdapter(SameTabUiDispatcher dispatcher) : IVisibleBrowser
    {
        private Guid runId;
        public Task<UiVisibleState> Start(Guid id, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "")
        { runId = id; return dispatcher.Dispatch(id, "start", dispatcher.StartContext(id, localExecutionAllowed, editRequest), ct); }
        public Task<UiVisibleState> Execute(string tool, JsonElement arguments, CancellationToken ct) => dispatcher.Dispatch(runId, tool, arguments, ct);
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private async Task<UiVisibleState> Dispatch(Guid id, string tool, JsonElement arguments, CancellationToken ct)
    {
        using var measurement = NovaTelemetry.Measure("ui." + tool);
        if (!surfaces.TryGetValue(id, out var surface)) throw new ConflictException("No hay una pestaña vinculada.");
        Task<UiVisibleState> result;
        lock (surface.Gate) {
            if (surface.Pending is not null) throw new ConflictException("Ya hay una acción pendiente.");
            Guid? target = arguments.TryGetProperty("promptId", out var raw) && Guid.TryParse(raw.GetString(), out var parsed) ? parsed : null;
            surface.Pending = new(id, Guid.NewGuid(), ++surface.Sequence, tool, target, arguments.Clone(), "DISPATCHED", "PENDING", DateTime.UtcNow);
            surface.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously); result = surface.Completion.Task;
        }
        var dispatched = surface.Pending!;
        live.Emit(id, LiveStatus.TOOL_RUNNING, LiveActor.SYSTEM_VALIDATION, "Acción enviada a la pestaña emisora; esperando su resultado.", "UiActionDispatched", tool, dispatched.Target, actionId: dispatched.ActionId, actionSequence: dispatched.Sequence);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try {
            while (!result.IsCompleted) {
                await Task.WhenAny(result, Task.Delay(500, timeout.Token)); timeout.Token.ThrowIfCancellationRequested();
            }
            var state = await result; await live.Boundary(id, timeout.Token); return state;
        } finally { lock (surface.Gate) { surface.Pending = null; surface.Completion = null; } }
    }
}
