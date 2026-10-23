using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using DomainPromptTemplate = QuickPrompt.Domain.Entities.PromptTemplate;
using DomainFinalPrompt = QuickPrompt.Domain.Entities.FinalPrompt;
using DomainPromptCategory = QuickPrompt.Domain.Enums.PromptCategory;
using LegacyPromptTemplate = QuickPrompt.Models.PromptTemplate;
using LegacyFinalPrompt = QuickPrompt.Models.FinalPrompt;
using LegacyPromptCategory = QuickPrompt.Models.Enums.PromptCategory;

namespace QuickPrompt.Shared.Mappers;

/// <summary>
/// Extension methods to map between Legacy models and Domain entities.
/// TEMPORARY PHASE 2 SOLUTION - Will be removed in Phase 3 when legacy models are deprecated.
/// </summary>
public static class EntityMappers
{
    // ==================== PROMPT TEMPLATE MAPPERS ====================

    public static DomainPromptTemplate ToDomainEntity(this LegacyPromptTemplate legacy)
    {
        if (legacy == null) return null!;

        return new DomainPromptTemplate
        {
            Id = legacy.Id,
            Title = legacy.Title,
            Description = legacy.Description,
            Template = legacy.Template,
            Variables = legacy.Variables ?? new Dictionary<string, string>(),
            Category = (DomainPromptCategory)((int)legacy.Category),
            IsFavorite = legacy.IsFavorite,
            CreatedAt = legacy.CreatedAt,
            UpdatedAt = legacy.UpdatedAt
        };
    }

    public static LegacyPromptTemplate ToLegacyModel(this DomainPromptTemplate domain)
    {
        if (domain == null) return null!;

        return new LegacyPromptTemplate
        {
            Id = domain.Id,
            Title = domain.Title,
            Description = domain.Description,
            Template = domain.Template,
            Variables = domain.Variables ?? new Dictionary<string, string>(),
            Category = (LegacyPromptCategory)((int)domain.Category),
            IsFavorite = domain.IsFavorite,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }

    // ==================== FINAL PROMPT MAPPERS ====================

    public static DomainFinalPrompt ToDomainEntity(this LegacyFinalPrompt legacy)
    {
        if (legacy == null) return null!;

        return new DomainFinalPrompt
        {
            Id = legacy.Id,
            CompletedText = legacy.CompletedText,
            SourcePromptId = legacy.SourcePromptId,
            IsFavorite = legacy.IsFavorite,
            CreatedAt = legacy.CreatedAt
        };
    }

    public static LegacyFinalPrompt ToLegacyModel(this DomainFinalPrompt domain)
    {
        if (domain == null) return null!;

        return new LegacyFinalPrompt
        {
            Id = domain.Id,
            CompletedText = domain.CompletedText,
            SourcePromptId = domain.SourcePromptId,
            IsFavorite = domain.IsFavorite,
            CreatedAt = domain.CreatedAt
        };
    }
}
