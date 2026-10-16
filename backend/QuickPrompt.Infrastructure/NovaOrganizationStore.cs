using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore
{
    public PromptGroup NovaRenameGroup(Guid id, GroupInput input) => Write((db, tx) =>
    {
        var group = GetGroup(db, tx, id) ?? throw new ValidationException("Colección no disponible.");
        if (group.Revision != input.Revision) throw new ConflictException("La colección cambió. Recarga para renombrarla.");
        group.Name = input.Name; return SaveGroupInside(db, tx, group);
    });
    public void NovaDeleteGroup(Guid id, ConfirmInput input) => Write((db, tx) =>
    {
        if (!input.Confirmed) throw new ValidationException("Confirma la eliminación de la colección.");
        var group = GetGroup(db, tx, id) ?? throw new ValidationException("Colección no disponible.");
        if (group.Revision != input.Revision) throw new ConflictException("La colección cambió. Recarga para eliminarla.");
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = "DELETE FROM PromptGroups WHERE Id=$id"; command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery();
    });
    public void NovaAssign(Guid id, MembershipInput input) => Write((db, tx) =>
    {
        var prompt = GetOne<Template>(db, "template", id, tx) ?? throw new MissingPromptException();
        if (prompt.Revision != input.Revision) throw new ConflictException("El prompt cambió. Recarga sus colecciones.");
        return AssignGroupInside(db, tx, input.GroupId, "template", [id], input.Member);
    });
    public string NovaSnapshot()
    {
        lock (gate)
        {
            var operational = Read(db => { using var command = db.CreateCommand();
                command.CommandText = "SELECT Kind,Id,Payload FROM Documents WHERE Kind IN ('nova-draft','nova-preparation','nova-prepared-save','nova-use') ORDER BY Kind,Id";
                using var rows = command.ExecuteReader(); var text = new StringBuilder();
                while (rows.Read()) text.Append(rows.GetString(0)).Append('\0').Append(rows.GetString(1)).Append('\0').Append(rows.GetString(2)).Append('\0');
                return text.ToString(); });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Export() + "\0" + operational)));
        }
    }
    public string NovaBackup() => Read(db =>
    {
        var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "backups");
        Directory.CreateDirectory(directory);
        var name = $"nova-{DateTime.UtcNow:yyyyMMddTHHmmss}-{Guid.NewGuid():N}.sqlite";
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, name), Pooling = false }.ToString());
        target.Open(); db.BackupDatabase(target);
        using var check = target.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
        if ((string?)check.ExecuteScalar() != "ok") throw new ValidationException("El respaldo no superó la verificación.");
        return name;
    });
    public int NovaRestore(LibraryImportInput input)
    {
        if (!input.Confirmed) throw new ValidationException("Confirma el reemplazo de la biblioteca.");
        lock (gate)
        {
            var json = input.Data.GetRawText(); PreviewImport(json, true);
            if (input.ExpectedSnapshot != NovaSnapshot()) throw new ConflictException("La biblioteca cambió desde la revisión. Revisa el respaldo de nuevo.");
            NovaBackup(); // Verified SQLite backup must succeed before any replacement.
            return Import(json, true);
        }
    }
}
