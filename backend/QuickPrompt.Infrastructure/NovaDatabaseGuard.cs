using Microsoft.Data.Sqlite;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public static class NovaDatabaseGuard
{
    // Configuration may select an existing web database, but a legacy MAUI file
    // must go through explicit import, never through schema initialization.
    public static void Validate(string path)
    {
        if (!File.Exists(path)) return;
        SQLitePCL.Batteries_V2.Init();
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('SchemaVersion','Documents')";
        if (Convert.ToInt32(command.ExecuteScalar()) != 2)
            throw new ValidationException("La ruta apunta a una base SQLite no compatible. Utiliza la importación de un archivo elegido; no se modificó el esquema original.");
        SchemaMigrations.ReadVersion(db);
    }
}
