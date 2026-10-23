using DomainPromptTemplate = QuickPrompt.Domain.Entities.PromptTemplate;
using LegacyPromptTemplate = QuickPrompt.Models.PromptTemplate;
using DomainPromptCategory = QuickPrompt.Domain.Enums.PromptCategory;
using LegacyPromptCategory = QuickPrompt.Models.Enums.PromptCategory;

namespace QuickPrompt.Shared.Mappers;

/// <summary>
/// Extension methods to convert between Domain and Legacy PromptTemplate models.
/// TODO: Remove in FASE 4 when UI layer is fully refactored to use Domain entities.
/// </summary>
public static class PromptTemplateExtensions
{
    /// <summary>
    /// Convert Domain PromptTemplate to Legacy PromptTemplate.
    /// </summary>
    public static LegacyPromptTemplate ToLegacy(this DomainPromptTemplate domain)
    {
        return new LegacyPromptTemplate
        {
            Id = domain.Id,
            Title = domain.Title,
            Template = domain.Template,
            Description = domain.Description,
            Category = (LegacyPromptCategory)domain.Category,
            Variables = domain.Variables,
            IsFavorite = domain.IsFavorite,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsActive = domain.IsActive,
            DeletedAt = domain.DeletedAt
        };
    }

    /// <summary>
    /// Convert Legacy PromptTemplate to Domain PromptTemplate.
    /// </summary>
    public static DomainPromptTemplate ToDomain(this LegacyPromptTemplate legacy)
    {
        return new DomainPromptTemplate
        {
            Id = legacy.Id,
            Title = legacy.Title,
            Template = legacy.Template,
            Description = legacy.Description,
            Category = (DomainPromptCategory)legacy.Category,
            Variables = legacy.Variables,
            IsFavorite = legacy.IsFavorite,
            CreatedAt = legacy.CreatedAt,
            UpdatedAt = legacy.UpdatedAt,
            IsActive = legacy.IsActive,
            DeletedAt = legacy.DeletedAt
        };
    }
}
