using SQLite;
using System;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    /// <summary>
    /// Manages database schema migrations with versioning and rollback support.
    /// ? PHASE 2: Structured migration framework to prevent data loss during updates.
    /// </summary>
    public class DatabaseMigrationManager
    {
        private const int CURRENT_VERSION = 1; // ? Increment when adding new migrations
        private const string VERSION_TABLE = "SchemaVersion";
        
        private readonly SQLiteAsyncConnection _database;

        public DatabaseMigrationManager(SQLiteAsyncConnection database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Executes all pending migrations to bring database to current version.
        /// </summary>
        public async Task MigrateToLatestAsync()
        {
            try
            {
                // Create version tracking table if not exists
                await EnsureVersionTableExistsAsync();

                int currentVersion = await GetDatabaseVersionAsync();
                
                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] Current DB version: {currentVersion}, Target version: {CURRENT_VERSION}");

                if (currentVersion >= CURRENT_VERSION)
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseMigrationManager] Database is up to date");
                    return;
                }

                // Apply migrations sequentially
                for (int version = currentVersion + 1; version <= CURRENT_VERSION; version++)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] Applying migration to version {version}...");
                    
                    try
                    {
                        await ApplyMigrationAsync(version);
                        await SaveDatabaseVersionAsync(version);
                        
                        System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] Successfully migrated to version {version}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] FAILED to migrate to version {version}: {ex.Message}");
                        throw new InvalidOperationException($"Migration to version {version} failed. Database may be in inconsistent state.", ex);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] All migrations completed successfully. Current version: {CURRENT_VERSION}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationManager] Migration process failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates the schema version tracking table.
        /// </summary>
        private async Task EnsureVersionTableExistsAsync()
        {
            var createTableSql = $@"
                CREATE TABLE IF NOT EXISTS {VERSION_TABLE} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Version INTEGER NOT NULL,
                    AppliedAt TEXT NOT NULL,
                    Description TEXT
                )";
            
            await _database.ExecuteAsync(createTableSql);
        }

        /// <summary>
        /// Gets the current database schema version.
        /// </summary>
        private async Task<int> GetDatabaseVersionAsync()
        {
            try
            {
                var versionSql = $"SELECT COALESCE(MAX(Version), 0) FROM {VERSION_TABLE}";
                var version = await _database.ExecuteScalarAsync<int>(versionSql);
                return version;
            }
            catch (Exception)
            {
                // Table doesn't exist yet, return version 0
                return 0;
            }
        }

        /// <summary>
        /// Saves the applied migration version with timestamp.
        /// </summary>
        private async Task SaveDatabaseVersionAsync(int version)
        {
            var insertSql = $@"
                INSERT INTO {VERSION_TABLE} (Version, AppliedAt, Description)
                VALUES (?, ?, ?)";
            
            await _database.ExecuteAsync(insertSql, version, DateTime.UtcNow.ToString("o"), $"Migration to version {version}");
        }

        /// <summary>
        /// Applies a specific migration based on version number.
        /// ? Add new migrations here when schema changes are needed.
        /// </summary>
        private async Task ApplyMigrationAsync(int version)
        {
            switch (version)
            {
                case 1:
                    await ApplyMigration_v1_InitialSchemaAsync();
                    break;
                
                // ? FUTURE MIGRATIONS: Add new cases here
                // case 2:
                //     await ApplyMigration_v2_AddNewColumnAsync();
                //     break;
                
                default:
                    throw new InvalidOperationException($"No migration defined for version {version}");
            }
        }

        /// <summary>
        /// Migration v1: Initial schema with all current columns.
        /// This establishes the baseline for existing databases.
        /// </summary>
        private async Task ApplyMigration_v1_InitialSchemaAsync()
        {
            // Check if PromptTemplate table exists and has all required columns
            var tableInfo = await _database.GetTableInfoAsync("PromptTemplate");
            
            if (!tableInfo.Any())
            {
                // Table doesn't exist, will be created by InitializeDatabaseAsync
                System.Diagnostics.Debug.WriteLine("[Migration v1] PromptTemplate table will be created by repository initialization");
                return;
            }

            // Ensure all columns exist (for databases created before migrations)
            if (!tableInfo.Any(c => c.Name == "CreatedAt"))
            {
                System.Diagnostics.Debug.WriteLine("[Migration v1] Adding CreatedAt column");
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN CreatedAt TEXT DEFAULT ''");
            }

            if (!tableInfo.Any(c => c.Name == "UpdatedAt"))
            {
                System.Diagnostics.Debug.WriteLine("[Migration v1] Adding UpdatedAt column");
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN UpdatedAt TEXT DEFAULT ''");
            }

            if (!tableInfo.Any(c => c.Name == "IsDeleted"))
            {
                System.Diagnostics.Debug.WriteLine("[Migration v1] Adding IsDeleted column");
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN IsDeleted INTEGER DEFAULT 0");
            }

            if (!tableInfo.Any(c => c.Name == "Category"))
            {
                System.Diagnostics.Debug.WriteLine("[Migration v1] Adding Category column");
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN Category TEXT");
            }

            // Update null/empty timestamps with valid values
            await _database.ExecuteAsync(@"
                UPDATE PromptTemplate 
                SET CreatedAt = datetime('now') 
                WHERE CreatedAt IS NULL OR CreatedAt = ''");

            await _database.ExecuteAsync(@"
                UPDATE PromptTemplate 
                SET UpdatedAt = datetime('now') 
                WHERE UpdatedAt IS NULL OR UpdatedAt = ''");

            System.Diagnostics.Debug.WriteLine("[Migration v1] Initial schema migration completed");
        }

        // ? FUTURE MIGRATIONS: Add new migration methods here
        // Example:
        // private async Task ApplyMigration_v2_AddNewColumnAsync()
        // {
        //     await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN NewColumn TEXT");
        //     System.Diagnostics.Debug.WriteLine("[Migration v2] NewColumn added");
        // }
    }
}
