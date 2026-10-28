using Microsoft.Data.Sqlite;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

// A small exact vector database for system metadata, separate from the domain DB.
// Scores are computed over the current reflection snapshot, never stale stored text.
public sealed class CapabilityVectorStore
{
    private const int ApplicationId = 0x51504E56;
    private readonly string connectionString;
    public CapabilityVectorStore(string path)
    {
        var absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!); SQLitePCL.Batteries_V2.Init();
        connectionString = new SqliteConnectionStringBuilder { DataSource = absolute, Pooling = false }.ToString();
        using var db = Open(); using var check = db.CreateCommand();
        check.CommandText = "PRAGMA application_id"; var identity = Convert.ToInt32(check.ExecuteScalar());
        check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        if (identity != ApplicationId && (identity != 0 || Convert.ToInt32(check.ExecuteScalar()) != 0))
            throw new ValidationException("El índice vectorial requiere su propia base; no se modificará otra SQLite.");
        check.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(check.ExecuteScalar()) is not (0 or 1)) throw new ValidationException("Versión del índice vectorial no compatible.");
        using var create = db.CreateCommand(); create.CommandText = $"""
            PRAGMA application_id={ApplicationId};
            CREATE TABLE IF NOT EXISTS Vectors (NodeId TEXT PRIMARY KEY, Profile TEXT NOT NULL, ContentHash TEXT NOT NULL, Dimension INTEGER NOT NULL, Vector BLOB NOT NULL);
            PRAGMA user_version=1;
            """; create.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public Dictionary<string, float[]> Read(string profile, IReadOnlyDictionary<string, string> expected)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT NodeId, ContentHash, Dimension, Vector FROM Vectors WHERE Profile=$profile";
        command.Parameters.AddWithValue("$profile", profile); using var reader = command.ExecuteReader(); var result = new Dictionary<string, float[]>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var id = reader.GetString(0); var dimension = reader.GetInt32(2); var bytes = (byte[])reader[3];
            if (!expected.TryGetValue(id, out var hash) || hash != reader.GetString(1)) continue;
            if (dimension is < 1 or > 4096 || bytes.Length != dimension * sizeof(float)) throw new ValidationException("Índice vectorial corrupto.");
            var vector = new float[dimension]; Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length); result.Add(id, VectorMath.Unit(vector));
        }
        return result;
    }
    public void Replace(string profile, IReadOnlyDictionary<string, string> hashes, IReadOnlyDictionary<string, float[]> vectors)
    {
        if (hashes.Count != vectors.Count || hashes.Keys.Any(id => !vectors.ContainsKey(id)) || vectors.Values.Select(v => v.Length).Distinct().Count() > 1)
            throw new ValidationException("Snapshot vectorial incompleto o incompatible.");
        var normalized = vectors.ToDictionary(pair => pair.Key, pair => VectorMath.Unit(pair.Value), StringComparer.Ordinal);
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var clear = db.CreateCommand(); clear.Transaction = transaction; clear.CommandText = "DELETE FROM Vectors"; clear.ExecuteNonQuery();
        foreach (var pair in normalized)
        {
            var bytes = new byte[pair.Value.Length * sizeof(float)]; Buffer.BlockCopy(pair.Value, 0, bytes, 0, bytes.Length);
            using var command = db.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO Vectors (NodeId,Profile,ContentHash,Dimension,Vector) VALUES ($id,$profile,$hash,$dimension,$vector)";
            command.Parameters.AddWithValue("$id", pair.Key); command.Parameters.AddWithValue("$profile", profile); command.Parameters.AddWithValue("$hash", hashes[pair.Key]);
            command.Parameters.AddWithValue("$dimension", pair.Value.Length); command.Parameters.AddWithValue("$vector", bytes); command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
