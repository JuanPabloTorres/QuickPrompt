namespace QuickPrompt.Domain.Enums;

/// <summary>
/// Query filters for retrieving prompts.
/// Note: This is a query concern, not a domain rule, but is placed here 
/// because it's used in repository interfaces (domain contracts).
/// </summary>
public enum PromptFilter
{
    All,
    Favorites,
    NonFavorites,
    Today,
    Last7Days,
    None
}
