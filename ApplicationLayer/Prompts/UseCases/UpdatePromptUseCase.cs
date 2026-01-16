using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Tools;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Request model for updating a prompt.
/// </summary>
public class UpdatePromptRequest
{
    public Guid PromptId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Use Case for updating an existing prompt template.
/// Extracts business logic from EditPromptPageViewModel.
/// </summary>
public class UpdatePromptUseCase
{
    private readonly IPromptRepository _promptRepository;

    public UpdatePromptUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    /// <summary>
    /// Updates an existing prompt template after validation.
    /// </summary>
    /// <param name="request">The prompt update request.</param>
    /// <returns>Result containing the updated prompt or error message.</returns>
    public async Task<Result<PromptTemplate>> ExecuteAsync(UpdatePromptRequest request)
    {
        if (request == null)
            return Result<PromptTemplate>.Failure("Request cannot be null", "InvalidRequest");

        if (request.PromptId == Guid.Empty)
            return Result<PromptTemplate>.Failure("Invalid prompt ID", "InvalidRequest");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<PromptTemplate>.Failure("Title is required", "ValidationError");

        if (string.IsNullOrWhiteSpace(request.Template))
            return Result<PromptTemplate>.Failure("Template is required", "ValidationError");

        // Check if template contains angle brackets (variables)
        if (!AngleBraceTextHandler.ContainsAngleBraces(request.Template))
            return Result<PromptTemplate>.Failure("Template must contain at least one variable using angle brackets <variable>", "ValidationError");

        try
        {
            // Check if prompt exists
            var existingPrompt = await _promptRepository.GetPromptByIdAsync(request.PromptId);

            if (existingPrompt == null)
                return Result<PromptTemplate>.Failure("Prompt not found", "NotFound");

            // Parse category
            if (!Enum.TryParse(typeof(PromptCategory), request.Category, out var categoryObj))
                categoryObj = PromptCategory.General;

            var category = (PromptCategory)categoryObj;

            // Extract variables from new template
            var newVariables = AngleBraceTextHandler.ExtractVariables(request.Template)
                .ToDictionary(v => v, v => string.Empty);

            // Update using repository method
            var updatedPrompt = await _promptRepository.UpdatePromptAsync(
                request.PromptId,
                request.Title,
                request.Template,
                request.Description,
                newVariables,
                category);

            if (updatedPrompt == null)
                return Result<PromptTemplate>.Failure("Failed to update prompt", "UpdateFailed");

            return Result<PromptTemplate>.Success(updatedPrompt);
        }
        catch (Exception ex)
        {
            return Result<PromptTemplate>.Failure(
                $"Failed to update prompt: {ex.Message}",
                "DatabaseError");
        }
    }
}
