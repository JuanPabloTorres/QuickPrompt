﻿using QuickPrompt.Models;
using QuickPrompt.Models.DTO;
using QuickPrompt.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services.ServiceInterfaces
{
    public interface IFinalPromptRepository
    {
        Task InitializeDatabaseAsync();

        Task<List<FinalPrompt>> GetAllAsync();

        Task<FinalPrompt> GetByIdAsync(Guid id);

        Task<int> SaveAsync(FinalPrompt prompt);

        Task<bool> DeleteAsync(Guid id);

        Task<bool> DeleteAllAsync();

        Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite);

        Task RestoreDatabaseAsync(SQLite.SQLiteAsyncConnection conn);

        Task<List<FinalPromptDTO>> GetFinalPromptsByCategoryAsync(PromptCategory category);

        Task<FinalPrompt?> FindByCompletedTextAsync(string completedText);

        Task<bool> DeleteByCompletedTextAsync(string completedText);
    }
}