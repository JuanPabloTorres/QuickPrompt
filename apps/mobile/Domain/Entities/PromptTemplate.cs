using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Domain.Entities;

/// <summary>
/// Domain entity representing a prompt template with variables.
/// Pure domain model without UI or infrastructure dependencies.
/// </summary>
public class PromptTemplate : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    
    public string Template { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public PromptCategory Category { get; set; } = PromptCategory.General;
    
    public Dictionary<string, string> Variables { get; set; } = new();
    
    public bool IsFavorite { get; set; }

    public PromptTemplate()
    {
    }

    public PromptTemplate(
        string title,
        string template,
        string description,
        PromptCategory category = PromptCategory.General)
    {
        Title = title;
        Template = template;
        Description = description;
        Category = category;
        Variables = new Dictionary<string, string>();
    }

    /// <summary>
    /// Factory method to create a prompt template with extracted variables.
    /// </summary>
    public static PromptTemplate Create(
        string title,
        string template,
        string description,
        Dictionary<string, string> variables,
        PromptCategory category = PromptCategory.General)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be empty", nameof(template));

        return new PromptTemplate
        {
            Title = title,
            Template = template,
            Description = string.IsNullOrWhiteSpace(description) ? "N/A" : description,
            Variables = variables ?? new Dictionary<string, string>(),
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update the template and refresh variables.
    /// </summary>
    public void UpdateTemplate(string newTemplate, Dictionary<string, string> newVariables)
    {
        if (string.IsNullOrWhiteSpace(newTemplate))
            throw new ArgumentException("Template cannot be empty", nameof(newTemplate));

        Template = newTemplate;
        Variables = newVariables ?? new Dictionary<string, string>();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Toggle favorite status.
    /// </summary>
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Mark as deleted (soft delete).
    /// </summary>
    public void MarkAsDeleted()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
