using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SemanticOperation
{
    [AgentOperation("Locate saved prompts without changing them.", "search_prompts", "ui_search_prompt")]
    SEARCH_PROMPT,
    [AgentOperation("Resolve one real source and its current revision.", "resolve_prompt", "ui_open_prompt", "ui_search_prompt", "ui_load_active_draft")]
    RESOLVE_PROMPT,
    [AgentOperation("Open the resolved saved prompt or authorized working draft.", "open_prompt", "open_working_draft", "ui_open_prompt")]
    OPEN_PROMPT,
    [AgentOperation("Prepare revised reusable instruction text without saving.", "propose_prompt_update", "revise_prompt_once", "ui_open_editor", "ui_prepare_update", Writes = new[] { AgentRepresentation.ReusableInstruction })]
    PROPOSE_ADAPTATION,
    [AgentOperation("Prepare a distinct reusable instruction without saving.", "propose_new_prompt", "revise_prompt_once", "ui_open_create_prompt", "ui_prepare_creation", Writes = new[] { AgentRepresentation.ReusableInstruction })]
    CREATE_DRAFT,
    [AgentOperation("Validate the exact proposed draft before further effects.", "validate_prompt", "ui_verify_prompt_state")]
    VALIDATE_DRAFT,
    [AgentOperation("Assign authorized case values to actual template inputs.", "resolve_variable_values", "ui_fill_variable", Writes = new[] { AgentRepresentation.CaseValues }, Requires = new[] { AgentRepresentation.CaseValues })]
    FILL_VARIABLES,
    [AgentOperation("Render the template deterministically with its input values.", "compile_prompt", "ui_show_compiled_prompt", Writes = new[] { AgentRepresentation.RenderedInstruction })]
    COMPILE_PROMPT,
    [AgentOperation("Prepare a complete prompt for local execution; do not run it.", "prepare_local_execution", "ui_show_compiled_prompt", "ui_open_provider_picker")]
    PREPARE_LOCAL_EXECUTION,
    [AgentOperation("Run the prepared prompt only with explicit execution authority.", "execute_local_llm", "ui_execute_local")]
    EXECUTE_LOCAL,
    [AgentOperation("Prepare the chosen destination without external submission.", "prepare_provider_transfer", "ui_open_provider_picker")]
    PREPARE_PROVIDER,
    [AgentOperation("Prepare compiled text for copying.", "compile_prompt", "ui_show_compiled_prompt", ModelName = "PREPARE_COPY_PREVIEW", Writes = new[] { AgentRepresentation.RenderedInstruction })]
    COPY_PROMPT,
    [AgentOperation("Explain without changing the artifact.")]
    EXPLAIN,
    [AgentOperation("Ask for essential missing information without guessing.", "ask_clarification")]
    ASK_CLARIFICATION,
    [AgentOperation("Cancel the current task.")]
    CANCEL,
    [AgentOperation("Pause the current task.")]
    PAUSE,
    [AgentOperation("Resume the current task under its existing authority.")]
    RESUME,
    [AgentOperation("Request human review of an existing-record update; this is not approval.", "ui_request_save_version")]
    REQUEST_SAVE_VERSION,
    [AgentOperation("Request human review of a separate copy; this is not approval.")]
    REQUEST_SAVE_COPY,
    [AgentOperation("Request review of deletion of one resolved record; never delete without confirmation.", "ui_review_deletion", "ui_enable_deletion_confirmation", "ui_verify_deletion")]
    REQUEST_DELETE,
    [AgentOperation("Write the exact compiled prompt to the clipboard now through the visible application control, with a fresh receipt.", "ui_copy_compiled_prompt", ModelName = "WRITE_CLIPBOARD_NOW")]
    COPY_TO_CLIPBOARD
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetReference { NONE, NAMED, SELECTED, PREVIOUS_RESULT, ACTIVE_DRAFT, CANDIDATE, OTHER_CANDIDATE }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SemanticOutcome { READ, DRAFT, NEW_DRAFT, READY_LOCAL, READY_PROVIDER, LOCAL_RESPONSE, ADVICE, CLARIFICATION, CANCELLED, PAUSED, RESUMED, REVIEW }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CopyContent { RENDERED, TEMPLATE }
public record SemanticStep(int Id, SemanticOperation Operation, int[] DependsOn);
public record SemanticConstraints(SemanticOperation[] ForbiddenOperations, bool PreserveOriginal, string? Language, string? Format, string[] LiteralClauses, string[] TargetTerms, bool Shortening, string[]? UserProhibitions = null);
public record SemanticInterpretation(SemanticOperation[] Intentions, TargetReference TargetReference, string TargetDescription, Guid? ResolvedTargetId,
    string Query, string RequestedTransformation, SemanticConstraints Constraints, SemanticOutcome DesiredOutcome, SemanticStep[] ProposedSteps,
    string[] MissingInformation, bool RequiresConfirmation, string ExecutionEvidence, string? ProviderId, string Explanation, string[]? ValidationNotes = null,
    SemanticOperation[]? DeferredGoals = null, SemanticOutcome? DeferredOutcome = null, bool ContinuesPendingTask = false)
{
    [JsonIgnore] public TaskGoal? Goal { get; init; }
    [JsonIgnore] public string[]? UserDefinedInputs { get; init; }
    [JsonIgnore] public TaskIntentContract? IntentContract { get; init; }
    [JsonIgnore] public InstructionAuthoring? Authoring { get; init; }
    [JsonIgnore] public WorkflowGoalSnapshot? WorkflowGoals { get; init; }
    [JsonIgnore] public UserInputScope? InputScope { get; init; }
    [JsonIgnore] public string? DeletionEvidence { get; init; }
    [JsonIgnore] public string? ClipboardEvidence { get; init; }
    // Only the trusted interpreter can attach actual read observations. Never model JSON authority.
    [JsonIgnore] public SemanticEntity[] ObservedSources { get; init; } = [];
    [JsonIgnore] public bool Deletes => Has(SemanticOperation.REQUEST_DELETE);
    public bool Has(SemanticOperation operation) => Intentions.Contains(operation);
    public bool RequestsApprovalFor(SemanticOperation operation) => Goal?.ApprovalRequirements.Contains(operation) ?? Has(operation);
    [JsonIgnore] public bool Adapts => Has(SemanticOperation.PROPOSE_ADAPTATION);
    [JsonIgnore] public bool Creates => Has(SemanticOperation.CREATE_DRAFT);
    [JsonIgnore] public bool Executes => Has(SemanticOperation.EXECUTE_LOCAL);
    // Filling an individual input is a valid task even when other inputs remain blank.
    // Only effects that consume the whole prompt require a complete compilation.
    // An explicit UI choice owns this representation. It never grants an effect.
    [JsonIgnore] public CopyContent CopyContent { get; init; } = CopyContent.RENDERED;
    [JsonIgnore] public bool RequiresTemplatePreview => CopyContent == CopyContent.TEMPLATE && (Has(SemanticOperation.COPY_PROMPT) || Has(SemanticOperation.COPY_TO_CLIPBOARD));
    [JsonIgnore] public bool RequiresCompiledOutput => CopyContent == CopyContent.RENDERED && (Has(SemanticOperation.COPY_PROMPT) || Has(SemanticOperation.COPY_TO_CLIPBOARD))
        || Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || Has(SemanticOperation.PREPARE_PROVIDER) || Executes;
    [JsonIgnore] public bool RequiresProvidedValues => Has(SemanticOperation.FILL_VARIABLES)
        && (Goal?.RequestedEffects.Contains(SemanticOperation.FILL_VARIABLES) ?? !RequiresCompiledOutput);
    public Understanding Preparation() => new(Creates ? PreparationIntent.CREATE_PROMPT : Adapts ? PreparationIntent.ADAPT_PROMPT : Executes ? PreparationIntent.RUN_LOCAL
        : Has(SemanticOperation.FILL_VARIABLES) ? PreparationIntent.COMPLETE_PROMPT : Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || Has(SemanticOperation.PREPARE_PROVIDER) || (Has(SemanticOperation.COPY_PROMPT) || Has(SemanticOperation.COPY_TO_CLIPBOARD)) ? PreparationIntent.USE_PROMPT
        : DesiredOutcome is SemanticOutcome.CLARIFICATION or SemanticOutcome.ADVICE ? PreparationIntent.UNKNOWN : PreparationIntent.FIND_PROMPT,
        Query, string.Join(" ", MissingInformation), Intentions.Select(op => op switch { SemanticOperation.PROPOSE_ADAPTATION => PreparationIntent.ADAPT_PROMPT,
            SemanticOperation.CREATE_DRAFT => PreparationIntent.CREATE_PROMPT, SemanticOperation.EXECUTE_LOCAL => PreparationIntent.RUN_LOCAL,
            SemanticOperation.FILL_VARIABLES => PreparationIntent.COMPLETE_PROMPT, _ => PreparationIntent.FIND_PROMPT }).Distinct().ToArray());
    [JsonIgnore] public PromptRequirements Requirements => new(Constraints.Language, Constraints.Format, Constraints.LiteralClauses, Constraints.TargetTerms, Constraints.Shortening);
}
public record SemanticEntity(Guid Id, int Revision, string Title, string Text, bool Truncated, bool Stale = false, string[]? InputNames = null)
{
    public static SemanticEntity From(PromptDocument prompt) => new(prompt.Id, prompt.Revision, prompt.Title, prompt.TemplateText[..Math.Min(2000, prompt.TemplateText.Length)], prompt.TemplateText.Length > 2000, InputNames: prompt.VariableNames);
}
public record SemanticContext(SemanticEntity? Selected, SemanticEntity[] Candidates, SemanticEntity? DraftSource, string? ActiveDraft,
    SemanticInterpretation? LastProposal, SemanticOperation? PendingAction, string Approval, SemanticConstraints? Constraints, Dictionary<string, string>? AuthorizedValues = null,
    Dictionary<string, string>? ActiveDraftValues = null, SemanticInterpretation? PendingPlan = null, Guid? ExplicitTargetId = null, string? PendingRequest = null, string[]? ActiveDraftInputNames = null)
{
    [JsonIgnore] public bool CanContinuePendingTask => PendingPlan is not null && !string.IsNullOrWhiteSpace(PendingRequest);
    [JsonIgnore] public DraftRef? DraftReference { get; init; }
    [JsonIgnore] public WorkflowGoalSnapshot? RefinementWorkflowGoals { get; init; }
    [JsonIgnore] public InstructionAuthoring? RefinementAuthoring { get; init; }
    [JsonIgnore] public ResolvedTaskReference? ResolvedReference { get; init; }
    // A freshly looked-up source is not the item selected in the user's UI.
    [JsonIgnore] public SemanticEntity? ResolvedSource { get; init; }
    [JsonIgnore] public UserInputScope? InputScope { get; init; }
    // Recall hints are not results previously shown to the user and grant no identity binding.
    [JsonIgnore] public SemanticEntity[] CatalogCandidates { get; init; } = [];
    [JsonIgnore] public RetrievalFilter? RetrievalScope { get; init; }
    public (TargetReference Role, SemanticEntity? Entity, bool Unique) CurrentSubject() {
        if (ActiveDraft is not null && DraftSource is null) return (TargetReference.ACTIVE_DRAFT, null, true);
        var entity = DraftSource ?? Selected ?? (Candidates.Length == 1 ? Candidates[0] : null);
        var role = DraftSource is not null ? TargetReference.PREVIOUS_RESULT : Selected is not null ? TargetReference.SELECTED : entity is not null ? TargetReference.CANDIDATE : TargetReference.NONE;
        // An incidental UI selection or other search results do not create a second
        // conversational task artifact. Ambiguous candidates alone still have no default.
        return (role, entity, entity is not null && !entity.Stale);
    }
    public string[] CurrentInputNames() {
        if (ResolvedSource?.InputNames is { } resolvedNames) return resolvedNames;
        var subject = CurrentSubject();
        return (subject.Role is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT ? ActiveDraftInputNames : null) ?? subject.Entity?.InputNames ?? [];
    }
    public static SemanticContext Empty => new(null, [], null, null, null, null, "NONE", null);
    [JsonIgnore] public Guid[] Ids => Candidates.Where(c => !c.Stale).Select(c => c.Id).Concat(Selected is null || Selected.Stale ? [] : new[] { Selected.Id }).Concat(DraftSource is null || DraftSource.Stale ? [] : new[] { DraftSource.Id }).Distinct().ToArray();
}
public interface ISemanticIntentInterpreter
{
    Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct);
}

// Validates typed model output, never classifies natural language. Persistence is deliberately absent.
public static class SemanticPlanValidator
{
    public static readonly IReadOnlyDictionary<SemanticOperation, string[]> Tools =
        SemanticCapabilityCatalog.All.ToDictionary(capability => capability.Operation, capability => capability.ToolNames.ToArray());
    public static void ValidateIntent(SemanticInterpretation plan)
    {
        if (plan.IntentContract is { } intent && (!ReferenceEquals(plan.Goal?.IntentContract, intent) || !intent.Allows(plan))) Reject("INTENT_PLAN_DIVERGED");
    }
    public static void Validate(SemanticInterpretation plan, SemanticContext context, string message, int budget = 16)
    {
        ValidateIntent(plan);
        plan.Authoring?.ValidateFor(plan, message, context.PendingRequest);
        using var measurement = NovaTelemetry.Measure("planning.validation");
        if (plan.Intentions.Length is < 1 or > 12 || plan.Intentions.Distinct().Count() != plan.Intentions.Length || plan.Intentions.Any(op => !Tools.ContainsKey(op))
            || plan.ProposedSteps.Length is < 1 or > 12 || plan.ProposedSteps.Length > budget || !Enum.IsDefined(plan.TargetReference) || !Enum.IsDefined(plan.DesiredOutcome)) Reject("PLAN_SHAPE");
        var seen = new HashSet<int>();
        foreach (var step in plan.ProposedSteps)
        {
            if (!plan.Has(step.Operation) || step.Id < 1 || seen.Contains(step.Id) || step.DependsOn.Any(id => !seen.Contains(id)) || step.DependsOn.Distinct().Count() != step.DependsOn.Length) Reject("PLAN_DEPENDENCY");
            seen.Add(step.Id);
        }
        if (plan.Intentions.Any(op => !plan.ProposedSteps.Any(step => step.Operation == op)) || plan.ProposedSteps.GroupBy(s => s.Operation).Any(g => g.Count() > 1)) Reject("PLAN_COVERAGE");
        if (plan.Constraints.ForbiddenOperations.Any(plan.Has) || plan.AdaptsAndCreates()) Reject("FORBIDDEN_OPERATION");
        if (plan.ContinuesPendingTask && !context.CanContinuePendingTask) Reject("UNVERIFIED_PENDING_TASK");
        if (plan.ResolvedTargetId is { } id && !context.Ids.Contains(id)) Reject("UNVERIFIED_TARGET_ID");
        if (plan.TargetReference == TargetReference.NONE && plan.ResolvedTargetId is not null) Reject("UNEXPECTED_TARGET_ID");
        if (plan.TargetReference == TargetReference.NONE && plan.Has(SemanticOperation.RESOLVE_PROMPT)) Reject("SOURCE_ACCESS_REQUIRES_REFERENCE");
        if (plan.Has(SemanticOperation.SEARCH_PROMPT) && string.IsNullOrWhiteSpace(plan.Query)) Reject("MISSING_SEARCH_QUERY");
        if (plan.TargetReference == TargetReference.SELECTED && plan.ResolvedTargetId is { } selected && selected != context.Selected?.Id) Reject("SELECTED_TARGET_MISMATCH");
        if (plan.TargetReference == TargetReference.CANDIDATE && plan.ResolvedTargetId is { } candidateIdentity && !context.Candidates.Any(c => c.Id == candidateIdentity)) Reject("UNVERIFIED_CANDIDATE_ID");
        if (plan.TargetReference == TargetReference.OTHER_CANDIDATE && plan.ResolvedTargetId is { } otherIdentity) {
            var alternatives = context.Candidates.Where(candidate => !candidate.Stale && candidate.Id != (context.DraftSource?.Id ?? context.Selected?.Id)).DistinctBy(candidate => candidate.Id).ToArray();
            if (alternatives.Length != 1 || alternatives[0].Id != otherIdentity) Reject("UNVERIFIED_OTHER_CANDIDATE");
        }
        if (plan.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT && plan.ResolvedTargetId is { } previous && previous != context.DraftSource?.Id) Reject("CONVERSATION_TARGET_MISMATCH");
        if (plan.TargetReference == TargetReference.CANDIDATE && context.Candidates.Length > 1 && plan.ResolvedTargetId is { } candidateId) {
            var candidate = context.Candidates.Single(c => c.Id == candidateId);
            if ((plan.Goal is null && !message.Contains(candidate.Title, StringComparison.OrdinalIgnoreCase)) || context.Candidates.Count(c => string.Equals(c.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)) > 1) Reject("AMBIGUOUS_TARGET_ID");
        }
        if ((plan.Has(SemanticOperation.REQUEST_SAVE_VERSION) || plan.Has(SemanticOperation.REQUEST_SAVE_COPY)) && !plan.RequiresConfirmation) Reject("SAVE_CONFIRMATION_REQUIRED");
        if (plan.Deletes) {
            if (!plan.RequiresConfirmation || plan.DesiredOutcome != SemanticOutcome.REVIEW) Reject("DELETE_CONFIRMATION_REQUIRED");
            if (plan.Intentions.Any(operation => operation is not (SemanticOperation.SEARCH_PROMPT or SemanticOperation.RESOLVE_PROMPT or SemanticOperation.OPEN_PROMPT or SemanticOperation.REQUEST_DELETE))) Reject("DELETE_EFFECT_CONFLICT");
            var names = context.Selected?.InputNames ?? context.DraftSource?.InputNames ?? context.ActiveDraftInputNames ?? [];
            var directive = GroundedVariableResolver.InstructionDirective(message, names, plan.InputScope);
            var pending = plan.ContinuesPendingTask && context.PendingPlan is {} previousDeletion
                && (previousDeletion.Deletes || previousDeletion.DeferredGoals?.Contains(SemanticOperation.REQUEST_DELETE) == true) ? context.PendingRequest : null;
            if (!DirectEvidence(directive, plan.DeletionEvidence ?? "")
                && (pending is null || !DirectEvidence(GroundedVariableResolver.InstructionDirective(pending, names), plan.DeletionEvidence ?? ""))) Reject("DELETE_NOT_REQUESTED");
            RequireDependency(plan, SemanticOperation.REQUEST_DELETE, SemanticOperation.RESOLVE_PROMPT);
        }
        if (plan.Constraints.PreserveOriginal && plan.Has(SemanticOperation.REQUEST_SAVE_VERSION)) Reject("ORIGINAL_PROTECTED");
        var scopedInputNames = plan.InputScope?.SourceId is { } scopeSource
            ? context.Candidates.Concat(new[] { context.Selected, context.DraftSource, context.ResolvedSource }.OfType<SemanticEntity>())
                .FirstOrDefault(entity => entity.Id == scopeSource)?.InputNames
            : context.ActiveDraftInputNames;
        var executionDirective = plan.InputScope is null ? message : GroundedVariableResolver.InstructionDirective(message,
            scopedInputNames ?? context.CurrentInputNames().Concat(plan.InputScope.Names).Distinct().ToArray(), plan.InputScope);
        if (plan.Executes && (plan.DesiredOutcome != SemanticOutcome.LOCAL_RESPONSE || !DirectEvidence(executionDirective, plan.ExecutionEvidence)
            || context.AuthorizedValues?.Values.Any(value => value.Contains(plan.ExecutionEvidence, StringComparison.Ordinal)) == true)) Reject("LOCAL_EXECUTION_NOT_REQUESTED");
        var clipboardDirective = GroundedVariableResolver.InstructionDirective(message, scopedInputNames ?? context.CurrentInputNames().Concat(plan.InputScope?.Names ?? []).Distinct().ToArray(), plan.InputScope);
        var pendingClipboardDirective = plan.ContinuesPendingTask && context.PendingPlan is {} priorClipboard
            && (priorClipboard.Has(SemanticOperation.COPY_TO_CLIPBOARD) || priorClipboard.DeferredGoals?.Contains(SemanticOperation.COPY_TO_CLIPBOARD) == true)
            && context.PendingRequest is {} pendingClipboardRequest
                ? GroundedVariableResolver.InstructionDirective(pendingClipboardRequest, scopedInputNames ?? context.CurrentInputNames()) : "";
        if (plan.Has(SemanticOperation.COPY_TO_CLIPBOARD) && ((!DirectEvidence(clipboardDirective, plan.ClipboardEvidence ?? "") && !DirectEvidence(pendingClipboardDirective, plan.ClipboardEvidence ?? ""))
            || context.AuthorizedValues?.Values.Any(value => value.Contains(plan.ClipboardEvidence ?? "", StringComparison.Ordinal)) == true)) Reject("CLIPBOARD_NOT_REQUESTED");
        RequireDependency(plan, SemanticOperation.COPY_TO_CLIPBOARD, SemanticOperation.COMPILE_PROMPT);
        if (plan.ProviderId is { } provider && !ProviderRegistry.Providers.Any(p => p.ProviderId == provider)) Reject("UNKNOWN_PROVIDER");
        RequireDependency(plan, SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.RESOLVE_PROMPT);
        if (plan.AdaptsOrCreates()) {
            if (!plan.Has(SemanticOperation.VALIDATE_DRAFT) || !plan.Has(SemanticOperation.COMPILE_PROMPT)) Reject("DRAFT_VALIDATION_REQUIRED");
            RequireDependency(plan, SemanticOperation.VALIDATE_DRAFT, plan.Adapts ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.CREATE_DRAFT);
            RequireDependency(plan, SemanticOperation.COMPILE_PROMPT, SemanticOperation.VALIDATE_DRAFT);
        }
        RequireDependency(plan, SemanticOperation.OPEN_PROMPT, SemanticOperation.RESOLVE_PROMPT);
        if (plan.Executes) { RequireDependency(plan, SemanticOperation.EXECUTE_LOCAL, SemanticOperation.PREPARE_LOCAL_EXECUTION); RequireDependency(plan, SemanticOperation.PREPARE_LOCAL_EXECUTION, SemanticOperation.COMPILE_PROMPT); }
        if (plan.DesiredOutcome == SemanticOutcome.READY_LOCAL && (plan.Executes || !plan.Has(SemanticOperation.PREPARE_LOCAL_EXECUTION))) Reject("PREPARE_IS_NOT_EXECUTE");
        if (plan.DesiredOutcome == SemanticOutcome.READY_PROVIDER && (plan.Executes || !plan.Has(SemanticOperation.PREPARE_PROVIDER))) Reject("PROVIDER_PREPARATION_REQUIRED");
        if (plan.DesiredOutcome == SemanticOutcome.READ && plan.AdaptsOrCreates()) Reject("READ_HAS_WRITE_PROPOSAL");
        if (plan.DesiredOutcome == SemanticOutcome.DRAFT && !plan.Adapts) Reject("DRAFT_REQUIRES_ADAPTATION");
        if (plan.DesiredOutcome == SemanticOutcome.NEW_DRAFT && !plan.Creates) Reject("NEW_DRAFT_REQUIRES_CREATE");
        if (plan.DesiredOutcome == SemanticOutcome.LOCAL_RESPONSE && !plan.Executes) Reject("LOCAL_RESPONSE_REQUIRES_EXECUTION");
        if (plan.Adapts && string.IsNullOrWhiteSpace(plan.RequestedTransformation)) Reject("MISSING_TRANSFORMATION");
        if (plan.DesiredOutcome == SemanticOutcome.ADVICE && (plan.AdaptsOrCreates() || plan.Executes)) Reject("ADVICE_HAS_EFFECTS");
        if (plan.MissingInformation.Length > 0 && plan.DesiredOutcome != SemanticOutcome.CLARIFICATION) Reject("UNRESOLVED_INFORMATION");
    }
    private static bool AdaptsAndCreates(this SemanticInterpretation plan) => plan.Adapts && plan.Creates;
    private static bool AdaptsOrCreates(this SemanticInterpretation plan) => plan.Adapts || plan.Creates;
    private static void RequireDependency(SemanticInterpretation plan, SemanticOperation operation, SemanticOperation dependency)
    {
        var step = plan.ProposedSteps.SingleOrDefault(s => s.Operation == operation);
        if (step is null) return;
        var required = plan.ProposedSteps.SingleOrDefault(s => s.Operation == dependency);
        if (required is null || !Ancestors(plan, step).Contains(required.Id)) Reject("MISSING_" + dependency);
    }
    private static HashSet<int> Ancestors(SemanticInterpretation plan, SemanticStep step)
    {
        var result = new HashSet<int>(step.DependsOn);
        foreach (var id in step.DependsOn) result.UnionWith(Ancestors(plan, plan.ProposedSteps.Single(s => s.Id == id)));
        return result;
    }
    public static void AuthorizeTool(SemanticInterpretation plan, string tool)
    {
        if (!ToolAllowed(plan, tool)) Reject("TOOL_OUTSIDE_PLAN");
    }
    public static bool ToolAllowed(SemanticInterpretation plan, string tool)
    {
        if (tool is "ui_show_template" or "ui_copy_template" && !plan.RequiresTemplatePreview) return false;
        if (tool is "ui_show_compiled_prompt" or "ui_copy_compiled_prompt" && plan.RequiresTemplatePreview && !plan.RequiresCompiledOutput) return false;
        if (plan.IntentContract is { } intent && (!ReferenceEquals(plan.Goal?.IntentContract, intent) || !intent.Allows(plan))) return false;
        // A later model/tool decision cannot enlarge the process-owned task contract.
        if (plan.Goal is { } goal && plan.DesiredOutcome != SemanticOutcome.CLARIFICATION && !goal.AllowsPlan(plan)) return false;
        // Shared dependency aliases must not re-authorize a specifically prohibited effect.
        // For example RESOLVE permits opening an ID, but a user may request search results only.
        var effect = tool switch { "ui_open_prompt" => SemanticOperation.OPEN_PROMPT, "ui_prepare_update" => SemanticOperation.PROPOSE_ADAPTATION,
            "ui_prepare_creation" => SemanticOperation.CREATE_DRAFT, "ui_fill_variable" => SemanticOperation.FILL_VARIABLES,
            "ui_execute_local" => SemanticOperation.EXECUTE_LOCAL, "ui_copy_compiled_prompt" or "ui_copy_template" => SemanticOperation.COPY_TO_CLIPBOARD, "ui_request_save_version" => SemanticOperation.REQUEST_SAVE_VERSION, _ => (SemanticOperation?)null };
        if (effect is { } prohibited && plan.Constraints.ForbiddenOperations.Contains(prohibited)) return false;
        if (tool == "ui_show_template") return plan.RequiresTemplatePreview;
        if (tool == "ui_copy_template") return plan.RequiresTemplatePreview && plan.Has(SemanticOperation.COPY_TO_CLIPBOARD);
        if (tool is "ui_navigate_library" or "ui_read_visible_state" or "ui_verify_prompt_state" or "finish" or "ask_clarification") return true;
        // Compilation/validation are mandatory domain checks, not extra user effects.
        if (tool is "compile_prompt" or "validate_prompt") return true;
        if (tool == "ui_fill_variable" && plan.Goal is { } inputGoal && inputGoal.RequestedEffects.Any(operation => operation is SemanticOperation.FILL_VARIABLES
                or SemanticOperation.COPY_PROMPT or SemanticOperation.COPY_TO_CLIPBOARD or SemanticOperation.PREPARE_PROVIDER or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.EXECUTE_LOCAL)
            && plan.Has(SemanticOperation.COMPILE_PROMPT)
            && !plan.Constraints.ForbiddenOperations.Contains(SemanticOperation.FILL_VARIABLES)) return true;
        return plan.Intentions.Any(op => Tools[op].Contains(tool));
    }
    private static void Reject(string code) => throw new PreparationToolException(code, "semantic_plan", "El plan semántico no pasó la validación: " + code + ". No se ejecutaron sus efectos.");

    // Syntactic evidence boundary only. It does not infer an intent from any word or verb.
    public static bool DirectEvidence(string message, string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return false;
        return DirectiveFragments(message).Any(fragment => fragment.Contains(evidence, StringComparison.Ordinal));
    }
    // A quoted source name is data; it must not erase the directives on either side.
    public static string[] DirectiveFragments(string message)
    {
        var fragments = new List<string>(); char? closing = null; var start = 0;
        for (var index = 0; index < message.Length; index++) {
            var character = message[index];
            if (closing is not null) {
                if (closing == '"' && character == '\\' && index + 1 < message.Length) { index++; continue; }
                if (character == closing) { closing = null; start = index + 1; }
            }
            else if (character is '«' or '“' or '"') {
                if (index > start) fragments.Add(message[start..index]);
                closing = character == '«' ? '»' : character == '“' ? '”' : '"';
            }
        }
        if (closing is null && start < message.Length) fragments.Add(message[start..]);
        return fragments.ToArray();
    }
}
