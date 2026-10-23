using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record EditorDraftRequest(Guid? PromptId, int? Revision, string Title, string TemplateText,
    Dictionary<string, string> Values, Guid? SessionId = null);

public interface IEditorDraftPreparation
{
    PreparedDraft Prepare(EditorDraftRequest input);
}

// Explicit editor input only. Existing owners retain evaluation, receipts and memory.
public sealed class EditorDraftPreparation(IPromptCatalog library, IPromptEvaluator evaluator,
    IPreparedDraftStore drafts, AgentSessions sessions) : IEditorDraftPreparation
{
    public PreparedDraft Prepare(EditorDraftRequest input)
    {
        EditorTools.ValidateValues(input.TemplateText, input.Values);
        if ((input.PromptId is null) != (input.Revision is null) || input.SessionId == Guid.Empty) throw new ValidationException("Identidad del editor inválida.");
        var source = input.PromptId is { } sourceId ? library.Get(sourceId) : null;
        if (source is not null && (source.Revision != input.Revision || source.IsArchived)) throw new ConflictException("El origen cambió.");
        var id = Guid.NewGuid(); var request = new PrepareInput(id, "Preparación explícita desde el editor.", SourcePromptId: source?.Id, SourceRevision: source?.Revision, Values: input.Values, SessionId: input.SessionId);
        var result = evaluator.Evaluate(request, new(source is null ? PreparationIntent.CREATE_PROMPT : PreparationIntent.ADAPT_PROMPT, "", ""),
            new(source is null ? PreparationDecision.CREATE_NEW : PreparationDecision.ADAPT_EXISTING, source?.Id, input.Title, source?.Description ?? "", input.TemplateText, [], "", "Borrador del editor; original conservado."),
            source is null ? [] : [new(source, 10, "Origen seleccionado")], input.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED")).ToArray())
            with { SessionId = input.SessionId };
        drafts.Put(result, LiveRuns.Hash(request));
        if (input.SessionId is { } sessionId) {
            var previous = sessions.Get(sessionId);
            var searchContext = (previous?.Candidates ?? []).Select(candidate => source is not null && candidate.Id == source.Id ? source : candidate).Take(6).ToArray();
            sessions.ObserveDraft(new(sessionId, source ?? previous?.Source, result, source is null ? previous?.Values ?? [] : input.Values, DateTime.UtcNow,
                searchContext));
        }
        return result;
    }
}
