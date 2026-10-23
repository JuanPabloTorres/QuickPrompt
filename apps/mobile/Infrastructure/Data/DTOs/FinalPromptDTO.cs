using QuickPrompt.Domain.Entities;
using SQLite;

namespace QuickPrompt.Infrastructure.Data.DTOs;

/// <summary>
/// Data Transfer Object for FinalPrompt with SQLite attributes.
/// This DTO is used in the Infrastructure layer to persist domain entities.
/// Maps between Domain.Entities.FinalPrompt and database storage.
/// </summary>
public class FinalPromptDTO
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string CompletedText { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public Guid? SourcePromptId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Convert Domain Entity to DTO.
    /// </summary>
    public static FinalPromptDTO FromDomain(FinalPrompt domain)
    {
        return new FinalPromptDTO
        {
            Id = domain.Id,
            CompletedText = domain.CompletedText,
            IsFavorite = domain.IsFavorite,
            SourcePromptId = domain.SourcePromptId,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsActive = domain.IsActive,
            DeletedAt = domain.DeletedAt
        };
    }

    /// <summary>
    /// Convert DTO to Domain Entity.
    /// </summary>
    public FinalPrompt ToDomain()
    {
        return new FinalPrompt
        {
            Id = this.Id,
            CompletedText = this.CompletedText,
            IsFavorite = this.IsFavorite,
            SourcePromptId = this.SourcePromptId,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
            IsActive = this.IsActive,
            DeletedAt = this.DeletedAt
        };
    }
}
