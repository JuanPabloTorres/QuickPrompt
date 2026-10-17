using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class LiveRunStore : ILiveRunStore
{
    private readonly LocalStore store;
    public LiveRunStore(LocalStore store) { this.store = store; store.BlockInterruptedLiveRuns(); }
    public bool TryCreate(LiveRecord record) => store.TryCreateLiveRun(record);
    public LiveRecord? Get(Guid id) => store.ReadLiveRun(id);
    public LiveEvent[] Events(Guid id, long after) => store.ReadLiveEvents(id, after);
    public LiveEvent Append(LiveRecord record, LiveEvent item) => store.AppendLiveEvent(record, item);
}
public sealed partial class LocalStore
{
    public void BlockInterruptedLiveRuns() => Write((db, tx) =>
    {
        foreach (var run in Get<LiveRecord>(db, "nova-live", tx).Where(r => !LiveRuns.Terminal(r.Status)))
        {
            var item = new LiveEvent(run.RunId, Guid.NewGuid(), run.Sequence + 1, DateTime.UtcNow, "Recovery", LiveActor.SYSTEM_VALIDATION,
                null, run.TargetId, LiveStatus.BLOCKED, null, "INTERRUPTED_NO_REPLAY", "La ejecución se interrumpió. Se conservan los eventos; las acciones no se repetirán.");
            Put(db, tx, "nova-live-event", $"{run.RunId}:{item.Sequence:D6}", item);
            Put(db, tx, "nova-live", run.RunId.ToString(), run with { Status = LiveStatus.BLOCKED, Sequence = item.Sequence, BrowserOpen = false, ErrorCode = item.ErrorCode, UpdatedAt = DateTime.UtcNow });
        }
        return true;
    });
    public bool TryCreateLiveRun(LiveRecord record) => Write((db, tx) =>
    {
        if (GetOne<LiveRecord>(db, "nova-live", record.RunId, tx) is not null || GetOne<LiveReceipt>(db, "nova-live-receipt", record.RunId, tx) is not null) return false;
        Put(db, tx, "nova-live-receipt", record.RunId.ToString(), new LiveReceipt(record.Fingerprint));
        Put(db, tx, "nova-live", record.RunId.ToString(), record); return true;
    });
    public LiveRecord? ReadLiveRun(Guid id) => Read(db => GetOne<LiveRecord>(db, "nova-live", id));
    public LiveEvent[] ReadLiveEvents(Guid id, long after) => Read(db =>
    {
        using var command = db.CreateCommand(); command.CommandText = "SELECT Payload FROM Documents WHERE Kind='nova-live-event' AND Id LIKE $prefix AND CAST(json_extract(Payload,'$.sequence') AS INTEGER)>$after ORDER BY CAST(json_extract(Payload,'$.sequence') AS INTEGER)";
        command.Parameters.AddWithValue("$prefix", id.ToString() + ":%"); command.Parameters.AddWithValue("$after", after);
        using var rows = command.ExecuteReader(); var events = new List<LiveEvent>();
        while (rows.Read()) events.Add(System.Text.Json.JsonSerializer.Deserialize<LiveEvent>(rows.GetString(0), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!);
        return events.ToArray();
    });
    public LiveEvent AppendLiveEvent(LiveRecord record, LiveEvent item) => Write((db, tx) =>
    {
        var prior = GetOne<LiveRecord>(db, "nova-live", record.RunId, tx) ?? throw new MissingPromptException();
        if (prior.Fingerprint != record.Fingerprint || prior.Sequence >= 1024 || prior.Sequence >= 1000 && !LiveRuns.Terminal(item.Status)) throw new ConflictException("Límite o identidad de eventos inválida.");
        var next = item with { Sequence = prior.Sequence + 1 };
        Put(db, tx, "nova-live-event", $"{record.RunId}:{next.Sequence:D6}", next);
        Put(db, tx, "nova-live", record.RunId.ToString(), record with { Sequence = next.Sequence });
        // Keep diagnostic payloads bounded; replay-prevention receipts are retained independently.
        using var trim = db.CreateCommand(); trim.Transaction = tx;
        trim.CommandText = "DELETE FROM Documents WHERE Kind='nova-live-event' AND json_extract(Payload,'$.runId') IN (SELECT Id FROM Documents WHERE Kind='nova-live' ORDER BY json_extract(Payload,'$.updatedAt') DESC LIMIT -1 OFFSET 200); DELETE FROM Documents WHERE Kind='nova-live' AND Id IN (SELECT Id FROM Documents WHERE Kind='nova-live' ORDER BY json_extract(Payload,'$.updatedAt') DESC LIMIT -1 OFFSET 200)";
        trim.ExecuteNonQuery(); return next;
    });
    private sealed record LiveReceipt(string Fingerprint);
}
