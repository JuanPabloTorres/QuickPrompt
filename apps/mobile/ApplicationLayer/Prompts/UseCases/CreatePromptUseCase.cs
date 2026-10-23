using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;
using QuickPrompt.Tools;

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
            return Result<PromptTemplate>.Failure("Request cannot be null");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<PromptTemplate>.Failure("Title is required");

        if (string.IsNullOrWhiteSpace(request.Template))
            return Result<PromptTemplate>.Failure("Template is required");

        // Check if template contains angle brackets (variables)
        if (!AngleBraceTextHandler.ContainsAngleBraces(request.Template))
            return Result<PromptTemplate>.Failure("Template must contain at least one variable using angle brackets <variable>");

        // Parse category
        if (!Enum.TryParse(typeof(PromptCategory), request.Category, out var categoryObj))
            categoryObj = PromptCategory.General;

        var category = (PromptCategory)categoryObj;

        try
        {
            // Extract variables from template
            var variables = AngleBraceTextHandler.ExtractVariables(request.Template)
                .ToDictionary(v => v, v => string.Empty);

            // Create domain entity
            var newPrompt = PromptTemplate.Create(
                request.Title,
                request.Template,
                request.Description,
                variables,
                category);

            // Save to repository
            var promptId = await _promptRepository.AddAsync(newPrompt);
            newPrompt.Id = promptId;

            return Result<PromptTemplate>.Success(newPrompt);
        }
        catch (Exception ex)
        {
            return Result<PromptTemplate>.Failure($"Failed to create prompt: {ex.Message}");
        }
    }
}
