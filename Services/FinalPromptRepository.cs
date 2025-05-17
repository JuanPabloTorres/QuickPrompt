using QuickPrompt.Models;
using QuickPrompt.Services.ServiceInterfaces;
using SQLite;

namespace QuickPrompt.Services
{
    public class FinalPromptRepository : IFinalPromptRepository
    {
        private readonly SQLiteAsyncConnection _database;

        private const string DatabaseName = "QuickPrompt.db3";

        public FinalPromptRepository()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);

            _database = new SQLiteAsyncConnection(dbPath);

            Task.Run(async () => await InitializeDatabaseAsync());
        }

      

        public async Task InitializeDatabaseAsync()
        {
            // Crear tabla si no existe
            await _database.CreateTableAsync<FinalPrompt>();

            // Verificar columnas y agregarlas si faltan
            var tableInfo = await _database.GetTableInfoAsync(nameof(FinalPrompt));

            if (!tableInfo.Any(c => c.Name == "CompletedText"))
                await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN CompletedText TEXT DEFAULT ''");

            if (!tableInfo.Any(c => c.Name == "SourcePromptId"))
                await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN SourcePromptId TEXT DEFAULT");

            if (!tableInfo.Any(c => c.Name == "CreatedAt"))
                await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN CreatedAt TEXT DEFAULT ''");

            if (!tableInfo.Any(c => c.Name == "UpdatedAt"))
                await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN UpdatedAt TEXT DEFAULT ''");

            if (!tableInfo.Any(c => c.Name == "IsDeleted"))
                await _database.ExecuteAsync("ALTER TABLE FinalPrompt ADD COLUMN IsDeleted INTEGER DEFAULT 0");

            // Actualizar valores nulos con timestamps válidos
            await _database.ExecuteAsync("UPDATE FinalPrompt SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL OR CreatedAt = ''");

            await _database.ExecuteAsync("UPDATE FinalPrompt SET UpdatedAt = datetime('now') WHERE UpdatedAt IS NULL OR UpdatedAt = ''");
        }

        public async Task<List<FinalPrompt>> GetAllAsync()
        {
            return await _database.Table<FinalPrompt>()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<FinalPrompt> GetByIdAsync(Guid id)
        {
            return await _database.Table<FinalPrompt>()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<int> SaveAsync(FinalPrompt prompt)
        {
            if (prompt.Id == Guid.Empty)
                prompt.Id = Guid.NewGuid();

            if (prompt.CreatedAt == default)
                prompt.CreatedAt = DateTime.Now;

            return await _database.InsertOrReplaceAsync(prompt);
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
    }
}