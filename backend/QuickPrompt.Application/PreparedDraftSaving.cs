using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public interface IPreparedDraftSaving
{
    PromptDocument Save(Guid id, SavePreparedInput input);
}

// Saving has no live-control or inference dependency. The store owns the transaction.
public sealed class PreparedDraftSaving(IPreparedDraftStore drafts, IAgentRunStore runs) : IPreparedDraftSaving
{
    public PromptDocument Save(Guid id, SavePreparedInput input)
    {
        if (!input.Confirmed) throw new ValidationException("Guardar en la biblioteca requiere una acción explícita.");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var saved = drafts.Save(id, input.UpdateSource);
        if (runs.Get(id) is { } run)
        {
            var durations = new Dictionary<string, double>(run.StageDurations ?? []) { ["Approval"] = timer.Elapsed.TotalMilliseconds };
            runs.Put(run with { Approval = input.UpdateSource ? "APPROVED_SOURCE_VERSION" : "APPROVED_COPY_OR_REUSE", StageDurations = durations, UpdatedAt = DateTime.UtcNow });
        }
        return saved;
    }
}
