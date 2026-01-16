using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Models;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Use Case for retrieving a prompt by ID.
/// </summary>
public class GetPromptByIdUseCase
{
    private readonly IPromptRepository _promptRepository;

    public GetPromptByIdUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    /// <summary>
    /// Gets a prompt template by its ID.
    /// </summary>
    /// <param name="promptId">The Guid ID of the prompt to retrieve.</param>
    /// <returns>Result containing the prompt or error message.</returns>
    public async Task<Result<PromptTemplate>> ExecuteAsync(Guid promptId)
    {
        if (promptId == Guid.Empty)
            return Result<PromptTemplate>.Failure("Invalid prompt ID", "InvalidRequest");

        try
        {
            var prompt = await _promptRepository.GetPromptByIdAsync(promptId);

            if (prompt == null)
                return Result<PromptTemplate>.Failure("Prompt not found", "NotFound");

            return Result<PromptTemplate>.Success(prompt);
        }
        catch (Exception ex)
        {
            return Result<PromptTemplate>.Failure(
                $"Failed to retrieve prompt: {ex.Message}",
                "DatabaseError");
        }
    }
}
