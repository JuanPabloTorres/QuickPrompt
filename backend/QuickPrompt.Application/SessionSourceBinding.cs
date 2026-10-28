using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record SourceContextInput(Guid PromptId, int Revision, Dictionary<string, string>? Values = null, bool AllowStaleContext = false);

/// <summary>One source-binding rule for local and private conversations. A binding never grants approval.</summary>
public sealed partial class AgentSessions
{
    public void BindSource(IPromptCatalog library, Guid id, SourceContextInput input)
    {
        if (id == Guid.Empty || input.Values?.Count > 60 || input.Values?.Any(value => value.Value is null || value.Value.Length > 8000) == true)
            throw new ValidationException("Contexto inválido.");
        var source = library.Get(input.PromptId);
        if (input.Revision < 1 || source.IsArchived || source.Revision != input.Revision && !input.AllowStaleContext) throw new ConflictException("El origen cambió.");
        if (input.Values?.Any(value => !source.VariableNames.Contains(value.Key)) == true) throw new ValidationException("Variable desconocida.");
        storage.Update(id, previous =>
        {
            var prompt = source;
            var same = previous?.Source?.Id == prompt.Id && previous.Source.Revision == prompt.Revision;
            var stale = prompt.Revision != input.Revision;
            if (stale) prompt = previous?.Source?.Id == prompt.Id && previous.Source.Revision == input.Revision ? previous.Source
                : prompt with { Revision = input.Revision, TemplateText = "", Defaults = new Dictionary<string, string>(), VariableNames = [] };
            return new(id, prompt, previous?.Draft, same ? previous!.Values.Concat(input.Values ?? []).GroupBy(value => value.Key).ToDictionary(group => group.Key, group => group.Last().Value)
                : input.Values ?? [], DateTime.UtcNow, previous?.Candidates, previous?.LastProposal, previous?.PendingAction, "NOT_GRANTED", stale, previous?.PendingPlan, previous?.PendingRequest)
            { DraftRevision = previous?.DraftRevision ?? 1, PendingCommand = same && !stale ? previous?.PendingCommand : null };
        });
    }
}
