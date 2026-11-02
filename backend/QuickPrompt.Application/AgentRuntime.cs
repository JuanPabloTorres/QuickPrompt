using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentRunState { RECEIVE, INTERPRET, RETRIEVE, PLAN, EXECUTE_TOOLS, COMPOSE, VALIDATE, READY, NEEDS_CLARIFICATION, AWAITING_APPROVAL, FAILED, CANCELLED, BLOCKED }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentIntent { FIND, OPEN, CREATE, ADAPT, IMPROVE, FILL, COMPILE, ORGANIZE, EXECUTE, PREPARE_PROVIDER, UNKNOWN }
public record AgentToolResult(string Tool, string Outcome, double DurationMs);
public record AgentCheckpoint(Guid RunId, Guid SessionId, string Fingerprint, AgentRunState State, AgentIntent[] Plan,
    Guid[] RetrievedPromptIds, Dictionary<Guid, int> SourceRevisions, AgentToolResult[] Tools, int StepBudget,
    string Approval, string? ErrorCode, Guid? PreparedId, string Model, string ContractVersion, DateTime UpdatedAt,
    AgentIntent UserIntent = AgentIntent.UNKNOWN, bool EffectStarted = false, Dictionary<string, double>? StageDurations = null);
public interface IAgentRunStore
{
    AgentCheckpoint? Get(Guid id);
    void Put(AgentCheckpoint checkpoint);
    void Forget(Guid id);
    bool TryBegin(AgentCheckpoint checkpoint);
}
public interface ILocalPreparedExecutor
{
    Task<string> Execute(PreparedDraft draft, string model, Func<string, Task>? progress, CancellationToken ct);
}
public interface IPreparationReadTools { void Register(PreparationTools tools); }
public record DraftDiagnostics(PreparedDraft Initial, PreparedDraft? Revised, DateTime UpdatedAt);
public sealed class PreparationDiagnostics
{
    private readonly ConcurrentDictionary<Guid, DraftDiagnostics> items = new();
    public void Put(Guid id, PreparedDraft? initial, PreparedDraft? revised)
    {
        foreach (var entry in items.Where(e => e.Value.UpdatedAt < DateTime.UtcNow.AddHours(-2)).Concat(items.OrderBy(e => e.Value.UpdatedAt).Take(Math.Max(0, items.Count - 100))).ToArray()) items.TryRemove(entry.Key, out _);
        if (initial is not null) items[id] = new(initial, revised, DateTime.UtcNow);
        else if (items.TryGetValue(id, out var existing)) items[id] = existing with { Revised = revised, UpdatedAt = DateTime.UtcNow };
    }
    public DraftDiagnostics? Get(Guid id) => items.TryGetValue(id, out var item) && item.UpdatedAt > DateTime.UtcNow.AddHours(-2) ? item : null;
    public void Forget(Guid id) => items.TryRemove(id, out _);
}
public record DraftRef(Guid DraftId, Guid SessionId, int DraftRevision, GoalTarget? SourceRef, string CurrentContent);
public enum DraftObservationKind { Snapshot, Authored, NewArtifact, Restored }
public record AgentSession(Guid Id, PromptDocument? Source, PreparedDraft? Draft, Dictionary<string, string> Values, DateTime UpdatedAt,
    PromptDocument[]? Candidates = null, SemanticInterpretation? LastProposal = null, SemanticOperation? PendingAction = null, string Approval = "NONE", bool SourceStale = false, SemanticInterpretation? PendingPlan = null, string? PendingRequest = null)
{
    public int DraftRevision { get; init; } = 1;
    // Store concurrency is distinct from reusable-instruction/body revision. Not part of local HTTP JSON.
    [JsonIgnore] public int StorageRevision { get; init; }
    public LiveCommand? PendingCommand { get; init; }
    public DraftRef? WorkingDraftReference => Draft is null ? null : new(Draft.Id, Id, DraftRevision,
        Draft.SourceId is { } source && Draft.SourceRevision is { } revision ? new(source, revision) : null, Draft.TemplateText);
    public PreparedDraft? UnsavedDraft => Draft is { SourceId: null } ? Draft : null;
    public int? SourceRevision => Draft?.SourceRevision ?? Source?.Revision;
}
/// <summary>Atomic storage boundary for task context. Callers bind identity outside model arguments.</summary>
public interface IAgentSessionStore
{
    AgentSession? Get(Guid id);
    void Put(AgentSession session);
    AgentSession Update(Guid id, Func<AgentSession?, AgentSession> transition);
    void Forget(Guid id);
}

// Existing local behavior remains transient, bounded and never logged or written to SQLite.
public sealed partial class AgentSessions(IAgentSessionStore? store = null)
{
    private readonly IAgentSessionStore storage = store ?? new TransientAgentSessionStore();
    public AgentSession? Get(Guid id) => storage.Get(id);
    public void Put(AgentSession session) => storage.Put(session);
    public DraftRef RequireWorkingDraft(DraftRef expected)
    {
        var current = Get(expected.SessionId)?.WorkingDraftReference;
        if (expected.DraftId == Guid.Empty || expected.SessionId == Guid.Empty || expected.DraftRevision < 1
            || current is null || current != expected)
            throw new ConflictException("El borrador de la sesión cambió o caducó. Revisa su versión actual antes de continuar.");
        return current;
    }
    // Conversation identity is independent from immutable preparation operation receipts.
    // Only a verified authoring action may replace the artifact's semantic provenance.
    public AgentSession ObserveDraft(AgentSession observation, DraftObservationKind kind = DraftObservationKind.Snapshot,
        int? expectedDraftRevision = null, Guid? expectedDraftId = null)
    {
        var draft = observation.Draft ?? throw new ValidationException("Falta el borrador observado.");
        if (observation.Id == Guid.Empty || draft.SessionId is { } sessionId && sessionId != observation.Id
            || (draft.SourceId is null) != (draft.SourceRevision is null)
            || draft.SourceId is { } sourceId
                && !(observation.Source?.Id == sourceId && observation.Source.Revision == draft.SourceRevision)
                && !draft.Candidates.Any(candidate => candidate.Prompt.Id == sourceId && candidate.Prompt.Revision == draft.SourceRevision)
            || !Enum.IsDefined(kind)) throw new ValidationException("Identidad del borrador observado inválida.");
        return storage.Update(observation.Id, Transition);

        AgentSession Transition(AgentSession? previous)
        {
            if (expectedDraftRevision is { } expected && (expected != (previous?.DraftRevision ?? 0)
                || expected > 0 && previous?.Draft?.Id != expectedDraftId))
                throw new ConflictException("El contexto conservado cambió. Recupera su revisión actual.");
            var prior = previous?.Draft;
            var sameIdentity = kind != DraftObservationKind.NewArtifact && prior is not null
                && prior.SourceId == draft.SourceId && prior.SourceRevision == draft.SourceRevision;
            var sameContent = sameIdentity && prior!.Title == draft.Title && prior.TemplateText == draft.TemplateText;
            var provenance = kind != DraftObservationKind.Snapshot ? draft.Semantic ?? observation.LastProposal
                : sameContent ? prior!.Semantic ?? previous!.LastProposal : null;
            return observation with {
                Draft = draft with { Id = sameIdentity ? prior!.Id : Guid.NewGuid(), SessionId = observation.Id, Semantic = provenance },
                DraftRevision = sameIdentity ? previous!.DraftRevision + (sameContent ? 0 : 1) : 1,
                LastProposal = provenance, Approval = "NOT_GRANTED",
                Values = new(observation.Values), UpdatedAt = DateTime.UtcNow
            };
        }
    }
    public void Forget(Guid id) => storage.Forget(id);
}

internal sealed class TransientAgentSessionStore : IAgentSessionStore
{
    private readonly ConcurrentDictionary<Guid, AgentSession> sessions = new();
    public AgentSession? Get(Guid id)
    {
        foreach (var entry in sessions.Where(e => e.Value.UpdatedAt < DateTime.UtcNow.AddHours(-2))) sessions.TryRemove(entry.Key, out _);
        foreach (var entry in sessions.OrderBy(e => e.Value.UpdatedAt).Take(Math.Max(0, sessions.Count - 100))) sessions.TryRemove(entry.Key, out _);
        return sessions.GetValueOrDefault(id);
    }
    public void Put(AgentSession session) { Get(session.Id); sessions[session.Id] = session; }
    public AgentSession Update(Guid id, Func<AgentSession?, AgentSession> transition)
    {
        Get(id);
        return sessions.AddOrUpdate(id, _ => transition(null), (_, prior) => transition(prior));
    }
    public void Forget(Guid id) => sessions.TryRemove(id, out _);
}

public static class AgentRouting
{
    public static AgentIntent Intent(PreparationIntent intent) => intent switch {
        PreparationIntent.FIND_PROMPT => AgentIntent.FIND, PreparationIntent.CREATE_PROMPT => AgentIntent.CREATE,
        PreparationIntent.ADAPT_PROMPT => AgentIntent.ADAPT, PreparationIntent.IMPROVE_PROMPT => AgentIntent.IMPROVE,
        PreparationIntent.COMPLETE_PROMPT => AgentIntent.FILL, PreparationIntent.RUN_LOCAL => AgentIntent.EXECUTE,
        PreparationIntent.USE_PROMPT => AgentIntent.PREPARE_PROVIDER, PreparationIntent.ORGANIZE_PROMPT => AgentIntent.ORGANIZE, _ => AgentIntent.UNKNOWN };
}
