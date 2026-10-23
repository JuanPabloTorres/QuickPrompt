using QuickPrompt.Engines.Execution;
using QuickPrompt.History.Models;
using QuickPrompt.History.Repositories;
using QuickPrompt.History.Sync;

namespace QuickPrompt.History
{
    public class ExecutionHistoryIntegration
    {
        private readonly IExecutionHistoryRepository _localRepo;
        private readonly SyncService _syncService;
        private readonly string _deviceId;

        public ExecutionHistoryIntegration(IExecutionHistoryRepository localRepo, SyncService syncService, string deviceId)
        {
            _localRepo = localRepo;
            _syncService = syncService;
            _deviceId = deviceId;
        }

        public async Task RecordExecutionAsync(EngineExecutionResult result, string engineId, string prompt)
        {
            var entry = new ExecutionHistoryEntry
            {
                Id = Guid.NewGuid(),
                EngineId = engineId,
                PromptCompiled = prompt,
                ExecutedAt = DateTime.UtcNow,
                Status = result.Success ? "Success" : "Failed",
                UsedFallback = result.UsedFallback,
                DeviceId = _deviceId,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            await _localRepo.AddAsync(entry);
            _syncService.Enqueue(entry);
        }
    }
}