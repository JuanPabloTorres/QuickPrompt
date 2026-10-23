using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    /// <summary>
    /// Provides thread-safe access to SQLite database connection.
    /// ✅ PHASE 2: Thread-safety implemented using double-check locking pattern.
    /// </summary>
    public class DatabaseConnectionProvider
    {
        private SQLiteAsyncConnection? _connection;
        
        // ✅ PHASE 2: Lock object for thread-safe singleton initialization
        private readonly object _lock = new object();

        private const string DatabaseName = "QuickPrompt.db3";

        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, DatabaseName);

        /// <summary>
        /// Gets the database connection using thread-safe double-check locking pattern.
        /// </summary>
        /// <returns>Thread-safe SQLiteAsyncConnection instance</returns>
        public SQLiteAsyncConnection GetConnection()
        {
            // ✅ PHASE 2: First check (no lock) - fast path for already initialized connection
            if (_connection == null)
            {
                lock (_lock)
                {
                    // ✅ PHASE 2: Second check (with lock) - prevents multiple threads from creating connection
                    if (_connection == null)
                    {
                        _connection = new SQLiteAsyncConnection(DbPath);
                        System.Diagnostics.Debug.WriteLine("[DatabaseConnectionProvider] New SQLite connection created");
                    }
                }
            }
            
            return _connection;
        }

        /// <summary>
        /// Restores the database by closing current connection, deleting file, and reinitializing.
        /// ✅ PHASE 2: Thread-safe database restoration.
        /// </summary>
        public async Task RestoreDatabaseAsync(Func<SQLiteAsyncConnection, Task> initializeCallback)
        {
            lock (_lock)
            {
                // 🛑 Cierra la conexión anterior (si existe)
                if (_connection != null)
                {
                    try
                    {
                        // Close connection synchronously (SQLiteAsyncConnection.CloseAsync doesn't exist)
                        _connection = null;
                        System.Diagnostics.Debug.WriteLine("[DatabaseConnectionProvider] Connection closed for restoration");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseConnectionProvider] Error closing connection: {ex.Message}");
                    }
                }

                // 🗑️ Elimina el archivo físico
                if (File.Exists(DbPath))
                {
                    try
                    {
                        File.Delete(DbPath);
                        System.Diagnostics.Debug.WriteLine("[DatabaseConnectionProvider] Database file deleted");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseConnectionProvider] Error deleting database file: {ex.Message}");
                        throw new InvalidOperationException("Failed to delete database file during restoration", ex);
                    }
                }

                // 🔁 Crea una nueva conexión
                _connection = new SQLiteAsyncConnection(DbPath);
                System.Diagnostics.Debug.WriteLine("[DatabaseConnectionProvider] New connection created after restoration");
            }

            // ✅ Ejecuta la inicialización con la nueva conexión (fuera del lock para evitar deadlock)
            if (initializeCallback != null)
            {
                try
                {
                    await initializeCallback(_connection);
                    System.Diagnostics.Debug.WriteLine("[DatabaseConnectionProvider] Database initialization callback completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseConnectionProvider] Error in initialization callback: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Checks if the database file exists on disk.
        /// </summary>
        /// <returns>True if database file exists, false otherwise</returns>
        public static bool DatabaseExists() => File.Exists(DbPath);
    }
}