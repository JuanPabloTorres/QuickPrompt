using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.FinalPrompts.UseCases;

/// <summary>
/// Use Case for deleting a final (completed) prompt.
/// </summary>
public class DeleteFinalPromptUseCase
{
    private readonly IFinalPromptRepository _finalPromptRepository;

    public DeleteFinalPromptUseCase(IFinalPromptRepository finalPromptRepository)
    {
        _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
    }

    public async Task<Result<bool>> ExecuteAsync(Guid finalPromptId)
    {
        try
        {
            if (finalPromptId == Guid.Empty)
            {
                return Result<bool>.Failure("Invalid final prompt ID.");
            }

            await _finalPromptRepository.DeleteAsync(finalPromptId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Failed to delete final prompt: {ex.Message}");
        }
    }
}
