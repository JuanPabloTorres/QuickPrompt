using QuickPrompt.Models;
using QuickPrompt.Models.DTO;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Shared.Mappers;
using SQLite;

namespace QuickPrompt.Services
{
    public class FinalPromptRepository : IFinalPromptRepository
    {
        private SQLiteAsyncConnection _database;
        private readonly DatabaseConnectionProvider _dbProvider;

        public FinalPromptRepository(DatabaseConnectionProvider provider)
        {
            _dbProvider = provider;
            _database = _dbProvider.GetConnection();
            Task.Run(async () => await InitializeDatabaseAsync());
        }

        /// <summary>
        /// Inicializa la base de datos y crea las tablas necesarias si no existen.
        /// ? PHASE 2: Uses DatabaseMigrationManager for structured schema evolution.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Starting database initialization...");

                // ? PHASE 2: Create tables first
                await _database.CreateTableAsync<FinalPrompt>();
                System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] FinalPrompt table created/verified");

                // ? PHASE 2: Apply structured migrations for FinalPrompt table
                // Note: FinalPrompt uses the same migration framework but different version tracking
                var tableInfo = await _database.GetTableInfoAsync(nameof(FinalPrompt));

                if (!tableInfo.Any(c => c.Name == "CompletedText"))
                {
                    System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Adding CompletedText column");
                    await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN CompletedText TEXT DEFAULT ''");
                }

                if (!tableInfo.Any(c => c.Name == "SourcePromptId"))
                {
                    System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Adding SourcePromptId column");
                    await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN SourcePromptId TEXT DEFAULT ''");
                }

                if (!tableInfo.Any(c => c.Name == "CreatedAt"))
                {
                    System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Adding CreatedAt column");
                    await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN CreatedAt TEXT DEFAULT ''");
                }

                if (!tableInfo.Any(c => c.Name == "UpdatedAt"))
                {
                    System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Adding UpdatedAt column");
                    await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN UpdatedAt TEXT DEFAULT ''");
                }

                if (!tableInfo.Any(c => c.Name == "IsDeleted"))
                {
                    System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Adding IsDeleted column");
                    await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN IsDeleted INTEGER DEFAULT 0");
                }

                // Actualizar valores nulos con timestamps válidos
                await _database.ExecuteAsync(@"
                    UPDATE FinalPrompt 
                    SET CreatedAt = datetime('now') 
                    WHERE CreatedAt IS NULL OR CreatedAt = ''");

                await _database.ExecuteAsync(@"
                    UPDATE FinalPrompt 
                    SET UpdatedAt = datetime('now') 
                    WHERE UpdatedAt IS NULL OR UpdatedAt = ''");

                System.Diagnostics.Debug.WriteLine("[FinalPromptRepository] Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FinalPromptRepository] CRITICAL: Database initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FinalPromptRepository] StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException("Failed to initialize FinalPrompt database. Application cannot continue.", ex);
            }
        }

        public async Task<List<FinalPrompt>> GetAllAsync()
        {
            return await _database.Table<FinalPrompt>()
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<FinalPrompt> GetByIdAsync(Guid id)
        {
            return await _database.Table<FinalPrompt>()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<FinalPromptDTO>> GetFinalPromptsByCategoryAsync(PromptCategory category)
        {
            var sql = @"
        SELECT
            f.CompletedText,
            f.SourcePromptId,
            p.Category
        FROM FinalPrompt f
        LEFT  JOIN PromptTemplate p ON f.SourcePromptId = p.Id
        WHERE p.Category = ?
    ";

            return await _database.QueryAsync<FinalPromptDTO>(sql, (int)category);
        }

        public async Task<int> SaveAsync(FinalPrompt prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt.CompletedText))
                return 0;

            // Obtener todos y comparar en memoria (pequeño volumen, rendimiento aceptable)
            var allPrompts = await _database.Table<FinalPrompt>().ToListAsync();

            bool alreadyExists = allPrompts
                .Any(p => string.Equals(
                    p.CompletedText?.Trim(),
                    prompt.CompletedText?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                return 0; // No guardar duplicado

            // Continuar con guardado normal
            if (prompt.Id == Guid.Empty)
                prompt.Id = Guid.NewGuid();

            if (prompt.CreatedAt == default)
                prompt.CreatedAt = DateTime.Now;

            return await _database.InsertAsync(prompt);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var prompt = await GetByIdAsync(id);
            return prompt != null && await _database.DeleteAsync(prompt) > 0;
        }

        public async Task<bool> DeleteAllAsync()
        {
            var all = await GetAllAsync();
            return all.Any() && await _database.DeleteAllAsync<FinalPrompt>() > 0;
        }

        public async Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
        {
            var prompt = await GetByIdAsync(id);

            if (prompt == null)
                return false;

            if (prompt.IsFavorite == isFavorite)
                return false;

            prompt.IsFavorite = isFavorite;
            return await _database.UpdateAsync(prompt) > 0;
        }

        public async Task RestoreDatabaseAsync()
        {
            // Cierra cualquier operación pendiente antes de borrar
            //await _database.CloseAsync();

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");

            if (File.Exists(dbPath))
                File.Delete(dbPath);

            // Recrear la instancia de conexión y reiniciar base de datos
            var newConnection = new SQLiteAsyncConnection(dbPath);

            await InitializeDatabaseAsync();
        }

        public async Task RestoreDatabaseAsync(SQLiteAsyncConnection newConnection)
        {
            _database = newConnection;

            await InitializeDatabaseAsync(); // usa la nueva conexión para inicializar tabla PromptTemplate
        }

        public async Task<FinalPrompt?> FindByCompletedTextAsync(string completedText)
        {
            var normalized = completedText?.Trim().ToLowerInvariant();

            return await _database.Table<FinalPrompt>()
                .Where(x => x.CompletedText != null)
                .ToListAsync()
                .ContinueWith(t =>
                    t.Result.FirstOrDefault(x =>
                        x.CompletedText.Trim().ToLowerInvariant() == normalized));
        }

        public async Task<bool> DeleteByCompletedTextAsync(string completedText)
        {
            var prompt = await FindByCompletedTextAsync(completedText);
            if (prompt == null)
                return false;
            return await _database.DeleteAsync(prompt) > 0;
        }
    }
}