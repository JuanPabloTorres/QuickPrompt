using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public class DatabaseConnectionProvider
    {
        private SQLiteAsyncConnection _connection;

        private const string DatabaseName = "QuickPrompt.db3";

        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, DatabaseName);

        public SQLiteAsyncConnection GetConnection()
        {
            return _connection ??= new SQLiteAsyncConnection(DbPath);
        }

        public async Task RestoreDatabaseAsync(Func<SQLiteAsyncConnection, Task> initializeCallback)
        {
            // 🛑 Cierra la conexión anterior (si existe)
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection = null;
            }

            // 🗑️ Elimina el archivo físico
            if (File.Exists(DbPath))
                File.Delete(DbPath);

            // 🔁 Crea una nueva conexión
            _connection = new SQLiteAsyncConnection(DbPath);

            // ✅ Ejecuta la inicialización con la nueva conexión
            if (initializeCallback != null)
                await initializeCallback(_connection);
        }

        public static bool DatabaseExists() => File.Exists(DbPath);
    }
}