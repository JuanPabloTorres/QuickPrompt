using QuickPrompt.Services.ServiceInterfaces;
using DomainFinalPrompt = QuickPrompt.Domain.Entities.FinalPrompt;
using ModelsFinalPrompt = QuickPrompt.Models.FinalPrompt;

namespace QuickPrompt.Infrastructure.Repositories;

/// <summary>
/// Adapter that implements Domain.Interfaces.IFinalPromptRepository
/// using the legacy Services.ServiceInterfaces.IFinalPromptRepository.
/// This allows Use Cases (which depend on Domain interfaces) to work
/// with the existing legacy repository implementation.
/// </summary>
public class FinalPromptRepositoryAdapter : Domain.Interfaces.IFinalPromptRepository
{
    private readonly IFinalPromptRepository _legacyRepository;

    public FinalPromptRepositoryAdapter(IFinalPromptRepository legacyRepository)
    {
        _legacyRepository = legacyRepository ?? throw new ArgumentNullException(nameof(legacyRepository));
    }

    public async Task<DomainFinalPrompt?> GetByIdAsync(Guid id)
    {
        var legacyPrompt = await _legacyRepository.GetByIdAsync(id);
        return legacyPrompt != null ? MapToDomain(legacyPrompt) : null;
    }

    public async Task<List<DomainFinalPrompt>> GetAllAsync()
    {
        var legacyPrompts = await _legacyRepository.GetAllAsync();
        return legacyPrompts.Select(MapToDomain).ToList();
    }

    public async Task<List<DomainFinalPrompt>> GetByCategoryAsync(Domain.Enums.PromptCategory category)
    {
        // Legacy repository uses DTOs for category filtering
        // We need to convert to the Models enum
        var legacyCategory = (Models.Enums.PromptCategory)(int)category;
        var legacyDTOs = await _legacyRepository.GetFinalPromptsByCategoryAsync(legacyCategory);
        
        // Map DTOs to domain entities
        return legacyDTOs.Select(dto => new DomainFinalPrompt
        {
            Id = Guid.NewGuid(), // DTO doesn't have ID, generate new one
            CompletedText = dto.CompletedText ?? string.Empty,
            SourcePromptId = dto.SourcePromptId,
            IsFavorite = false, // DTO doesn't have this field
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();
    }

    public async Task<DomainFinalPrompt?> FindByCompletedTextAsync(string completedText)
    {
        var legacyPrompt = await _legacyRepository.FindByCompletedTextAsync(completedText);
        return legacyPrompt != null ? MapToDomain(legacyPrompt) : null;
    }

    public async Task<Guid> AddAsync(DomainFinalPrompt prompt)
    {
        var legacyPrompt = MapToModels(prompt);
        await _legacyRepository.SaveAsync(legacyPrompt);
        return legacyPrompt.Id;
    }

    public async Task<bool> UpdateAsync(DomainFinalPrompt prompt)
    {
        var legacyPrompt = MapToModels(prompt);
        var result = await _legacyRepository.SaveAsync(legacyPrompt);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _legacyRepository.DeleteAsync(id);
    }

    public async Task<bool> DeleteAllAsync()
    {
        return await _legacyRepository.DeleteAllAsync();
    }

    public async Task<bool> DeleteByCompletedTextAsync(string completedText)
    {
        return await _legacyRepository.DeleteByCompletedTextAsync(completedText);
    }

    public async Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
    {
        return await _legacyRepository.UpdateFavoriteStatusAsync(id, isFavorite);
    }

    // ==================== MAPPING METHODS ====================

    /// <summary>
    /// Maps Models.FinalPrompt to Domain.Entities.FinalPrompt
    /// </summary>
    private static DomainFinalPrompt MapToDomain(ModelsFinalPrompt modelsPrompt)
    {
        return new DomainFinalPrompt
        {
            Id = modelsPrompt.Id,
            CompletedText = modelsPrompt.CompletedText ?? string.Empty,
            SourcePromptId = modelsPrompt.SourcePromptId,
            IsFavorite = modelsPrompt.IsFavorite,
            CreatedAt = modelsPrompt.CreatedAt,
            UpdatedAt = modelsPrompt.UpdatedAt,
            DeletedAt = modelsPrompt.DeletedAt,
            IsActive = modelsPrompt.IsActive
        };
    }

    /// <summary>
    /// Maps Domain.Entities.FinalPrompt to Models.FinalPrompt
    /// </summary>
    private static ModelsFinalPrompt MapToModels(DomainFinalPrompt domainPrompt)
    {
        return new ModelsFinalPrompt
        {
            Id = domainPrompt.Id,
            CompletedText = domainPrompt.CompletedText,
            SourcePromptId = domainPrompt.SourcePromptId,
            IsFavorite = domainPrompt.IsFavorite,
            CreatedAt = domainPrompt.CreatedAt,
            UpdatedAt = domainPrompt.UpdatedAt,
            DeletedAt = domainPrompt.DeletedAt,
            IsActive = domainPrompt.IsActive
        };
    }
}
