using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class AgentRunStore(LocalStore store) : IAgentRunStore
{
    private readonly int recovered = store.BlockInterruptedAgentRuns();
    public AgentCheckpoint? Get(Guid id) => store.ReadAgentRun(id);
    public void Put(AgentCheckpoint checkpoint) => store.WriteAgentRun(checkpoint);
    public void Forget(Guid id) => store.ForgetAgentRun(id);
    public bool TryBegin(AgentCheckpoint checkpoint) => store.TryBeginAgentRun(checkpoint);
}

public sealed class LocalPreparedExecutor(ILocalLlmClient client) : ILocalPreparedExecutor
{
    public async Task<string> Execute(PreparedDraft draft, string model, Func<string, Task>? progress, CancellationToken ct)
    {
        if (draft.State != PreparationState.READY || string.IsNullOrWhiteSpace(draft.CompiledText)) throw new ValidationException("Faltan datos antes de ejecutar.");
        var compiled = new CompiledPrompt(Guid.NewGuid(), draft.SourceId ?? draft.Id, draft.SourceRevision ?? 1,
            draft.Title, draft.CompiledText, new Dictionary<string, string>(), DateTime.UtcNow);
        return (await client.Execute(compiled, model, true, progress, ct)).Text;
    }
}

public sealed partial class LocalStore
{
    public int BlockInterruptedAgentRuns() => Write((db, tx) =>
    {
        var interrupted = Get<AgentCheckpoint>(db, "nova-run", tx).Where(r => r.State is AgentRunState.RECEIVE or AgentRunState.INTERPRET or AgentRunState.RETRIEVE
            or AgentRunState.PLAN or AgentRunState.EXECUTE_TOOLS or AgentRunState.COMPOSE or AgentRunState.VALIDATE).ToArray();
        foreach (var run in interrupted) Put(db, tx, "nova-run", run.RunId.ToString(), run with { State = AgentRunState.BLOCKED, ErrorCode = "INTERRUPTED_NO_REPLAY", UpdatedAt = DateTime.UtcNow });
        return interrupted.Length;
    });
    public bool TryBeginAgentRun(AgentCheckpoint checkpoint) => Write((db, tx) =>
    {
        if (GetOne<AgentCheckpoint>(db, "nova-run", checkpoint.RunId, tx) is not null || GetOne<AgentRunReceipt>(db, "nova-run-receipt", checkpoint.RunId, tx) is not null) return false;
        Put(db, tx, "nova-run-receipt", checkpoint.RunId.ToString(), new AgentRunReceipt(checkpoint.Fingerprint));
        Put(db, tx, "nova-run", checkpoint.RunId.ToString(), checkpoint); return true;
    });
    public AgentCheckpoint? ReadAgentRun(Guid id) => Read(db => GetOne<AgentCheckpoint>(db, "nova-run", id));
    public void WriteAgentRun(AgentCheckpoint checkpoint) => Write((db, tx) =>
    {
        var prior = GetOne<AgentCheckpoint>(db, "nova-run", checkpoint.RunId, tx);
        if (prior is not null && prior.Fingerprint != checkpoint.Fingerprint) throw new ConflictException("RunId ya utilizado para otra solicitud.");
        Put(db, tx, "nova-run", checkpoint.RunId.ToString(), checkpoint);
        // Bounded diagnostic retention. Contains identifiers/timing only; never task or generated content.
        using var trim = db.CreateCommand(); trim.Transaction = tx;
        trim.CommandText = "DELETE FROM Documents WHERE Kind='nova-run' AND Id NOT IN (SELECT Id FROM Documents WHERE Kind='nova-run' ORDER BY json_extract(Payload,'$.updatedAt') DESC LIMIT 500)";
        trim.ExecuteNonQuery(); return true;
    });
    public void ForgetAgentRun(Guid id) => Write((db, tx) => { using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM Documents WHERE Kind='nova-run' AND Id=$id"; cmd.Parameters.AddWithValue("$id", id.ToString()); return cmd.ExecuteNonQuery(); });
    private sealed record AgentRunReceipt(string Fingerprint);
}
