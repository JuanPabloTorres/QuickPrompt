using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using SQLite;

namespace QuickPrompt.Infrastructure.Data.DTOs;

/// <summary>
/// Data Transfer Object for PromptTemplate with SQLite attributes.
/// This DTO is used in the Infrastructure layer to persist domain entities.
/// Maps between Domain.Entities.PromptTemplate and database storage.
/// </summary>
public partial class PromptTemplateDTO : ObservableObject
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    [ObservableProperty]
    private string template = string.Empty;

    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private PromptCategory category = PromptCategory.General;

    /// <summary>
    /// Variables serialized as JSON for SQLite storage.
    /// </summary>
    public string VariablesJson
    {
        get => JsonConvert.SerializeObject(Variables);
        set => Variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(value) 
            ?? new Dictionary<string, string>();
    }

    [Ignore]
    public Dictionary<string, string> Variables { get; set; } = new();

    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Convert Domain Entity to DTO.
    /// </summary>
    public static PromptTemplateDTO FromDomain(PromptTemplate domain)
    {
        return new PromptTemplateDTO
        {
            Id = domain.Id,
            Title = domain.Title,
            Template = domain.Template,
            Description = domain.Description,
            Category = domain.Category,
            Variables = domain.Variables,
            IsFavorite = domain.IsFavorite,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsActive = domain.IsActive,
            DeletedAt = domain.DeletedAt
        };
    }

    /// <summary>
    /// Convert DTO to Domain Entity.
    /// </summary>
    public PromptTemplate ToDomain()
    {
        return new PromptTemplate
        {
            Id = this.Id,
            Title = this.Title,
            Template = this.Template,
            Description = this.Description,
            Category = this.Category,
            Variables = this.Variables,
            IsFavorite = this.IsFavorite,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
            IsActive = this.IsActive,
            DeletedAt = this.DeletedAt
        };
    }
}
