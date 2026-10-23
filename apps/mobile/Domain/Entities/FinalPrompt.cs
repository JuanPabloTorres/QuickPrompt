namespace QuickPrompt.Domain.Entities;

/// <summary>
/// Domain entity representing a completed/final prompt ready for AI consumption.
/// Pure domain model without UI or infrastructure dependencies.
/// </summary>
public class FinalPrompt : BaseEntity
{
    public string CompletedText { get; set; } = string.Empty;
    
    public bool IsFavorite { get; set; }
    
    public Guid? SourcePromptId { get; set; }

    public FinalPrompt()
    {
    }

    public FinalPrompt(string completedText, Guid? sourcePromptId = null)
    {
        if (string.IsNullOrWhiteSpace(completedText))
            throw new ArgumentException("Completed text cannot be empty", nameof(completedText));

        CompletedText = completedText;
        SourcePromptId = sourcePromptId;
    }

    /// <summary>
    /// Factory method to create a final prompt from a template execution.
    /// </summary>
    public static FinalPrompt CreateFromTemplate(
        string completedText,
        Guid sourcePromptId)
    {
        if (sourcePromptId == Guid.Empty)
            throw new ArgumentException("Source prompt ID cannot be empty", nameof(sourcePromptId));

        return new FinalPrompt
        {
            CompletedText = completedText,
            SourcePromptId = sourcePromptId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Toggle favorite status.
    /// </summary>
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        UpdatedAt = DateTime.UtcNow;
    }
}
