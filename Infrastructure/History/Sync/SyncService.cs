using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickPrompt.History.Models;
using QuickPrompt.History.Repositories;

namespace QuickPrompt.History.Sync
{
    public class SyncQueueItem
    {
        public ExecutionHistoryEntry Entry { get; set; } = default!;
        public DateTime QueuedAt { get; set; }
    }

    public class SyncService
    {
        private readonly IExecutionHistoryRepository _localRepo;
        private readonly IExecutionHistoryCloudRepository _cloudRepo;
        private readonly ConcurrentQueue<SyncQueueItem> _queue = new();
        private DateTime _lastSync = DateTime.MinValue;
        private readonly TimeSpan _throttle = TimeSpan.FromSeconds(30);
        private readonly Func<bool> _isUserLoggedIn;
        private readonly Func<bool> _isSyncEnabled;

        public SyncService(IExecutionHistoryRepository localRepo, IExecutionHistoryCloudRepository cloudRepo, Func<bool> isUserLoggedIn, Func<bool> isSyncEnabled)
        {
            _localRepo = localRepo;
            _cloudRepo = cloudRepo;
            _isUserLoggedIn = isUserLoggedIn;
            _isSyncEnabled = isSyncEnabled;
        }

        public void Enqueue(ExecutionHistoryEntry entry)
        {
            _queue.Enqueue(new SyncQueueItem { Entry = entry, QueuedAt = DateTime.UtcNow });
        }

        public async Task SyncNowAsync(CancellationToken cancellationToken = default)
        {
            if (!_isUserLoggedIn() || !_isSyncEnabled())
                return;
            
            if (DateTime.UtcNow - _lastSync < _throttle)
                return;
            
            _lastSync = DateTime.UtcNow;

            // Get pending entries from database (not just queue)
            var pendingEntries = await _localRepo.GetPendingSyncAsync();
            
            // Also add any queued items
            var queuedItems = new List<ExecutionHistoryEntry>();
            while (_queue.TryDequeue(out var item))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("[Sync] Cancelled by user");
                    return;
                }
                queuedItems.Add(item.Entry);
            }
            
            // Combine and deduplicate by ID
            var allItems = pendingEntries
                .Union(queuedItems)
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .ToList();
            
            if (allItems.Count == 0)
                return;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Sync] Syncing {allItems.Count} entries to cloud");
                
                // Upload to cloud
                await _cloudRepo.BatchUpsertAsync(allItems);
                
                // Mark as synced in local database
                var entryIds = allItems.Select(e => e.Id).ToList();
                await _localRepo.MarkAsSyncedAsync(entryIds);
                
                System.Diagnostics.Debug.WriteLine($"[Sync] Successfully synced {allItems.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sync] Sync failed: {ex.Message}");
                
                // Re-queue failed items for next sync attempt
                foreach (var item in allItems)
                {
                    Enqueue(item);
                }
            }
        }
    }
}