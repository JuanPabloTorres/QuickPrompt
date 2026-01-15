using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuickPrompt.History.Models;

namespace QuickPrompt.History.Repositories
{
    /// <summary>
    /// Null implementation of cloud repository for when Firestore is not configured.
    /// All operations are no-ops to prevent crashes.
    /// </summary>
    public class NullExecutionHistoryCloudRepository : IExecutionHistoryCloudRepository
    {
        public Task BatchUpsertAsync(IEnumerable<ExecutionHistoryEntry> entries)
        {
            // No-op: Firestore not configured
            return Task.CompletedTask;
        }

        public Task<List<ExecutionHistoryEntry>> GetUpdatesSinceAsync(DateTime sinceUtc)
        {
            // No-op: Firestore not configured
            return Task.FromResult(new List<ExecutionHistoryEntry>());
        }
    }
}
