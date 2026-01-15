using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuickPrompt.History.Models;

namespace QuickPrompt.History.Repositories
{
    public interface IExecutionHistoryCloudRepository
    {
        Task BatchUpsertAsync(IEnumerable<ExecutionHistoryEntry> entries);
        Task<List<ExecutionHistoryEntry>> GetUpdatesSinceAsync(DateTime sinceUtc);
    }
}