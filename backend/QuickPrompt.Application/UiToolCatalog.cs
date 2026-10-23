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
    // Execute a resolved semantic identity, never infer a new source from words.
    // Returning null leaves genuinely unresolved navigation to the decision model.
    public static UiDecision? BoundNavigation(UiToolContract[] available, UiVisibleState state, PromptDocument? source, bool searched)
    {
        if (source is null) return null;
        if (state.Mode != "discover") return state.Mode != "create" && state.PromptId != source.Id && available.Any(tool => tool.Name == "ui_navigate_library")
            ? new("ui_navigate_library", NoArguments, "validated_plan") : null;
        if (!searched && available.Any(tool => tool.Name == "ui_search_prompt"))
            return new("ui_search_prompt", JsonSerializer.SerializeToElement(new { query = source.Title }), "validated_plan");
        if (state.Candidates.Any(candidate => candidate.Id == source.Id && candidate.Revision == source.Revision)
            && available.Any(tool => tool.Name == "ui_open_prompt"))
            return new("ui_open_prompt", JsonSerializer.SerializeToElement(new { promptId = source.Id }), "validated_plan");
        return null;
    }
    public static UiDecision? BoundInput(UiToolContract[] available, UiVisibleState state, IReadOnlyDictionary<string, string> grounded)
    {
        if (!available.Any(tool => tool.Name == "ui_fill_variable")) return null;
        var name = state.VariableNames.FirstOrDefault(field => grounded.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) && state.Values.GetValueOrDefault(field) != value);
        return name is null ? null : new("ui_fill_variable", JsonSerializer.SerializeToElement(new { promptId = state.PromptId, name, value = grounded[name], evidence = grounded[name] }), "validated_plan");
    }
    public static UiDecision? BoundGoal(UiToolContract[] available, UiVisibleState state, SemanticInterpretation plan, IReadOnlyCollection<string> executed)
    {
        bool Has(string tool) => available.Any(t => t.Name == tool);
        UiDecision Action(string tool) => new(tool, tool is "ui_navigate_library" or "ui_open_create_prompt" or "ui_prepare_creation" or "finish"
            ? NoArguments : JsonSerializer.SerializeToElement(new { promptId = state.PromptId }), "validated_goal");
        // Semantic choices already reside in TaskGoal. These are state transitions of its typed executor,
        // not new classifications of words, or model decisions for individual mouse movements.
        foreach (var tool in new[] { "ui_navigate_library", "ui_load_active_draft", "ui_open_create_prompt", "ui_prepare_creation", "ui_open_editor", "ui_prepare_update" })
            if (Has(tool) && !executed.Contains(tool)) return Action(tool);
        if (Has("ui_show_template") && !executed.Contains("ui_show_template")) return Action("ui_show_template");
        if (Has("ui_copy_template") && !executed.Contains("ui_copy_template")) return Action("ui_copy_template");
        if (Has("ui_copy_compiled_prompt") && !executed.Contains("ui_copy_compiled_prompt")) return Action("ui_copy_compiled_prompt");
        if (Has("ui_open_provider_picker")) return Action("ui_open_provider_picker");
        if (Has("ui_execute_local") && !executed.Contains("ui_execute_local")) return Action("ui_execute_local");
        if (Has("finish") && plan.Goal?.ApprovalRequirements.Length > 0 && !state.DraftChanged && !plan.Adapts && !plan.Creates && Has("ask_clarification"))
            return new("ask_clarification", JsonSerializer.SerializeToElement(new { question = "No hay un borrador modificado. ¿Qué cambio quieres revisar antes de guardar?" }), "validated_goal");
        if (Has("finish")) return Action("finish");
        if (Has("ask_clarification") && (plan.RequiresCompiledOutput || plan.RequiresProvidedValues)
            && state.Mode != "discover" && !state.CompiledReady && state.VariableNames.Any(name => string.IsNullOrWhiteSpace(state.Values.GetValueOrDefault(name))))
            return new("ask_clarification", JsonSerializer.SerializeToElement(new { question = "¿Puedes aportar " + string.Join(", ", state.VariableNames.Where(name => string.IsNullOrWhiteSpace(state.Values.GetValueOrDefault(name)))) + "?" }), "validated_goal");
        return null;
    }
    private static readonly UiCapabilityDescriptor[] Definitions = CapabilityDiscovery.Ui(typeof(UiCapabilities));
    // Preserve the public/default model catalog. Explicit-choice capabilities
    // share this registry but are offered only under the bound task authority.
    public static readonly UiToolContract[] All = Definitions.Where(definition => !definition.RequiresTemplateCopyChoice).Select(definition => definition.Contract).ToArray();
    public static bool IsRegistered(string name) => Definitions.Any(definition => definition.Contract.Name == name);
    private static readonly IReadOnlyDictionary<string, string> Labels = Definitions.ToDictionary(definition => definition.Contract.Name, definition => definition.Label, StringComparer.Ordinal);
    public static string Label(string tool) => Labels.GetValueOrDefault(tool, "Acción no reconocida");
    public static UiToolContract[] Available(UiVisibleState state, bool searched, bool ambiguous, bool localAllowed, bool updateAllowed, bool editorAllowed = false, Guid? requestedSourceId = null, bool changeValidated = false)
    {
        var selectedVisible = requestedSourceId is { } selected && state.Candidates.Any(candidate => candidate.Id == selected);
        var changeReady = !updateAllowed || changeValidated && state.DraftChanged;
        var names = new HashSet<string> { "ui_read_visible_state" };
        if (requestedSourceId is null || state.PromptId is not null || searched && !selectedVisible) names.Add("ask_clarification");
        if (state.Mode == "discover")
        { if (!ambiguous) names.Add("ui_search_prompt"); if (!ambiguous && (selectedVisible || requestedSourceId is null && searched && state.Candidates.Length > 0)) names.Add("ui_open_prompt"); }
        if (state.PromptId is not null || state.Mode == "create" && state.TemplateText is { Length: > 0 })
        {
            names.UnionWith(["ui_verify_prompt_state", "finish"]);
            if (state.Mode == "focus" && (updateAllowed || editorAllowed)) names.Add("ui_open_editor");
            if (state.VariableNames.Length > 0 && changeReady) names.Add("ui_fill_variable");
            if (state.Mode == "create" && updateAllowed && !state.DraftChanged) names.Add("ui_prepare_update");
            if (state.DraftChanged) names.Add("ui_request_save_version");
            if (changeReady && state.TemplateText is { Length: > 0 }) {
                names.Add("ui_show_template");
                if (state.TemplatePreviewText == state.TemplateText) names.Add("ui_copy_template");
            }
            if (state.CompiledReady && changeReady) { names.Add("ui_show_compiled_prompt"); names.Add("ui_copy_compiled_prompt"); names.Add("ui_open_provider_picker"); if (localAllowed && !state.ResponseComplete) names.Add("ui_execute_local"); }
        }
        return Definitions.Select(definition => definition.Contract).Where(t => names.Contains(t.Name)).ToArray();
    }
    public static void Validate(UiDecision decision, UiToolContract[] available, UiVisibleState state, LiveStartInput input, SemanticInterpretation? semantic = null, DraftRef? workingReference = null)
    {
        var contract = available.SingleOrDefault(t => t.Name == decision.ToolName) ?? throw new PreparationToolException("UI_TOOL_NOT_ALLOWED", decision.ToolName, "Qwen propuso una acción no disponible o no autorizada.");
        ContractSchema.Validate(decision.Arguments, contract.ArgumentsSchema);
        if (semantic is not null) SemanticPlanValidator.AuthorizeTool(semantic, decision.ToolName);
        if (decision.ToolName == "ui_execute_local" && (semantic?.Executes != true || !state.CompiledReady || GroundedValues(input, state, semantic).Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value)))
            throw new ValidationException("Completa los datos autorizados y confirma la ejecución local en la petición.");
        if (decision.ToolName == "ui_execute_local" && semantic is not null && state.Values.Values.Any(value => value.Contains(semantic.ExecutionEvidence, StringComparison.Ordinal)))
            throw new ValidationException("Los datos del prompt no conceden permiso para ejecutar el modelo.");
        if (decision.ToolName == "ui_copy_compiled_prompt" && (semantic?.Has(SemanticOperation.COPY_TO_CLIPBOARD) != true || !state.CompiledReady || GroundedValues(input, state, semantic).Any(value => state.Values.GetValueOrDefault(value.Key) != value.Value)))
            throw new ValidationException("La copia necesita una petición explícita y el texto actual compilado.");
        if (decision.ToolName == "ui_copy_template" && (semantic?.Has(SemanticOperation.COPY_TO_CLIPBOARD) != true
            || !semantic.RequiresTemplatePreview || state.TemplateText is not { Length: > 0 } || state.TemplatePreviewText != state.TemplateText))
            throw new ValidationException("La copia necesita una petición explícita y la plantilla actual visible.");
        if (decision.ToolName == "ui_search_prompt" && string.IsNullOrWhiteSpace(Parse<UiSearchArguments>(decision.Arguments).Query)) throw new ValidationException("Falta la consulta de búsqueda.");
        if (decision.ToolName == "ask_clarification" && string.IsNullOrWhiteSpace(Parse<UiQuestionArguments>(decision.Arguments).Question)) throw new ValidationException("Falta una pregunta concreta.");
        if (decision.ToolName == "ui_open_prompt")
        {
            Guid openTargetId; try { openTargetId = Parse<UiTargetArguments>(decision.Arguments).PromptId; } catch (JsonException) { throw new ValidationException("ID inválido."); }
            if (!state.Candidates.Any(p => p.Id == openTargetId)) throw new ValidationException("El ID no está en la biblioteca visible.");
            if (input.SourceId is { } requested && openTargetId != requested) throw new ValidationException("La petición se refiere al prompt seleccionado, no a otro origen.");
        }
        if (decision.ToolName != "ui_open_prompt" && decision.Arguments.TryGetProperty("promptId", out var target)) {
            // Session identity is independent of the optional task-goal adapter.
            // The orchestrator obtains this reference from AgentSessions, never model JSON.
            var sessionTemporary = workingReference is { SourceRef: null, DraftRevision: > 0 }
                && workingReference.DraftId != Guid.Empty && workingReference.SessionId == input.SessionId
                && input.SourceId is null && semantic?.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT;
            var restoresTemporary = decision.ToolName == "ui_load_active_draft" && input.SourceId is null
                && (semantic?.Goal is { WorkingTarget: not null, TargetReferences.Length: 0 } || sessionTemporary);
            if (restoresTemporary && sessionTemporary && state.Mode == "create"
                && (state.PromptId is not null || !string.IsNullOrEmpty(state.TemplateText) && state.TemplateText != workingReference!.CurrentContent))
                throw new ConflictException("El editor contiene otro borrador. Se conserva tu texto sin reemplazarlo.");
            var unsaved = target.ValueKind == JsonValueKind.Null && (restoresTemporary || state.Mode == "create" && state.PromptId is null
                && (semantic?.Creates == true || semantic?.Goal?.WorkingTarget is not null || sessionTemporary));
            var matchesVisible = Guid.TryParse(target.GetString(), out var targetId) && targetId == state.PromptId;
            NovaTelemetry.DebugPoint("ui.target.validation", !unsaved && !matchesVisible, () => new() {
                ["tool"] = decision.ToolName, ["argumentIsNull"] = target.ValueKind == JsonValueKind.Null,
                ["mode"] = state.Mode, ["visibleId"] = state.PromptId, ["requestedSourceId"] = input.SourceId,
                ["restoresTemporary"] = restoresTemporary, ["unsavedAccepted"] = unsaved,
                ["matchesVisible"] = matchesVisible, ["creates"] = semantic?.Creates,
                ["hasWorkingTarget"] = semantic?.Goal?.WorkingTarget is not null, ["sessionTemporary"] = sessionTemporary
            });
            if (!unsaved && !matchesVisible) throw new ValidationException("La acción no corresponde al prompt visible.");
        }
        if (decision.Arguments.TryGetProperty("promptId", out _) && input.SourceId is { } expected && state.Mode != "discover" && state.PromptId != expected)
            throw new ValidationException("El objetivo visible cambió. No se actuará sobre otro prompt.");
        if (decision.ToolName == "ui_fill_variable")
        {
            var fill = Parse<UiFillArguments>(decision.Arguments); var name = fill.Name; var value = fill.Value; var evidence = fill.Evidence;
            if (!state.VariableNames.Contains(name) || string.IsNullOrWhiteSpace(value) || GroundedValues(input, state, semantic).GetValueOrDefault(name) != value
                || !(input.Values?.GetValueOrDefault(name) == value || !string.IsNullOrWhiteSpace(evidence) && new[] { input.Message, input.AuthorizedPendingRequest }.Any(request => request?.Contains(evidence, StringComparison.Ordinal) == true) && evidence.Contains(value, StringComparison.Ordinal)))
                throw new ValidationException("El valor no está autorizado por la petición ni corresponde a un campo real.");
        }
        if (decision.ToolName == "ui_prepare_update" && (semantic?.Adapts != true || input.TaskRequest.Length > 4000)) throw new ValidationException("El editor visible necesita una petición de cambio de hasta 4000 caracteres.");
    }
    public static bool FieldValueMatches(UiVisibleState state, JsonElement arguments)
        => state.Values.GetValueOrDefault(arguments.GetProperty("name").GetString()!) == arguments.GetProperty("value").GetString();

    // Navigating toward another authorized source is not an attempt to apply its
    // data to the currently visible form. Revision validation remains strict once
    // that identity is visible; tool execution still uses GroundedValues below.
    public static bool InputTargetVisible(LiveStartInput input, UiVisibleState state)
        => (input.SourceId is null || input.SourceId == state.PromptId)
            && (input.InputScope?.SourceId is null || input.InputScope.SourceId == state.PromptId);

    public static Dictionary<string, string> GroundedValues(LiveStartInput input, UiVisibleState state, SemanticInterpretation? plan = null)
    {
        // Literal data is not an instruction to write fields. A read/advice/
        // authoring goal must not acquire input prerequisites from raw text.
        if (plan is not null && !SemanticPlanCompiler.RequiresInputBinding(plan.Goal?.RequestedEffects ?? plan.Intentions, plan.CopyContent)) return [];
        if (state.TemplateText is null) return [];
        var proposal = new PromptProposal(PreparationDecision.ADAPT_EXISTING, state.PromptId, state.Title ?? "", "", state.TemplateText, [], "", "Resolución de campos autorizados.");
        if (input.InputScope is { } scope) scope.ValidateTarget(state.PromptId, state.Revision);
        var current = new GroundedVariableResolver().Resolve(new PrepareInput(input.RunId, input.Message) {
            AuthorizedPendingRequest = input.AuthorizedPendingRequest, InputScope = input.InputScope }, proposal, null);
        return (input.Values ?? []).Where(value => state.VariableNames.Contains(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .Concat(current.Select(value => new KeyValuePair<string, string>(value.Name, value.Value))).GroupBy(value => value.Key).ToDictionary(group => group.Key, group => group.Last().Value);
    }
}
