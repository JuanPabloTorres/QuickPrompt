namespace QuickPrompt.Nova.Application;

// Task-scoped observations, owned by RunVisible. No new session store or model state.
public record UiTaskMemory(string OriginalRequest, string[] Clarifications, UiObservedEffect[] Receipts,
    GoalCompletion? Verification = null, int OmittedReceipts = 0, string Approval = "NOT_GRANTED", bool EffectCommitted = false);

public record UiObservedEffect(int Sequence, string ToolName, string Mode, Guid? PromptId, int? Revision,
    string? Title, string? TemplateHash, Dictionary<string, string> WrittenValues, string[] RemovedValues,
    bool DraftChanged, bool CompiledReady, string? CompiledHash, bool ResponseComplete, string? ProviderId)
{
    public Guid? ClipboardOperationId { get; init; }
    // Called only AFTER executor checks pass. Retain observed changes, never proposed arguments.
    public static UiObservedEffect Observe(int sequence, string tool, UiVisibleState before, UiVisibleState after)
        => new(sequence, tool, after.Mode, after.PromptId, after.Revision, after.Title,
            after.TemplateText is null ? null : LiveRuns.Hash(after.TemplateText),
            after.Values.Where(value => !before.Values.TryGetValue(value.Key, out var old) || old != value.Value)
                .ToDictionary(value => value.Key, value => value.Value),
            before.Values.Keys.Except(after.Values.Keys, StringComparer.Ordinal).ToArray(),
            after.DraftChanged, after.CompiledReady,
            after.CompiledText is null ? null : LiveRuns.Hash(after.CompiledText), after.ResponseComplete, after.ProviderId) {
                ClipboardOperationId = tool is "ui_copy_compiled_prompt" or "ui_copy_template" ? after.Clipboard?.OperationId : null
            };
}

// The model sees a projection; the full state remains with the executor. Exact values are never truncated.
public record UiAgentObservation(string Mode, UiPrompt[] Candidates, Guid? PromptId, int? Revision, string? Title,
    string? TemplateExcerpt, bool TemplateExcerptTruncated, string? TemplateHash,
    Dictionary<string, string> Values, string[] VariableNames, bool DraftChanged,
    bool CompiledReady, string? CompiledExcerpt, bool CompiledExcerptTruncated, string? CompiledHash,
    string? ResponseExcerpt, bool ResponseExcerptTruncated, bool ResponseComplete,
    bool ProviderOpen, string? ProviderId, string? Error)
{
    public static UiAgentObservation From(UiVisibleState state)
        => new(state.Mode, state.Candidates, state.PromptId, state.Revision, state.Title,
            Excerpt(state.TemplateText, 2500), state.TemplateText?.Length > 2500,
            state.TemplateText is null ? null : LiveRuns.Hash(state.TemplateText),
            new(state.Values), state.VariableNames.ToArray(), state.DraftChanged, state.CompiledReady,
            Excerpt(state.CompiledText, 2500), state.CompiledText?.Length > 2500,
            state.CompiledText is null ? null : LiveRuns.Hash(state.CompiledText),
            Excerpt(state.Response, 1000), state.Response?.Length > 1000, state.ResponseComplete,
            state.ProviderOpen, state.ProviderId, state.Error);

    private static string? Excerpt(string? text, int limit) => text is null ? null : text[..Math.Min(text.Length, limit)];
}
