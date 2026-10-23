using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record CompleteDraftRequest(Guid OperationId, Dictionary<string, string> Values);
public interface IPreparedDraftCompletion
{
    PreparedDraft Complete(Guid id, CompleteDraftRequest input);
}

public sealed class PreparedDraftCompletion(IPreparedDraftStore drafts, IAgentRunStore runs, AgentSessions sessions, LiveRuns live) : IPreparedDraftCompletion
{
    public PreparedDraft Complete(Guid id, CompleteDraftRequest input)
    {
        var source = drafts.Get(id) ?? throw new MissingPromptException();
        if (input.OperationId == Guid.Empty || input.OperationId == id || input.Values is null || input.Values.Count > 60
            || input.Values.Any(v => !source.Variables.Any(d => d.Name == v.Key) || string.IsNullOrWhiteSpace(v.Value) || v.Value.Length > 8000)) throw new ValidationException("Revisa los valores del borrador.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, values = input.Values.OrderBy(v => v.Key, StringComparer.Ordinal) }))));
        if (drafts.Fingerprint(input.OperationId) is { } previous)
        { if (previous != fingerprint) throw new ConflictException("Identificador ya utilizado."); return drafts.Get(input.OperationId) ?? throw new ConflictException("El resultado del borrador ya no está disponible. Vuelve a prepararlo antes de continuar."); }
        var values = source.Values.Where(v => !input.Values.ContainsKey(v.Name)).Concat(input.Values.Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED"))).ToArray();
        var missing = source.Variables.Where(v => !values.Any(value => value.Name == v.Name)).Select(v => v.Name).ToArray();
        var result = source with { Id = input.OperationId, Values = values, MissingVariables = missing,
            CompiledText = missing.Length == 0 ? TemplateCompiler.Render(source.TemplateText, values.ToDictionary(v => v.Name, v => v.Value)) : null,
            State = missing.Length == 0 ? PreparationState.READY : PreparationState.NEEDS_INFORMATION, Question = missing.Length == 0 ? "" : $"¿Puedes aportar {string.Join(", ", missing)}?", CreatedAt = DateTime.UtcNow };
        var run = new AgentCheckpoint(result.Id, source.SessionId ?? source.Id, fingerprint,
            result.State == PreparationState.READY ? AgentRunState.READY : AgentRunState.NEEDS_CLARIFICATION, [AgentIntent.FILL, AgentIntent.COMPILE],
            result.Candidates.Select(c => c.Prompt.Id).ToArray(), result.Candidates.ToDictionary(c => c.Prompt.Id, c => c.Prompt.Revision),
            [new("compile_prompt", "OK", 0), new("validate_prompt", "OK", 0)], 5, "SAVE_REQUIRES_REVIEW", null, result.Id,
            "deterministic", AgentOrchestrator.ContractVersion, DateTime.UtcNow, AgentIntent.FILL);
        if (!runs.TryBegin(run)) throw new ConflictException("Esta operación ya se inició. No se repetirá.");
        drafts.Put(result, fingerprint);
        var sessionId = result.SessionId ?? source.Id;
        var session = sessions.Get(sessionId);
        sessions.ObserveDraft(new(sessionId, result.Candidates.SingleOrDefault(c => c.Prompt.Id == result.SourceId)?.Prompt ?? session?.Source,
            result, result.SourceId is null ? session?.Values ?? [] : result.Values.ToDictionary(v => v.Name, v => v.Value), DateTime.UtcNow,
            result.Candidates.Select(c => c.Prompt).Take(6).ToArray(), result.Semantic ?? session?.LastProposal, null, "NOT_GRANTED", session?.SourceStale ?? false,
            result.State == PreparationState.NEEDS_INFORMATION ? result.Semantic : null, result.State == PreparationState.NEEDS_INFORMATION ? session?.PendingRequest : null), DraftObservationKind.Restored);
        live.Create(new(result.Id, result.SessionId ?? source.Id, "Completar el borrador con los valores aportados.", "LIVE", result.SourceId, result.SourceRevision));
        live.Emit(result.Id, LiveStatus.VERIFYING, LiveActor.RULE_SELECTED, "Compilación determinista completada con los valores aportados; no se consultó al modelo.", "Compilation", "compile_prompt", result.SourceId);
        live.Emit(result.Id, result.State == PreparationState.READY ? LiveStatus.COMPLETED : LiveStatus.BLOCKED, LiveActor.SYSTEM_VALIDATION, result.State == PreparationState.READY ? "Resultado compilado y listo para revisar." : "Todavía faltan datos.", "Completion", target: result.SourceId);
        return result;
    }
}
