using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Domain.Interfaces;

/// <summary>
/// Repository contract for PromptTemplate entities.
/// This interface belongs to the Domain layer and defines the contract
/// that Infrastructure must implement.
/// </summary>
public interface IPromptRepository
{
    Task<PromptTemplate?> GetByIdAsync(Guid id);
    
    Task<List<PromptTemplate>> GetAllAsync();
    
    Task<List<PromptTemplate>> GetByBlockAsync(
        int offset,
        int limit,
        PromptFilter filter = PromptFilter.All,
        string? searchTerm = null,
        string? category = null);
    
    Task<int> GetTotalCountAsync(
        string? searchTerm = null,
        PromptFilter filter = PromptFilter.All,
        string? category = null);
    
    Task<Guid> AddAsync(PromptTemplate prompt);
    
    Task<bool> UpdateAsync(PromptTemplate prompt);
    
    Task<bool> DeleteAsync(Guid id);
    
    Task<bool> DeleteAllAsync();
    
    Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite);
}
