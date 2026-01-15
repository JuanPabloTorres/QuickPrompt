using System;
using SQLite;

namespace QuickPrompt.History.Models
{
    [Table("ExecutionHistoryEntry")]
    public class ExecutionHistoryEntry
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        
        public string EngineId { get; set; } = string.Empty;
        public string PromptCompiled { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        
        // Sync tracking fields
        public bool IsSynced { get; set; }
        public DateTime? SyncedAt { get; set; }
    }
}