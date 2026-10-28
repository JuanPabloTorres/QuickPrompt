namespace QuickPrompt.Nova.Application;

// Public presentation contract. Never copy requests, model output, arguments,
// field values or draft bodies into this allowlisted projection.
public record RuntimeTool(string Id, string Label, string State);
public record RuntimeMilestone(string Id, string Label, string State);
public record RuntimeMemory(bool SourceBound, bool DraftActive, int ResolvedVariables, int TotalVariables, bool ReusesDraft, string[] Constraints);
public record LiveRuntime(string Version, Guid RunId, long Sequence, string Objective, string ContextSource, bool ContextResolved,
    string Model, string ModelState, double? LastInferenceMs, double ElapsedMs, string? ActiveTool, RuntimeTool[] Tools,
    RuntimeMemory Memory, RuntimeMilestone[] Flow, bool RecoverableOnly);

public sealed partial class LiveRuns
{
    // Publish the exact eligible set chosen by the existing executor, never
    // recompute permissions in the read model or frontend.
    public void PublishRuntimeTools(Guid id, IEnumerable<UiToolContract> tools)
    {
        var control = Active(id);
        lock (control.Gate) {
            var names = tools.Select(tool => tool.Name).Where(UiToolCatalog.IsRegistered).Distinct().Order().ToArray();
            if (Terminal(Get(id).Status) || control.RuntimeTools.SequenceEqual(names)) return;
            control.RuntimeTools = names;
            Emit(id, Get(id).Status, LiveActor.SYSTEM_VALIDATION, "Herramientas disponibles actualizadas.", "ToolCatalogReady");
        }
    }

    private LiveRuntime RuntimeView(LiveRecord run, Control? control)
    {
        var events = store.Events(run.RunId, 0).Where(item => item.Sequence <= run.Sequence).ToArray();
        var terminal = Terminal(run.Status);
        var paused = run.PauseRequested || run.Status == LiveStatus.PAUSED;
        var semantic = control?.Semantic;
        var observation = control?.Observation;
        var available = terminal || paused ? [] : control?.RuntimeTools ?? [];
        var known = UiToolCatalog.All.Select(tool => tool.Name).ToHashSet();
        var toolEvents = events.Where(item => item.ToolName is not null && known.Contains(item.ToolName)).ToArray();
        var recent = events.LastOrDefault(item => item.Stage is not ("ToolCatalogReady" or "UiActionDispatched" or "UiActionProgress"));
        var lastTool = toolEvents.LastOrDefault();
        var activeTool = !terminal && lastTool is { Status: LiveStatus.TOOL_RUNNING or LiveStatus.TOOL_PROPOSED or LiveStatus.TOOL_APPROVED }
            ? lastTool.ToolName : null;
        var tools = available.Concat(toolEvents.Select(item => item.ToolName!)).Distinct().Select(name => {
            var last = toolEvents.LastOrDefault(item => item.ToolName == name);
            var verified = toolEvents.LastOrDefault(item => item.ToolName == name && (item.Status == LiveStatus.UI_UPDATED || item.Stage == "UiActionReceipt"));
            var started = toolEvents.LastOrDefault(item => item.ToolName == name && item.Status is LiveStatus.TOOL_PROPOSED or LiveStatus.TOOL_APPROVED or LiveStatus.TOOL_RUNNING);
            var state = last?.ErrorCode is not null ? "blocked"
                : activeTool == name ? paused ? "paused" : last!.Status == LiveStatus.TOOL_RUNNING ? "running" : "selected"
                : verified is not null && verified.Sequence > (started?.Sequence ?? 0) ? "completed"
                : available.Contains(name) ? "available" : "unavailable";
            if (terminal && state is "running" or "selected" or "paused") state = "unavailable";
            return new RuntimeTool(name, UiToolCatalog.Label(name), state);
        }).ToArray();
        var decision = events.LastOrDefault(item => item.DurationMs is not null &&
            (item.Stage == "Decision" && item.Actor == LiveActor.MODEL_SELECTED
                || item.Stage == "Observation" && item.ToolName is "ui_prepare_creation" or "ui_prepare_update" or "ui_execute_local"));
        var interpreting = events.FirstOrDefault(item => item.Stage == "SemanticInterpretation");
        var interpreted = events.FirstOrDefault(item => item.Stage == "SemanticPlan");
        var inferenceMs = decision?.DurationMs ?? (control is not null && control.Input.Command is null && interpreting is not null && interpreted is not null ? (interpreted.Timestamp - interpreting.Timestamp).TotalMilliseconds : (double?)null);
        var modelState = terminal ? run.Status is LiveStatus.FAILED or LiveStatus.BLOCKED ? "error" : "ready"
            : paused || run.Status == LiveStatus.AWAITING_APPROVAL ? "waiting"
            : recent?.Stage is "Generation" or "GenerationRetry" || activeTool == "ui_execute_local" ? "generating"
            : recent?.Stage is "SemanticInterpretation" or "InputExtraction" or "Model" or "ModelRetry" ? "interpreting" : "ready";
        var objective = semantic?.DesiredOutcome switch {
            SemanticOutcome.NEW_DRAFT => "Crear un prompt", SemanticOutcome.DRAFT => "Adaptar un borrador",
            SemanticOutcome.READY_LOCAL => "Preparar el modelo local", SemanticOutcome.READY_PROVIDER => "Preparar un destino",
            SemanticOutcome.LOCAL_RESPONSE => "Generar una respuesta local", SemanticOutcome.ADVICE => "Resolver una consulta",
            SemanticOutcome.REVIEW => "Preparar una revisión", SemanticOutcome.READ => "Consultar la biblioteca",
            _ => "Solicitud actual"
        };
        var sourceBound = control?.Input.SourceId is not null;
        var draft = control?.WorkingDraft is not null || control?.GeneratedDraft is not null || control?.Proposal is not null || observation?.DraftChanged == true;
        var constraints = new List<string> { "Escrituras con aprobación" };
        if (!run.EffectCommitted) constraints.Add("Sin escrituras confirmadas en este run");
        if (semantic is not null && !semantic.Executes) constraints.Add("Sin ejecución local autorizada");
        var variables = observation?.VariableNames ?? [];
        var memory = new RuntimeMemory(sourceBound, draft, variables.Count(name => !string.IsNullOrWhiteSpace(observation?.Values.GetValueOrDefault(name))),
            variables.Length, control?.WorkingDraft is not null, constraints.ToArray());
        // A milestone is completed only by its own receipt, never because a
        // later stage happened. Some valid runs skip tools or human approval.
        var flow = new[] {
            Milestone("interpret", "Interpretar", interpreting is not null, interpreted is not null),
            Milestone("context", "Contexto", interpreted is not null, semantic is not null),
            Milestone("select", "Elegir", events.Any(e => e.Stage == "Model"), events.Any(e => e.Stage == "Decision")),
            Milestone("execute", "Ejecutar", toolEvents.Any(e => e.Status == LiveStatus.TOOL_RUNNING), toolEvents.Any(e => e.Status == LiveStatus.UI_UPDATED)),
            Milestone("verify", "Verificar", events.Any(e => e.Stage == "Verification"), run.Status == LiveStatus.COMPLETED),
            new RuntimeMilestone("approval", "Finalizar", terminal ? run.Status == LiveStatus.COMPLETED ? "completed" : "stopped" : run.Status == LiveStatus.AWAITING_APPROVAL ? "waiting" : "pending")
        };
        var end = terminal ? events.FirstOrDefault(e => Terminal(e.Status))?.Timestamp ?? run.UpdatedAt : run.UpdatedAt;
        return new("nova-runtime-1", run.RunId, run.Sequence, objective,
            control is null ? "Historial recuperado" : control.WorkingDraft is not null ? "Borrador de esta conversación + petición" : sourceBound ? "Prompt seleccionado + petición" : "Petición actual",
            semantic is not null, run.Model, modelState, inferenceMs, Math.Max(0, (end - (events.FirstOrDefault()?.Timestamp ?? end)).TotalMilliseconds),
            activeTool, tools, memory, flow, control is null);

        RuntimeMilestone Milestone(string id, string label, bool started, bool completed) =>
            new(id, label, completed ? "completed" : started ? terminal ? "stopped" : paused ? "paused" : "active" : "pending");
    }
}
