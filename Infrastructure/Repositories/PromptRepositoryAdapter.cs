using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using LegacyIPromptRepository = QuickPrompt.Services.ServiceInterfaces.IPromptRepository;
using DomainPromptTemplate = QuickPrompt.Domain.Entities.PromptTemplate;
using ModelsPromptTemplate = QuickPrompt.Models.PromptTemplate;
using DomainPromptFilter = QuickPrompt.Domain.Enums.PromptFilter;
using ModelsFilters = QuickPrompt.Tools.Filters;
using DomainPromptCategory = QuickPrompt.Domain.Enums.PromptCategory;
using ModelsPromptCategory = QuickPrompt.Models.Enums.PromptCategory;

namespace QuickPrompt.Infrastructure.Repositories;

/// <summary>
/// Adapter that implements Domain.Interfaces.IPromptRepository
/// using the legacy Services.ServiceInterfaces.IPromptRepository.
/// This allows Use Cases (which depend on Domain interfaces) to work
/// with the existing legacy repository implementation.
/// </summary>
public class PromptRepositoryAdapter : Domain.Interfaces.IPromptRepository
{
    private readonly LegacyIPromptRepository _legacyRepository;

    public PromptRepositoryAdapter(LegacyIPromptRepository legacyRepository)
    {
        _legacyRepository = legacyRepository ?? throw new ArgumentNullException(nameof(legacyRepository));
    }

    public async Task<DomainPromptTemplate?> GetByIdAsync(Guid id)
    {
        var legacyPrompt = await _legacyRepository.GetPromptByIdAsync(id);
        return legacyPrompt != null ? MapToDomain(legacyPrompt) : null;
    }

    public async Task<List<DomainPromptTemplate>> GetAllAsync()
    {
        var legacyPrompts = await _legacyRepository.GetAllPromptsAsync();
        return legacyPrompts.Select(MapToDomain).ToList();
    }

    public async Task<List<DomainPromptTemplate>> GetByBlockAsync(
        int offset,
        int limit,
        DomainPromptFilter filter = DomainPromptFilter.All,
        string? searchTerm = null,
        string? category = null)
    {
        var legacyFilter = MapPromptFilterToLegacyFilters(filter);
        
        var legacyPrompts = await _legacyRepository.GetPromptsByBlockAsync(
            offset,
            limit,
            legacyFilter,
            searchTerm ?? string.Empty,
            category);
        
        return legacyPrompts.Select(MapToDomain).ToList();
    }

    public async Task<int> GetTotalCountAsync(
        string? searchTerm = null,
        DomainPromptFilter filter = DomainPromptFilter.All,
        string? category = null)
    {
        var legacyFilter = MapPromptFilterToLegacyFilters(filter);
        
        return await _legacyRepository.GetTotalPromptsCountAsync(
            searchTerm ?? string.Empty,
            legacyFilter,
            category ?? string.Empty);
    }

    public async Task<Guid> AddAsync(DomainPromptTemplate prompt)
    {
        var legacyPrompt = MapToModels(prompt);
        await _legacyRepository.SavePromptAsync(legacyPrompt);
        return legacyPrompt.Id;
    }

    public async Task<bool> UpdateAsync(DomainPromptTemplate prompt)
    {
        var updated = await _legacyRepository.UpdatePromptAsync(
            prompt.Id,
            prompt.Title,
            prompt.Template,
            prompt.Description,
            prompt.Variables,
            MapCategoryToModels(prompt.Category));
        
        return updated != null;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _legacyRepository.DeletePromptAsync(id);
    }

    public async Task<bool> DeleteAllAsync()
    {
        return await _legacyRepository.DeleteAllPromptsAsync();
    }

    public async Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
    {
        return await _legacyRepository.UpdateFavoriteStatusAsync(id, isFavorite);
    }

    // ==================== MAPPING METHODS ====================

    /// <summary>
    /// Maps Models.PromptTemplate to Domain.Entities.PromptTemplate
    /// </summary>
    private static DomainPromptTemplate MapToDomain(ModelsPromptTemplate modelsPrompt)
    {
        return new DomainPromptTemplate
        {
            Id = modelsPrompt.Id,
            Title = modelsPrompt.Title,
            Template = modelsPrompt.Template,
            Description = modelsPrompt.Description,
            Category = MapCategoryToDomain(modelsPrompt.Category),
            Variables = modelsPrompt.Variables ?? new Dictionary<string, string>(),
            IsFavorite = modelsPrompt.IsFavorite,
            CreatedAt = modelsPrompt.CreatedAt,
            UpdatedAt = modelsPrompt.UpdatedAt,
            DeletedAt = modelsPrompt.DeletedAt,
            IsActive = modelsPrompt.IsActive
        };
    }

    /// <summary>
    /// Maps Domain.Entities.PromptTemplate to Models.PromptTemplate
    /// </summary>
    private static ModelsPromptTemplate MapToModels(DomainPromptTemplate domainPrompt)
    {
        return new ModelsPromptTemplate
        {
            Id = domainPrompt.Id,
            Title = domainPrompt.Title,
            Template = domainPrompt.Template,
            Description = domainPrompt.Description,
            Category = MapCategoryToModels(domainPrompt.Category),
            Variables = domainPrompt.Variables ?? new Dictionary<string, string>(),
            IsFavorite = domainPrompt.IsFavorite,
            CreatedAt = domainPrompt.CreatedAt,
            UpdatedAt = domainPrompt.UpdatedAt,
            DeletedAt = domainPrompt.DeletedAt,
            IsActive = domainPrompt.IsActive
        };
    }

    /// <summary>
    /// Maps Domain.Enums.PromptCategory to Models.Enums.PromptCategory
    /// Both enums are identical, so we cast directly.
    /// </summary>
    private static ModelsPromptCategory MapCategoryToModels(DomainPromptCategory domainCategory)
    {
        // Since both enums have identical values, we can cast directly
        return (ModelsPromptCategory)(int)domainCategory;
    }

    /// <summary>
    /// Maps Models.Enums.PromptCategory to Domain.Enums.PromptCategory
    /// Both enums are identical, so we cast directamente.
    /// </summary>
    private static DomainPromptCategory MapCategoryToDomain(ModelsPromptCategory modelsCategory)
    {
        // Since both enums have identical values, we can cast directly
        return (DomainPromptCategory)(int)modelsCategory;
    }

    /// <summary>
    /// Maps Domain.Enums.PromptFilter to Models.Enums.Filters for legacy repository.
    /// </summary>
    private static ModelsFilters MapPromptFilterToLegacyFilters(DomainPromptFilter domainFilter)
    {
        return domainFilter switch
        {
            DomainPromptFilter.All => ModelsFilters.All,
            DomainPromptFilter.Today => ModelsFilters.Today,
            DomainPromptFilter.Last7Days => ModelsFilters.Last7Days,
            DomainPromptFilter.Favorites => ModelsFilters.Favorites,
            DomainPromptFilter.NonFavorites => ModelsFilters.NonFavorites,
            _ => ModelsFilters.All
        };
    }
}
