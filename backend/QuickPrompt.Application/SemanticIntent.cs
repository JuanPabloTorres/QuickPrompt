using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SemanticOperation { SEARCH_PROMPT, RESOLVE_PROMPT, OPEN_PROMPT, PROPOSE_ADAPTATION, CREATE_DRAFT, VALIDATE_DRAFT, FILL_VARIABLES, COMPILE_PROMPT, PREPARE_LOCAL_EXECUTION, EXECUTE_LOCAL, PREPARE_PROVIDER, COPY_PROMPT, EXPLAIN, ASK_CLARIFICATION, CANCEL, PAUSE, RESUME, REQUEST_SAVE_VERSION, REQUEST_SAVE_COPY }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetReference { NONE, NAMED, SELECTED, PREVIOUS_RESULT, ACTIVE_DRAFT, CANDIDATE, OTHER_CANDIDATE }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SemanticOutcome { READ, DRAFT, NEW_DRAFT, READY_LOCAL, READY_PROVIDER, LOCAL_RESPONSE, ADVICE, CLARIFICATION, CANCELLED, PAUSED, RESUMED, REVIEW }
public record SemanticStep(int Id, SemanticOperation Operation, int[] DependsOn);
public record SemanticConstraints(SemanticOperation[] ForbiddenOperations, bool PreserveOriginal, string? Language, string? Format, string[] LiteralClauses, string[] TargetTerms, bool Shortening, string[]? UserProhibitions = null);
public record SemanticInterpretation(SemanticOperation[] Intentions, TargetReference TargetReference, string TargetDescription, Guid? ResolvedTargetId,
    string Query, string RequestedTransformation, SemanticConstraints Constraints, SemanticOutcome DesiredOutcome, SemanticStep[] ProposedSteps,
    string[] MissingInformation, bool RequiresConfirmation, string ExecutionEvidence, string? ProviderId, string Explanation, string[]? ValidationNotes = null,
    SemanticOperation[]? DeferredGoals = null, SemanticOutcome? DeferredOutcome = null, bool ContinuesPendingTask = false)
{
    public bool Has(SemanticOperation operation) => Intentions.Contains(operation);
    [JsonIgnore] public bool Adapts => Has(SemanticOperation.PROPOSE_ADAPTATION);
    [JsonIgnore] public bool Creates => Has(SemanticOperation.CREATE_DRAFT);
    [JsonIgnore] public bool Executes => Has(SemanticOperation.EXECUTE_LOCAL);
    public Understanding Preparation() => new(Creates ? PreparationIntent.CREATE_PROMPT : Adapts ? PreparationIntent.ADAPT_PROMPT : Executes ? PreparationIntent.RUN_LOCAL
        : Has(SemanticOperation.FILL_VARIABLES) ? PreparationIntent.COMPLETE_PROMPT : Has(SemanticOperation.PREPARE_LOCAL_EXECUTION) || Has(SemanticOperation.PREPARE_PROVIDER) || Has(SemanticOperation.COPY_PROMPT) ? PreparationIntent.USE_PROMPT
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
    public static readonly IReadOnlyDictionary<SemanticOperation, string[]> Tools = new Dictionary<SemanticOperation, string[]> {
        [SemanticOperation.SEARCH_PROMPT] = ["search_prompts", "ui_search_prompt"], [SemanticOperation.RESOLVE_PROMPT] = ["resolve_prompt", "ui_open_prompt", "ui_search_prompt", "ui_load_active_draft"],
        [SemanticOperation.OPEN_PROMPT] = ["open_prompt", "open_working_draft", "ui_open_prompt"], [SemanticOperation.PROPOSE_ADAPTATION] = ["propose_prompt_update", "revise_prompt_once", "ui_open_editor", "ui_prepare_update"],
        [SemanticOperation.CREATE_DRAFT] = ["propose_new_prompt", "revise_prompt_once", "ui_open_create_prompt", "ui_prepare_creation"], [SemanticOperation.VALIDATE_DRAFT] = ["validate_prompt", "ui_verify_prompt_state"],
        [SemanticOperation.FILL_VARIABLES] = ["resolve_variable_values", "ui_fill_variable"], [SemanticOperation.COMPILE_PROMPT] = ["compile_prompt", "ui_show_compiled_prompt"],
        [SemanticOperation.PREPARE_LOCAL_EXECUTION] = ["prepare_local_execution", "ui_show_compiled_prompt", "ui_open_provider_picker"],
        [SemanticOperation.EXECUTE_LOCAL] = ["execute_local_llm", "ui_execute_local"], [SemanticOperation.PREPARE_PROVIDER] = ["prepare_provider_transfer", "ui_open_provider_picker"],
        [SemanticOperation.COPY_PROMPT] = ["compile_prompt", "ui_show_compiled_prompt"], [SemanticOperation.EXPLAIN] = [], [SemanticOperation.ASK_CLARIFICATION] = ["ask_clarification"],
        [SemanticOperation.CANCEL] = [], [SemanticOperation.PAUSE] = [], [SemanticOperation.RESUME] = [],
        [SemanticOperation.REQUEST_SAVE_VERSION] = ["ui_request_save_version"], [SemanticOperation.REQUEST_SAVE_COPY] = [] };
    public static void Validate(SemanticInterpretation plan, SemanticContext context, string message, int budget = 16)
    {
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
        if (plan.ContinuesPendingTask && (context.PendingPlan is null || string.IsNullOrWhiteSpace(context.PendingRequest))) Reject("UNVERIFIED_PENDING_TASK");
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
            if (!message.Contains(candidate.Title, StringComparison.OrdinalIgnoreCase) || context.Candidates.Count(c => string.Equals(c.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)) > 1) Reject("AMBIGUOUS_TARGET_ID");
        }
        if ((plan.Has(SemanticOperation.REQUEST_SAVE_VERSION) || plan.Has(SemanticOperation.REQUEST_SAVE_COPY)) && !plan.RequiresConfirmation) Reject("SAVE_CONFIRMATION_REQUIRED");
        if (plan.Constraints.PreserveOriginal && plan.Has(SemanticOperation.REQUEST_SAVE_VERSION)) Reject("ORIGINAL_PROTECTED");
        if (plan.Executes && (plan.DesiredOutcome != SemanticOutcome.LOCAL_RESPONSE || !DirectEvidence(message, plan.ExecutionEvidence)
            || context.AuthorizedValues?.Values.Any(value => value.Contains(plan.ExecutionEvidence, StringComparison.Ordinal)) == true)) Reject("LOCAL_EXECUTION_NOT_REQUESTED");
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
        if (tool is "ui_navigate_library" or "ui_read_visible_state" or "ui_verify_prompt_state" or "finish" or "ask_clarification") return true;
        // Compilation/validation are mandatory domain checks, not extra user effects.
        if (tool is "compile_prompt" or "validate_prompt") return true;
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
