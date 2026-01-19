using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services.ServiceInterfaces
{
    /// <summary>
    /// Legacy repository interface for PromptTemplate.
    /// ✅ PHASE 3: Migrated to Domain.Entities (Clean Architecture)
    /// ✅ SPRINT 2: Nullable reference types enabled for null-safety
    /// </summary>
    public interface IPromptRepository
    {
        Task InitializeDatabaseAsync();

        Task InsertDefaultPromptsAsync();

        Task<int> SavePromptAsync(PromptTemplate prompt);

        /// <summary>
        /// Gets a prompt by ID.
        /// </summary>
        /// <returns>PromptTemplate if found, null otherwise</returns>
        Task<PromptTemplate?> GetPromptByIdAsync(Guid id);

        Task<bool> DeletePromptAsync(Guid id);

        Task<bool> DeleteAllPromptsAsync();

        /// <summary>
        /// Updates an existing prompt.
        /// </summary>
        /// <returns>Updated PromptTemplate if successful, null if not found</returns>
        Task<PromptTemplate?> UpdatePromptAsync(Guid id, string newTitle, string newTemplate, string newDescription, Dictionary<string, string> newVariables, PromptCategory selectedCategory);

        Task<List<PromptTemplate>> GetAllPromptsAsync();

        Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, Filters dateFilter = Filters.All, string? filterText = "", string? selectedCategory = null);

        Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite);

        Task<int> GetTotalPromptsCountAsync(string? filterText, Filters dateFilter, string? category);

        Task RestoreDatabaseAsync(SQLite.SQLiteAsyncConnection conn);
    }
}