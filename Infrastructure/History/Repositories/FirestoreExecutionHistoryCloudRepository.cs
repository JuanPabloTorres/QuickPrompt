using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using QuickPrompt.History.Models;

namespace QuickPrompt.History.Repositories
{
    public class FirestoreExecutionHistoryCloudRepository : IExecutionHistoryCloudRepository
    {
        private readonly CollectionReference _collection;

        public FirestoreExecutionHistoryCloudRepository(FirestoreDb db, string userId)
        {
            _collection = db.Collection("users").Document(userId).Collection("history");
        }

        public async Task BatchUpsertAsync(IEnumerable<ExecutionHistoryEntry> entries)
        {
            var batch = _collection.Database.StartBatch();
            foreach (var entry in entries)
            {
                var doc = _collection.Document(entry.Id.ToString());
                batch.Set(doc, entry, SetOptions.MergeAll);
            }
            await batch.CommitAsync();
        }

        public async Task<List<ExecutionHistoryEntry>> GetUpdatesSinceAsync(DateTime sinceUtc)
        {
            var snapshot = await _collection.WhereGreaterThanOrEqualTo("UpdatedAt", sinceUtc).GetSnapshotAsync();
            var result = new List<ExecutionHistoryEntry>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var entry = doc.ConvertTo<ExecutionHistoryEntry>();
                    result.Add(entry);
                }
            }
            return result;
        }
    }
}