using QuickPrompt.Domain.Enums;
using QuickPrompt.Tools;

namespace QuickPrompt.Shared.Mappers;

/// <summary>
/// Extension methods to convert between legacy Filters enum and Domain PromptFilter enum.
/// </summary>
public static class FilterExtensions
{
    /// <summary>
    /// Convert legacy Filters to Domain PromptFilter.
    /// </summary>
    public static PromptFilter ToDomainFilter(this Filters filter)
    {
        return filter switch
        {
            Filters.All => PromptFilter.All,
            Filters.Favorites => PromptFilter.Favorites,
            Filters.NonFavorites => PromptFilter.NonFavorites,
            Filters.Today => PromptFilter.Today,
            Filters.Last7Days => PromptFilter.Last7Days,
            Filters.None => PromptFilter.None,
            _ => PromptFilter.All
        };
    }

    /// <summary>
    /// Convert Domain PromptFilter to legacy Filters.
    /// </summary>
    public static Filters ToLegacyFilter(this PromptFilter filter)
    {
        return filter switch
        {
            PromptFilter.All => Filters.All,
            PromptFilter.Favorites => Filters.Favorites,
            PromptFilter.NonFavorites => Filters.NonFavorites,
            PromptFilter.Today => Filters.Today,
            PromptFilter.Last7Days => Filters.Last7Days,
            PromptFilter.None => Filters.None,
            _ => Filters.All
        };
    }
}
