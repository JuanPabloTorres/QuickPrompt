using System.Collections.Immutable;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Product commands, distinct from execution steps. No phrase matching is used:
// names are decoded only from validated model output or typed application input.
public sealed record UserActionSpec(string Name, SemanticOperation Operation, string Description, ImmutableArray<CompletionCondition> Results);
public static class UserActions
{
    public static readonly ImmutableArray<UserActionSpec> All = [
        new("find_prompt", SemanticOperation.SEARCH_PROMPT, "Locate the requested saved instruction by topic or title. Do not author its future task.", []),
        new("read_prompt", SemanticOperation.OPEN_PROMPT, "Show the requested existing instruction unchanged. Reading is not advice, authoring or preparation.", [CompletionCondition.SOURCE_VISIBLE]),
        new("edit_prompt", SemanticOperation.PROPOSE_ADAPTATION, "Change the SAME referenced reusable instruction's wording. The result is an unsaved revision, not an independent new prompt. Do not save, copy or execute it as conveniences.", [CompletionCondition.DRAFT_CHANGED, CompletionCondition.CONTENT_VALID]),
        new("new_prompt", SemanticOperation.CREATE_DRAFT, "Create a DISTINCT reusable instruction only when a new independent prompt is requested. A requested change to the same template uses edit_prompt.", [CompletionCondition.NEW_DRAFT_VISIBLE, CompletionCondition.CONTENT_VALID]),
        new("fill_inputs", SemanticOperation.FILL_VARIABLES, "Apply supplied CURRENT case values without rewriting the instruction. Do not add for fields handled as preparation dependencies.", [CompletionCondition.VALUES_APPLIED]),
        new("preview_copy", SemanticOperation.COPY_PROMPT, "Leave compiled text ready to copy WITHOUT writing the clipboard. This result must be requested, not a convenience of authoring.", [CompletionCondition.COMPILED_VISIBLE]),
        new("write_clipboard", SemanticOperation.COPY_TO_CLIPBOARD, "Write the clipboard NOW only when the user requests actual copying. Ready-to-copy requests use preview_copy and do not write.", [CompletionCondition.COMPILED_VISIBLE, CompletionCondition.CLIPBOARD_WRITTEN]),
        new("prepare_local", SemanticOperation.PREPARE_LOCAL_EXECUTION, "Prepare a prompt for local AI without executing its future task.", [CompletionCondition.COMPILED_VISIBLE, CompletionCondition.DESTINATION_READY]),
        new("run_local", SemanticOperation.EXECUTE_LOCAL, "Execute the prompt's final task locally NOW, only when explicitly requested. Future instructions inside a prompt are not current execution requests.", [CompletionCondition.COMPILED_VISIBLE, CompletionCondition.LOCAL_RESPONSE]),
        new("prepare_destination", SemanticOperation.PREPARE_PROVIDER, "Prepare the requested named destination without sending. Identity comes from the supplied provider catalog.", [CompletionCondition.COMPILED_VISIBLE, CompletionCondition.DESTINATION_READY]),
        new("save_version", SemanticOperation.REQUEST_SAVE_VERSION, "The user requests SAVING changes into the existing library record. Never add merely to review an unsaved edit: review/approval is a later domain phase, not a reason to invent a save goal.", [CompletionCondition.APPROVED_WRITE]),
        new("save_copy", SemanticOperation.REQUEST_SAVE_COPY, "The user requests SAVING a separate library copy. Never add to mere creation/editing: the domain owns any later approval.", [CompletionCondition.APPROVED_WRITE]),
        new("delete_prompt", SemanticOperation.REQUEST_DELETE, "The user requests DELETING the saved record. The domain will require exact approval before deletion.", [CompletionCondition.APPROVED_DELETION]),
        new("explain", SemanticOperation.EXPLAIN, "Answer an actual advice/explanation question without a changed proposal. Showing an existing prompt uses read_prompt.", [CompletionCondition.EXPLANATION_READY]),
        new("ask", SemanticOperation.ASK_CLARIFICATION, "Clarify essential undefined task information; never optional preferences or the future case's facts. Actual source ambiguity and missing input fields are resolved by the executor.", []),
        new("cancel", SemanticOperation.CANCEL, "Cancel the current task.", []),
        new("pause", SemanticOperation.PAUSE, "Pause the current task.", []),
        new("resume", SemanticOperation.RESUME, "Resume the current task under its existing authority.", [])
    ];
    public static UserActionSpec Named(string name) => All.SingleOrDefault(a => a.Name == name)
        ?? throw new ValidationException("Acción fuera del catálogo de producto.");
    public static UserActionSpec For(SemanticOperation operation) => All.SingleOrDefault(a => a.Operation == operation)
        ?? throw new ValidationException("La operación no es una acción de usuario.");
}

// Accepted syntax/declared scope is not semantic proof. Real-provider evaluation
// against the original request qualifies meaning independently of this snapshot.
public sealed class UserActionRequest
{
    public string UserRequest { get; }
    public string RequestHash { get; }
    public ImmutableArray<string> Actions { get; }
    public ImmutableArray<SemanticOperation> Effects { get; }
    public ImmutableArray<CompletionCondition> Results { get; }
    public UserActionRequest(string request, IEnumerable<string> actions)
    {
        if (string.IsNullOrWhiteSpace(request)) throw new ValidationException("Falta la petición literal.");
        var specs = actions.Select(UserActions.Named).ToArray();
        if (specs.Length is < 1 or > 8 || specs.Select(s => s.Operation).Distinct().Count() != specs.Length)
            throw new ValidationException("El encargo contiene acciones duplicadas o fuera de límites.");
        UserRequest = request; RequestHash = LiveRuns.Hash(request);
        Actions = specs.Select(s => s.Name).ToImmutableArray(); Effects = specs.Select(s => s.Operation).ToImmutableArray();
        Results = specs.SelectMany(s => s.Results).Distinct().ToImmutableArray();
    }
    // Typed domain consumers/tests may already know the action. The model wire
    // accepts product names only; it cannot call this factory or grant permission.
    public static UserActionRequest FromOperations(string request, IEnumerable<SemanticOperation> actions)
        => new(request, actions.Select(a => UserActions.For(a).Name));
}

