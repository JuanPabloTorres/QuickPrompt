using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Session state has one checkpoint owner within task planning; domain effects remain elsewhere.
internal sealed class TaskConversationContext(AgentSessions sessions, TaskSourceResolver sources)
{
    public SemanticContext Restore(Guid sessionId, Guid? selectedId = null, int? revision = null)
    {
        var session = sessions.Get(sessionId);
        var selected = selectedId is { } id ? sources.Read(id, revision) : session?.UnsavedDraft is not null ? null : session?.Source;
        var candidates = (session?.Candidates ?? []).Take(6).ToArray();
        var draft = session?.Draft;
        SemanticEntity? draftSource = null;
        if (draft?.SourceId is { } source) {
            var snapshot = draft.Candidates.SingleOrDefault(c => c.Prompt.Id == source)?.Prompt ?? session?.Candidates?.SingleOrDefault(p => p.Id == source)
                ?? (session?.Source?.Id == source && session.Source.Revision == draft.SourceRevision ? session.Source : null);
            if (snapshot is not null) draftSource = sources.Snapshot(snapshot);
            else {
                try { draftSource = sources.Snapshot(sources.Read(source, draft.SourceRevision)); }
                catch (Exception error) when (error is MissingPromptException or ConflictException or ValidationException) { draftSource = new(source, draft.SourceRevision ?? 1, draft.Title, "", false, true); }
            }
        }
        return new(selected is null ? null : sources.Snapshot(selected), candidates.Select(sources.Snapshot).ToArray(), draftSource, draft?.TemplateText[..Math.Min(2500, draft.TemplateText.Length)],
            session?.LastProposal, session?.PendingAction, session?.Approval ?? "NONE", session?.PendingPlan?.Constraints ?? session?.LastProposal?.Constraints,
            session?.Source?.Id == selected?.Id && session?.Source?.Revision == selected?.Revision ? session?.Values : null, draft?.Values.ToDictionary(v => v.Name, v => v.Value), session?.PendingPlan, selectedId, session?.PendingRequest, draft?.Variables.Select(v => v.Name).ToArray()) { DraftReference = session?.WorkingDraftReference };
    }
    public void Checkpoint(Guid sessionId, SemanticResolution resolution, bool sourceLookupRequested, string? pendingRequest = null)
    {
        var plan = resolution.Interpretation; var context = resolution.Context; var source = resolution.Source; var candidates = resolution.Candidates;
        var previous = sessions.Get(sessionId);
        var sameSource = source is null || source.Id == previous?.Source?.Id && source.Revision == previous.Source.Revision;
        var selectedPreviousResult = context.ExplicitTargetId is { } chosen && previous?.Candidates?.Any(candidate => candidate.Id == chosen) == true
            || plan.ContinuesPendingTask && source is not null && previous?.Candidates?.Any(candidate => candidate.Id == source.Id) == true;
        var searchContext = sourceLookupRequested && !selectedPreviousResult
            ? candidates.Where(candidate => candidate.Score >= 2).Select(candidate => candidate.Prompt).Take(6).ToArray()
            : previous?.Candidates ?? [];
        if (source is not null) searchContext = searchContext.Select(candidate => candidate.Id == source.Id ? source : candidate).ToArray();
        sessions.Put(new(sessionId, source ?? previous?.Source, sameSource ? previous?.Draft : null, sameSource ? previous?.Values ?? [] : [], DateTime.UtcNow,
            searchContext, previous?.LastProposal, plan.ProposedSteps.FirstOrDefault()?.Operation, "NOT_GRANTED", source is null && previous?.SourceStale == true, plan, pendingRequest ?? previous?.PendingRequest) { DraftRevision = sameSource ? previous?.DraftRevision ?? 1 : 1, PendingCommand = plan.ContinuesPendingTask ? previous?.PendingCommand : null });
    }
}
