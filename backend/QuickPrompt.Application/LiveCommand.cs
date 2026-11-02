using QuickPrompt.Local;
using System.Text.Json.Serialization;

namespace QuickPrompt.Nova.Application;

// An explicit UI choice is user input, not an instruction for the model to classify again.
// It grants no persistence: the existing hash/revision-bound approval still owns every write.
public record LiveCommand(SemanticOperation Operation, string Content = "", string? ProviderId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SemanticOperation[]? RequestedOutcomes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] CopyContent CopyContent = CopyContent.RENDERED)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? InputNames { get; init; }
    public void Validate(LiveStartInput input)
    {
        PromptInputContract.ValidateSelection(InputNames);
        if(InputNames is not null&&Operation is not (SemanticOperation.CREATE_DRAFT or SemanticOperation.PROPOSE_ADAPTATION))
            throw new ValidationException("La definición de campos requiere crear o actualizar un prompt.");
        if (!Enum.IsDefined(CopyContent) || RequestedOutcomes is { } requested && (requested.Length is < 1 or > 8
            || requested.Distinct().Count() != requested.Length || !requested.Contains(Operation)
            || requested.Except([Operation, SemanticOperation.COPY_PROMPT]).Any()))
            throw new ValidationException("Resultados de la acción rápida fuera de contrato.");
        if (CopyContent == CopyContent.TEMPLATE && !(RequestedOutcomes ?? [Operation]).Any(op => op is SemanticOperation.COPY_PROMPT or SemanticOperation.COPY_TO_CLIPBOARD))
            throw new ValidationException("Elige un resultado de copia antes de seleccionar la plantilla.");
        if (CopyContent == CopyContent.TEMPLATE && input.Mode != "SAME_TAB")
            throw new ValidationException("La copia de plantilla requiere la pestaña visible.");
        if (Operation is not (SemanticOperation.CREATE_DRAFT or SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.OPEN_PROMPT or SemanticOperation.SEARCH_PROMPT
            or SemanticOperation.FILL_VARIABLES or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.PREPARE_PROVIDER or SemanticOperation.EXECUTE_LOCAL or SemanticOperation.COPY_TO_CLIPBOARD)
            || Content is null || Content.Length > 8000 || !input.Message.Contains(Content, StringComparison.Ordinal))
            throw new ValidationException("Acción rápida fuera de contrato.");
        if (Operation is SemanticOperation.CREATE_DRAFT or SemanticOperation.SEARCH_PROMPT ? input.SourceId is not null : input.SourceId is null)
            throw new ValidationException("La acción requiere su origen y revisión explícitos, o un borrador nuevo sin origen.");
        if (Operation is SemanticOperation.CREATE_DRAFT or SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.SEARCH_PROMPT && string.IsNullOrWhiteSpace(Content))
            throw new ValidationException("Describe el contenido o cambio que necesitas.");
        if (ProviderId is not null && !ProviderRegistry.Providers.Any(provider => provider.ProviderId == ProviderId)
            || Operation == SemanticOperation.PREPARE_PROVIDER && ProviderId is null
            || Operation is SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_LOCAL_EXECUTION && ProviderId is not (null or "ollama"))
            throw new ValidationException("Destino no compatible con la acción.");
    }

    public SemanticInterpretation Compile(string message, SemanticContext context)
    {
        var creates = Operation == SemanticOperation.CREATE_DRAFT;
        var searches = Operation == SemanticOperation.SEARCH_PROMPT;
        var changes = Operation == SemanticOperation.PROPOSE_ADAPTATION;
        var operation = Operation == SemanticOperation.PREPARE_PROVIDER && ProviderId == "ollama" ? SemanticOperation.PREPARE_LOCAL_EXECUTION : Operation;
        SemanticOperation[] roots = RequestedOutcomes ?? (creates ? [operation, SemanticOperation.REQUEST_SAVE_COPY]
            : changes ? [operation, SemanticOperation.REQUEST_SAVE_VERSION] : [operation]);
        var denied = new[] { SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.CREATE_DRAFT, SemanticOperation.EXECUTE_LOCAL,
            SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_SAVE_COPY, SemanticOperation.REQUEST_DELETE, SemanticOperation.COPY_TO_CLIPBOARD }.Except(roots).ToArray();
        var source = creates || searches ? null : context.Selected ?? throw new ValidationException("Selecciona de nuevo el prompt.");
        var outcome = creates ? SemanticOutcome.NEW_DRAFT : changes ? SemanticOutcome.DRAFT : operation switch {
            SemanticOperation.EXECUTE_LOCAL => SemanticOutcome.LOCAL_RESPONSE,
            SemanticOperation.PREPARE_LOCAL_EXECUTION => SemanticOutcome.READY_LOCAL,
            SemanticOperation.PREPARE_PROVIDER => SemanticOutcome.READY_PROVIDER,
            _ => SemanticOutcome.READ
        };
        // The typed command owns the action. Its textual receipt must stay outside
        // field data and quoted source names, rather than cite the whole envelope.
        var directiveEvidence = SemanticPlanValidator.DirectiveFragments(
            GroundedVariableResolver.InstructionDirective(message, source?.InputNames ?? []))
            .FirstOrDefault(fragment => !string.IsNullOrWhiteSpace(fragment))?.Trim() ?? "";
        var meaning = new SemanticMeaning(roots, searches ? TargetReference.NAMED : creates ? TargetReference.NONE : TargetReference.SELECTED,
            searches ? Content : source?.Title ?? "", source?.Id, searches ? Content : source?.Title ?? "", Content,
            new(denied, !changes, null, null, [], [], false), outcome, [], creates || changes,
            operation == SemanticOperation.EXECUTE_LOCAL ? directiveEvidence : "", operation is SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_LOCAL_EXECUTION ? "ollama" : ProviderId, "Acción elegida explícitamente en la interfaz.") {
                CopyContent = CopyContent,
                ClipboardEvidence = roots.Contains(SemanticOperation.COPY_TO_CLIPBOARD) ? directiveEvidence : null
            };
        var request = UserActionRequest.FromOperations(message, roots);
        var intent = TaskIntentContract.Accept(request, meaning, true);
        var plan = SemanticPlanCompiler.Compile(meaning, true) with { IntentContract = intent };
        // Choosing CREATE_DRAFT fixes the operation, not the semantic roles of
        // title/body/current-authoring text inside the user's work order. Reuse
        // the canonical authoring boundary instead of declaring it future prose.
        var clauses = creates ? new[] { new TaskClause(TaskClauseRole.WORK_ORDER, message) }
            : changes ? new[] { new TaskClause(TaskClauseRole.CONTENT, Content) } : [];
        return plan with { Goal = TaskGoal.FromIntent(message, context, plan, clauses, intent), ValidationNotes = ["EXPLICIT_UI_COMMAND"], UserDefinedInputs=InputNames?.ToArray() };
    }
}
