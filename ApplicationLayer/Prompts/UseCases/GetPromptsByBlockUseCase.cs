using QuickPrompt.ApplicationLayer.Common;
using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Domain.Interfaces;

namespace QuickPrompt.ApplicationLayer.Prompts.UseCases;

/// <summary>
/// Use Case for retrieving prompts with pagination (block-based loading).
/// </summary>
public class GetPromptsByBlockUseCase
{
    private readonly IPromptRepository _promptRepository;

    public GetPromptsByBlockUseCase(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository ?? throw new ArgumentNullException(nameof(promptRepository));
    }

    public async Task<Result<List<PromptTemplate>>> ExecuteAsync(
        int skip,
        int take,
        PromptFilter filter = PromptFilter.All,
        string? searchTerm = null,
        string? category = null)
    {
        try
        {
            var prompts = await _promptRepository.GetByBlockAsync(
                skip,
                take,
                filter,
                searchTerm,
                category);

            return Result<List<PromptTemplate>>.Success(prompts);
        }
        catch (Exception ex)
        {
            return Result<List<PromptTemplate>>.Failure($"Failed to load prompts: {ex.Message}");
        }
    }
}
