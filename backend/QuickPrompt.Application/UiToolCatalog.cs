using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record UiSearchArguments(string Query);
public record UiTargetArguments(Guid PromptId);
public record UiFillArguments(Guid? PromptId, string Name, string Value, string Evidence);
public record UiQuestionArguments(string Question);

public static class UiToolCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private static T Parse<T>(JsonElement args) => args.Deserialize<T>(Json) ?? throw new ValidationException("Argumentos de interfaz inválidos.");
    public static readonly JsonElement Empty = ContractSchema.Object([]);
    public static readonly object DraftTarget = new { anyOf = new object[] { ContractSchema.Text(36), new { type = "null" } } };
    public static readonly JsonElement Target = ContractSchema.Object(new() { ["promptId"] = DraftTarget }, "promptId");
    public static readonly JsonElement NoArguments = JsonSerializer.SerializeToElement(new { });
    public static readonly UiToolContract[] All = [
        new("ui_navigate_library", "Return to the actual library to search a different prompt. Never discard an open editor draft.", Empty),
        new("ui_open_create_prompt", "Open the real new-prompt form, without saving. Only for a new draft requested by the user.", Empty),
        new("ui_prepare_creation", "Generate the requested new reusable prompt with Qwen and type the validated title and body into the real open form. Arguments MUST be empty {}: do not pass title, body or text. Code invokes the generator with the original request. Never saves.", Empty),
        new("ui_load_active_draft", "Load the authorized previous conversation draft into the real editor. No text arguments; content is bound by code to the session artifact. Use before changing, copying or running that draft.", Target),
        new("ui_search_prompt", "Search the actual visible library using a short title or topic; never a full request.", ContractSchema.Object(new() { ["query"] = ContractSchema.Text(200) }, "query")),
        new("ui_open_prompt", "Click one real prompt currently visible in the library; verify its identity. A user-selected visible ID may be opened directly.", ContractSchema.Object(new() { ["promptId"] = ContractSchema.Text(36) }, "promptId")),
        new("ui_open_editor", "Open the selected prompt's actual editor, without saving.", Target),
        new("ui_fill_variable", "Fill one visible variable with an EXACT value from the user's request or explicitly supplied values. evidence must quote that value literally.", ContractSchema.Object(new() { ["promptId"] = DraftTarget, ["name"] = ContractSchema.Text(80), ["value"] = ContractSchema.Text(8000), ["evidence"] = ContractSchema.Text(16000) }, "promptId", "name", "value", "evidence")),
        new("ui_prepare_update", "Ask the existing Qwen editor to modify this DRAFT according to the ORIGINAL USER REQUEST held by the server. The executor fills that exact request in the editor; do not rewrite it or pass new instructions. Never saves the source.", Target),
        new("ui_show_compiled_prompt", "Inspect the deterministic compiled preview; ask for missing inputs if not ready.", Target),
        new("ui_open_provider_picker", "Open the existing provider picker for review only. No external navigation, copying or submission.", Target),
        new("ui_execute_local", "Execute the compiled prompt with local Qwen ONLY when the user's original request explicitly asks for local execution.", Target, "EXPLICIT_LOCAL_EXECUTION"),
        new("ui_read_visible_state", "Read visible React controls and current state without changing them.", Empty),
        new("ui_verify_prompt_state", "Independently compare visible identity/revision/compiled values with the domain source; no mutation.", Target),
        new("ui_request_save_version", "Request review of this exact draft and source revision. This does not save. A human must confirm the proposal.", Target, "REQUIRES_PROPOSAL_APPROVAL"),
        new("finish", "Finish when every requested effect has been independently observed. A changed draft is held for human review, never silently saved.", Empty),
        new("ask_clarification", "Ask one concrete question when no unique source exists or required data is missing. Never select arbitrarily.", ContractSchema.Object(new() { ["question"] = ContractSchema.Text(500) }, "question"))
    ];
    public static string Label(string tool) => tool switch {
        "ui_navigate_library" => "Volver a la biblioteca", "ui_open_create_prompt" => "Abrir el formulario de creación", "ui_prepare_creation" => "Generar y escribir el nuevo borrador",
        "ui_load_active_draft" => "Recuperar el borrador de la conversación",
        "ui_search_prompt" => "Buscar en la biblioteca", "ui_open_prompt" => "Abrir el prompt seleccionado", "ui_open_editor" => "Abrir el editor",
        "ui_fill_variable" => "Completar un campo autorizado", "ui_prepare_update" => "Preparar el cambio con Qwen en el editor",
        "ui_show_compiled_prompt" => "Revisar la vista previa compilada", "ui_open_provider_picker" => "Mostrar los destinos disponibles",
        "ui_execute_local" => "Ejecutar con Qwen local", "ui_read_visible_state" => "Leer el estado visible", "ui_verify_prompt_state" => "Verificar el prompt y su revisión",
        "ui_request_save_version" => "Pedir revisión de la nueva versión", "finish" => "Terminar y verificar el resultado", "ask_clarification" => "Pedir una aclaración", _ => "Acción no reconocida" };
    public static UiToolContract[] Available(UiVisibleState state, bool searched, bool ambiguous, bool localAllowed, bool updateAllowed, bool editorAllowed = false, Guid? requestedSourceId = null, bool changeValidated = false)
    {
        var selectedVisible = requestedSourceId is { } selected && state.Candidates.Any(candidate => candidate.Id == selected);
        var changeReady = !updateAllowed || changeValidated && state.DraftChanged;
        var names = new HashSet<string> { "ui_read_visible_state" };
        if (requestedSourceId is null || state.PromptId is not null || searched && !selectedVisible) names.Add("ask_clarification");
        if (state.Mode == "discover")
        { if (!ambiguous) names.Add("ui_search_prompt"); if (!ambiguous && (selectedVisible || requestedSourceId is null && searched && state.Candidates.Length > 0)) names.Add("ui_open_prompt"); }
        if (state.PromptId is not null)
        {
            names.UnionWith(["ui_verify_prompt_state", "finish"]);
            if (state.Mode == "focus" && (updateAllowed || editorAllowed)) names.Add("ui_open_editor");
            if (state.VariableNames.Length > 0 && changeReady) names.Add("ui_fill_variable");
            if (state.Mode == "create" && updateAllowed && !state.DraftChanged) names.Add("ui_prepare_update");
            if (state.DraftChanged) names.Add("ui_request_save_version");
            if (state.CompiledReady && changeReady) { names.Add("ui_show_compiled_prompt"); names.Add("ui_open_provider_picker"); if (localAllowed && !state.ResponseComplete) names.Add("ui_execute_local"); }
        }
        return All.Where(t => names.Contains(t.Name)).ToArray();
    }
    public static void Validate(UiDecision decision, UiToolContract[] available, UiVisibleState state, LiveStartInput input, SemanticInterpretation? semantic = null)
    {
        var contract = available.SingleOrDefault(t => t.Name == decision.ToolName) ?? throw new PreparationToolException("UI_TOOL_NOT_ALLOWED", decision.ToolName, "Qwen propuso una acción no disponible o no autorizada.");
        ContractSchema.Validate(decision.Arguments, contract.ArgumentsSchema);
        if (semantic is not null) SemanticPlanValidator.AuthorizeTool(semantic, decision.ToolName);
        if (decision.ToolName == "ui_execute_local" && (semantic?.Executes != true || !state.CompiledReady || GroundedValues(input, state).Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value)))
            throw new ValidationException("Completa los datos autorizados y confirma la ejecución local en la petición.");
        if (decision.ToolName == "ui_execute_local" && semantic is not null && state.Values.Values.Any(value => value.Contains(semantic.ExecutionEvidence, StringComparison.Ordinal)))
            throw new ValidationException("Los datos del prompt no conceden permiso para ejecutar el modelo.");
        if (decision.ToolName == "ui_search_prompt" && string.IsNullOrWhiteSpace(Parse<UiSearchArguments>(decision.Arguments).Query)) throw new ValidationException("Falta la consulta de búsqueda.");
        if (decision.ToolName == "ask_clarification" && string.IsNullOrWhiteSpace(Parse<UiQuestionArguments>(decision.Arguments).Question)) throw new ValidationException("Falta una pregunta concreta.");
        if (decision.ToolName == "ui_open_prompt")
        {
            Guid openTargetId; try { openTargetId = Parse<UiTargetArguments>(decision.Arguments).PromptId; } catch (JsonException) { throw new ValidationException("ID inválido."); }
            if (!state.Candidates.Any(p => p.Id == openTargetId)) throw new ValidationException("El ID no está en la biblioteca visible.");
            if (input.SourceId is { } requested && openTargetId != requested) throw new ValidationException("La petición se refiere al prompt seleccionado, no a otro origen.");
        }
        if (decision.ToolName != "ui_open_prompt" && decision.Arguments.TryGetProperty("promptId", out var target)) {
            var unsaved = target.ValueKind == JsonValueKind.Null && state.Mode == "create" && state.PromptId is null && semantic?.Creates == true;
            if (!unsaved && (!Guid.TryParse(target.GetString(), out var targetId) || targetId != state.PromptId)) throw new ValidationException("La acción no corresponde al prompt visible.");
        }
        if (decision.Arguments.TryGetProperty("promptId", out _) && input.SourceId is { } expected && state.Mode != "discover" && state.PromptId != expected)
            throw new ValidationException("El objetivo visible cambió. No se actuará sobre otro prompt.");
        if (decision.ToolName == "ui_fill_variable")
        {
            var fill = Parse<UiFillArguments>(decision.Arguments); var name = fill.Name; var value = fill.Value; var evidence = fill.Evidence;
            if (!state.VariableNames.Contains(name) || string.IsNullOrWhiteSpace(value) || GroundedValues(input, state).GetValueOrDefault(name) != value
                || !(input.Values?.GetValueOrDefault(name) == value || !string.IsNullOrWhiteSpace(evidence) && new[] { input.Message, input.AuthorizedPendingRequest }.Any(request => request?.Contains(evidence, StringComparison.Ordinal) == true) && evidence.Contains(value, StringComparison.Ordinal)))
                throw new ValidationException("El valor no está autorizado por la petición ni corresponde a un campo real.");
        }
        if (decision.ToolName == "ui_prepare_update" && (semantic?.Adapts != true || input.TaskRequest.Length > 4000)) throw new ValidationException("El editor visible necesita una petición de cambio de hasta 4000 caracteres.");
    }
    public static Dictionary<string, string> GroundedValues(LiveStartInput input, UiVisibleState state)
    {
        if (state.TemplateText is null) return [];
        var proposal = new PromptProposal(PreparationDecision.ADAPT_EXISTING, state.PromptId, state.Title ?? "", "", state.TemplateText, [], "", "Resolución de campos autorizados.");
        var current = new GroundedVariableResolver().Resolve(new PrepareInput(input.RunId, input.Message) { AuthorizedPendingRequest = input.AuthorizedPendingRequest }, proposal, null);
        return (input.Values ?? []).Where(value => state.VariableNames.Contains(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .Concat(current.Select(value => new KeyValuePair<string, string>(value.Name, value.Value))).GroupBy(value => value.Key).ToDictionary(group => group.Key, group => group.Last().Value);
    }
}
