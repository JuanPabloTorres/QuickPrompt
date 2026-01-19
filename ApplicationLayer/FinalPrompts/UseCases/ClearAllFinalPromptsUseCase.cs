using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.FinalPrompts.UseCases;

/// <summary>
/// Use Case for clearing all final (completed) prompts.
/// </summary>
public class ClearAllFinalPromptsUseCase
{
    private readonly IFinalPromptRepository _finalPromptRepository;

    public ClearAllFinalPromptsUseCase(IFinalPromptRepository finalPromptRepository)
    {
        _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
    }

    public async Task<Result<int>> ExecuteAsync()
    {
        try
        {
            var prompts = await _finalPromptRepository.GetAllAsync();

            if (prompts == null || !prompts.Any())
            {
                return Result<int>.Success(0);
            }

            int deletedCount = 0;

            foreach (var prompt in prompts)
            {
                await _finalPromptRepository.DeleteAsync(prompt.Id);
                deletedCount++;
            }

            return Result<int>.Success(deletedCount);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Failed to clear final prompts: {ex.Message}");
        }
    }
}
