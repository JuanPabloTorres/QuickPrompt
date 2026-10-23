using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Use Case for getting the total count of prompts with optional filtering.
/// </summary>
public class GetTotalPromptsCountUseCase
{
    private readonly IPromptRepository _promptRepository;

    public GetTotalPromptsCountUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    public async Task<Result<int>> ExecuteAsync(
        string? searchTerm = null,
        PromptFilter filter = PromptFilter.All,
        string? category = null)
    {
        try
        {
            var count = await _promptRepository.GetTotalCountAsync(
                searchTerm,
                filter,
                category);

            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Failed to get prompts count: {ex.Message}");
        }
    }
}
