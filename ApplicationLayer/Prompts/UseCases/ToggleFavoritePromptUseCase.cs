using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Use Case for toggling the favorite status of a prompt.
/// </summary>
public class ToggleFavoritePromptUseCase
{
    private readonly IPromptRepository _promptRepository;

    public ToggleFavoritePromptUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    public async Task<Result<bool>> ExecuteAsync(Guid promptId, bool isFavorite)
    {
        try
        {
            if (promptId == Guid.Empty)
            {
                return Result<bool>.Failure("Invalid prompt ID.");
            }

            await _promptRepository.UpdateFavoriteStatusAsync(promptId, isFavorite);

            return Result<bool>.Success(isFavorite);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Failed to update favorite status: {ex.Message}");
        }
    }
}
