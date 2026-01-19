using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Interfaces;

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
            return Result<PromptTemplate>.Failure("Invalid prompt ID");

        try
        {
            var prompt = await _promptRepository.GetByIdAsync(promptId);

            if (prompt == null)
                return Result<PromptTemplate>.Failure("Prompt not found");

            return Result<PromptTemplate>.Success(prompt);
        }
        catch (Exception ex)
        {
            return Result<PromptTemplate>.Failure($"Failed to retrieve prompt: {ex.Message}");
        }
    }
}
