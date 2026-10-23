using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuickPrompt.History.Models;

namespace QuickPrompt.History.Repositories
{
    public interface IExecutionHistoryRepository
    {
        Task AddAsync(ExecutionHistoryEntry entry);
        Task UpdateAsync(ExecutionHistoryEntry entry);
        Task DeleteAsync(Guid id);
        Task<ExecutionHistoryEntry?> GetByIdAsync(Guid id);
        Task<List<ExecutionHistoryEntry>> GetAllAsync();
        Task<List<ExecutionHistoryEntry>> GetPendingSyncAsync();
        Task MarkAsSyncedAsync(IEnumerable<Guid> entryIds);
    }
}