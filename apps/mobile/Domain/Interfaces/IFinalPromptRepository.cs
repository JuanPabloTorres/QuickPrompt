using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Domain.Interfaces;

/// <summary>
/// Repository contract for FinalPrompt (completed prompts) entities.
/// This interface belongs to the Domain layer and defines the contract
/// that Infrastructure must implement.
/// </summary>
public interface IFinalPromptRepository
{
    Task<FinalPrompt?> GetByIdAsync(Guid id);
    
    Task<List<FinalPrompt>> GetAllAsync();
    
    Task<List<FinalPrompt>> GetByCategoryAsync(PromptCategory category);
    
    Task<FinalPrompt?> FindByCompletedTextAsync(string completedText);
    
    Task<Guid> AddAsync(FinalPrompt prompt);
    
    Task<bool> UpdateAsync(FinalPrompt prompt);
    
    Task<bool> DeleteAsync(Guid id);
    
    Task<bool> DeleteAllAsync();
    
    Task<bool> DeleteByCompletedTextAsync(string completedText);
    
    Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite);
}
