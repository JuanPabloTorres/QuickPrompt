using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuickPrompt.History.Models;
using SQLite;

namespace QuickPrompt.History.Repositories
{
    public class SqliteExecutionHistoryRepository : IExecutionHistoryRepository
    {
        private readonly SQLiteAsyncConnection _db;
        private bool _isInitialized = false;

        public SqliteExecutionHistoryRepository(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
            // Initialize async without blocking constructor
            _ = InitializeDatabaseAsync();
        }

        private async Task InitializeDatabaseAsync()
        {
            if (_isInitialized) return;

            await _db.CreateTableAsync<ExecutionHistoryEntry>();

            // Check for missing columns and add them (migration)
            var tableInfo = await _db.GetTableInfoAsync(nameof(ExecutionHistoryEntry));

            if (!tableInfo.Any(c => c.Name == nameof(ExecutionHistoryEntry.IsSynced)))
                await _db.ExecuteAsync($"ALTER TABLE {nameof(ExecutionHistoryEntry)} ADD COLUMN IsSynced INTEGER DEFAULT 0");

            if (!tableInfo.Any(c => c.Name == nameof(ExecutionHistoryEntry.SyncedAt)))
                await _db.ExecuteAsync($"ALTER TABLE {nameof(ExecutionHistoryEntry)} ADD COLUMN SyncedAt TEXT DEFAULT NULL");

            _isInitialized = true;
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
                await InitializeDatabaseAsync();
        }

        public async Task AddAsync(ExecutionHistoryEntry entry)
        {
            await EnsureInitializedAsync();
            
            // Set sync tracking fields
            entry.IsSynced = false;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.SyncedAt = null;
            
            await _db.InsertAsync(entry);
        }

        public async Task UpdateAsync(ExecutionHistoryEntry entry)
        {
            await EnsureInitializedAsync();
            
            entry.UpdatedAt = DateTime.UtcNow;
            await _db.UpdateAsync(entry);
        }

        public async Task DeleteAsync(Guid id)
        {
            await EnsureInitializedAsync();
            await _db.Table<ExecutionHistoryEntry>().DeleteAsync(x => x.Id == id);
        }

        public async Task<ExecutionHistoryEntry?> GetByIdAsync(Guid id)
        {
            await EnsureInitializedAsync();
            return await _db.Table<ExecutionHistoryEntry>().Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<ExecutionHistoryEntry>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _db.Table<ExecutionHistoryEntry>().ToListAsync();
        }

        public async Task<List<ExecutionHistoryEntry>> GetPendingSyncAsync()
        {
            await EnsureInitializedAsync();
            // Return only entries that are NOT synced and NOT deleted
            return await _db.Table<ExecutionHistoryEntry>()
                .Where(x => !x.IsSynced && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task MarkAsSyncedAsync(IEnumerable<Guid> entryIds)
        {
            await EnsureInitializedAsync();
            
            var now = DateTime.UtcNow;
            
            // Batch update for efficiency
            foreach (var id in entryIds)
            {
                await _db.ExecuteAsync(
                    $"UPDATE {nameof(ExecutionHistoryEntry)} SET IsSynced = 1, SyncedAt = ?, UpdatedAt = ? WHERE Id = ?",
                    now, now, id);
            }
        }
    }
}