using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Tools;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Request model for creating a new prompt.
/// </summary>
public class CreatePromptRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Use Case for creating a new prompt template.
/// Extracts business logic from MainPageViewModel.
/// </summary>
public class CreatePromptUseCase
{
    private readonly IPromptRepository _promptRepository;

    public CreatePromptUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    /// <summary>
    /// Creates a new prompt template after validation.
    /// </summary>
    /// <param name="request">The prompt creation request.</param>
    /// <returns>Result containing the created prompt or error message.</returns>
    public async Task<Result<PromptTemplate>> ExecuteAsync(CreatePromptRequest request)
    {
        if (request == null)
            return Result<PromptTemplate>.Failure("Request cannot be null", "InvalidRequest");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<PromptTemplate>.Failure("Title is required", "ValidationError");

        if (string.IsNullOrWhiteSpace(request.Template))
            return Result<PromptTemplate>.Failure("Template is required", "ValidationError");

        // Check if template contains angle brackets (variables)
        if (!AngleBraceTextHandler.ContainsAngleBraces(request.Template))
            return Result<PromptTemplate>.Failure("Template must contain at least one variable using angle brackets <variable>", "ValidationError");

        // Parse category
        if (!Enum.TryParse(typeof(PromptCategory), request.Category, out var categoryObj))
            categoryObj = PromptCategory.General;

        var category = (PromptCategory)categoryObj;

        try
        {
            // Create prompt entity
            var newPrompt = PromptTemplate.CreatePromptTemplate(
                request.Title,
                request.Description,
                request.Template,
                category);

            // Save to repository
            await _promptRepository.SavePromptAsync(newPrompt);

            return Result<PromptTemplate>.Success(newPrompt);
        }
        catch (Exception ex)
        {
            return Result<PromptTemplate>.Failure(
                $"Failed to create prompt: {ex.Message}",
                "DatabaseError");
        }
    }
}
