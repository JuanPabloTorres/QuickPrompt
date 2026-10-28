using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore : IResultHistory
{
    private const string HistoryKinds = "'compiled','execution','web-transfer'";
    private static string HistoryFingerprint(SqliteConnection db, SqliteTransaction tx)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = $"SELECT Kind,Id,Payload FROM Documents WHERE Kind IN ({HistoryKinds}) ORDER BY Kind,Id";
        using var rows = command.ExecuteReader();
        while (rows.Read()) for (var column = 0; column < 3; column++) hash.AppendData(Encoding.UTF8.GetBytes(rows.GetString(column) + '\0'));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public ResultHistoryPage ResultHistory(int page)
    {
        if (page < 0 || page > 100000) throw new ValidationException("Página de historial inválida.");
        return Read(db => {
            using var tx = db.BeginTransaction(deferred: true);
            const int size = 10;
            var compiled = Get<CompiledPrompt>(db, "compiled", tx).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArray();
            var responses = Get<LlmExecution>(db, "execution", tx);
            var executions = responses.OrderBy(item => item.CreatedAt).ToLookup(item => item.CompiledId);
            using var transfers = db.CreateCommand(); transfers.Transaction = tx;
            transfers.CommandText = "SELECT COUNT(*) FROM Documents WHERE Kind='web-transfer'";
            var transferCount = Convert.ToInt32(transfers.ExecuteScalar());
            return new ResultHistoryPage(page, size, compiled.Length, responses.Count, transferCount, HistoryFingerprint(db, tx),
                compiled.Skip(page * size).Take(size).Select(item => new SavedResult(item.Id, item.TemplateId, item.Revision, item.Title, item.Text, item.CreatedAt, executions[item.Id].ToArray())).ToArray());
        });
    }
    public void ClearResultHistory(HistoryClearInput input)
    {
        if (!input.Confirmed) throw new ValidationException("Confirma el borrado del historial revisado.");
        Write((db, tx) => {
            if (Get<LiveRecord>(db, "nova-live", tx).Any(run => !LiveRuns.Terminal(run.Status)))
                throw new ConflictException("Termina o detén NOVA antes de borrar el historial.");
            if (input.ExpectedSnapshot != HistoryFingerprint(db, tx))
                throw new ConflictException("El historial cambió. Revísalo de nuevo antes de borrarlo.");
            return ClearHistoryInside(db, tx);
        });
    }
    private static int ClearHistoryInside(SqliteConnection db, SqliteTransaction tx)
    {
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = $"DELETE FROM Documents WHERE Kind IN ({HistoryKinds})";
        return command.ExecuteNonQuery();
    }
}
