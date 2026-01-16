using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Use Case for deleting a prompt template.
/// </summary>
public class DeletePromptUseCase
{
    private readonly IPromptRepository _promptRepository;

    public DeletePromptUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    /// <summary>
    /// Deletes a prompt template by ID.
    /// </summary>
    /// <param name="promptId">The Guid ID of the prompt to delete.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result> ExecuteAsync(Guid promptId)
    {
        if (promptId == Guid.Empty)
            return Result.Failure("Invalid prompt ID", "InvalidRequest");

        try
        {
            // Check if prompt exists
            var prompt = await _promptRepository.GetPromptByIdAsync(promptId);

            if (prompt == null)
                return Result.Failure("Prompt not found", "NotFound");

            // Delete the prompt
            var deleted = await _promptRepository.DeletePromptAsync(promptId);

            if (!deleted)
                return Result.Failure("Failed to delete prompt", "DeleteFailed");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                $"Failed to delete prompt: {ex.Message}",
                "DatabaseError");
        }
    }
}
