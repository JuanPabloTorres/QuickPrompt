using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.FinalPrompts.UseCases;

/// <summary>
/// Use Case for retrieving all final (completed) prompts.
/// </summary>
public class GetAllFinalPromptsUseCase
{
    private readonly IFinalPromptRepository _finalPromptRepository;

    public GetAllFinalPromptsUseCase(IFinalPromptRepository finalPromptRepository)
    {
        _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
    }

    public async Task<Result<List<FinalPrompt>>> ExecuteAsync()
    {
        try
        {
            var prompts = await _finalPromptRepository.GetAllAsync();

            return Result<List<FinalPrompt>>.Success(prompts);
        }
        catch (Exception ex)
        {
            return Result<List<FinalPrompt>>.Failure($"Failed to load final prompts: {ex.Message}");
        }
    }
}
