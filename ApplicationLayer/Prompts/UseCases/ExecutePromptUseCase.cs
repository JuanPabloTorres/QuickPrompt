using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Request model for executing a prompt with variables.
/// </summary>
public class ExecutePromptRequest
{
    public Guid PromptId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Response model for executed prompt.
/// </summary>
public class ExecutePromptResponse
{
    public string CompletedText { get; set; } = string.Empty;
    public FinalPrompt FinalPrompt { get; set; } = null!;
}

/// <summary>
/// Use Case for executing a prompt by filling in variables.
/// Extracts business logic from QuickPromptViewModel.
/// </summary>
public class ExecutePromptUseCase
{
    private readonly IPromptRepository _promptRepository;
    private readonly IFinalPromptRepository _finalPromptRepository;
    private readonly IPromptCacheService _cacheService;

    public ExecutePromptUseCase(
        IPromptRepository promptRepository,
        IFinalPromptRepository finalPromptRepository,
        IPromptCacheService cacheService)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
        _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    /// <summary>
    /// Executes a prompt by filling in the variables and saving the result.
    /// </summary>
    /// <param name="request">The execution request with prompt ID and variables.</param>
    /// <returns>Result containing the executed prompt or error message.</returns>
    public async Task<Result<ExecutePromptResponse>> ExecuteAsync(ExecutePromptRequest request)
    {
        if (request == null)
            return Result<ExecutePromptResponse>.Failure("Request cannot be null", "InvalidRequest");

        if (request.PromptId == Guid.Empty)
            return Result<ExecutePromptResponse>.Failure("Invalid prompt ID", "InvalidRequest");

        try
        {
            // Get the prompt template by Guid
            var prompt = await _promptRepository.GetPromptByIdAsync(request.PromptId);

            if (prompt == null)
                return Result<ExecutePromptResponse>.Failure(
                    "Prompt not found",
                    "NotFound");

            // Fill template with variables
            string completedText = prompt.Template;

            foreach (var variable in request.Variables)
            {
                // Replace <variableName> with value
                completedText = completedText.Replace(
                    $"<{variable.Key}>",
                    variable.Value);

                // Cache the variable value for future suggestions
                await _cacheService.AddAsync(variable.Key, variable.Value);
            }

            // Create FinalPrompt record
            var finalPrompt = new FinalPrompt
            {
                SourcePromptId = prompt.Id,
                CompletedText = completedText,
                IsFavorite = false,
                CreatedAt = DateTime.Now
            };

            // Save the final prompt
            await _finalPromptRepository.SaveAsync(finalPrompt);

            var response = new ExecutePromptResponse
            {
                CompletedText = completedText,
                FinalPrompt = finalPrompt
            };

            return Result<ExecutePromptResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<ExecutePromptResponse>.Failure(
                $"Failed to execute prompt: {ex.Message}",
                "ExecutionError");
        }
    }
}
