using Microsoft.Data.Sqlite;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

internal static class SchemaMigrations
{
    public static int ReadVersion(SqliteConnection db)
    {
        using var c = db.CreateCommand();
        c.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
        if (Convert.ToInt32(c.ExecuteScalar()) == 0) return 0;
        c.CommandText = "SELECT COALESCE(MAX(Version),0) FROM SchemaVersion";
        var version = Convert.ToInt32(c.ExecuteScalar());
        if (version > LocalStore.CurrentSchemaVersion) throw new ValidationException("Versión de DB no soportada / Unsupported DB version.");
        return version;
    }
    public static void BackupBeforeMigration(SqliteConnection db, string path)
    {
        // Owned migration backup, never overwrite an earlier recovery point.
        var target = Path.GetFullPath(path) + ".before-goal04-" + Guid.NewGuid().ToString("N") + ".sqlite";
        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString());
        backup.Open(); db.BackupDatabase(backup);
    }
    public static void Apply(SqliteConnection db, SqliteTransaction tx)
    {
        using var c = db.CreateCommand(); c.Transaction = tx;
        c.CommandText = """
            CREATE TABLE IF NOT EXISTS PromptGroups(Id TEXT PRIMARY KEY NOT NULL, NameKey TEXT NOT NULL UNIQUE, Payload TEXT NOT NULL CHECK(json_valid(Payload)));
            CREATE TABLE IF NOT EXISTS GroupMemberships(
                GroupId TEXT NOT NULL REFERENCES PromptGroups(Id) ON DELETE CASCADE,
                Kind TEXT NOT NULL CHECK(Kind IN ('template','instruction')), EntityId TEXT NOT NULL,
                PRIMARY KEY(GroupId,Kind,EntityId),
                FOREIGN KEY(Kind,EntityId) REFERENCES Documents(Kind,Id) ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS IX_GroupMemberships_Entity ON GroupMemberships(Kind,EntityId,GroupId);
            CREATE INDEX IF NOT EXISTS IX_Documents_Active ON Documents(Kind,json_extract(Payload,'$.isActive'),json_extract(Payload,'$.isFavorite'),json_extract(Payload,'$.updatedAt'));
            CREATE INDEX IF NOT EXISTS IX_Documents_Versions ON Documents(Kind,json_extract(Payload,'$.templateId'),json_extract(Payload,'$.instructionId'));
            INSERT OR IGNORE INTO SchemaVersion VALUES(2);
            """;
        c.ExecuteNonQuery();
    }
}
