namespace QuickPrompt.ApplicationLayer.Common.Interfaces;

/// <summary>
/// Service for caching prompt variable suggestions.
/// Replaces the static PromptVariableCache with a testable, injectable service.
/// </summary>
public interface IPromptCacheService
{
    /// <summary>
    /// Adds a variable value to the cache for a specific key.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <param name="value">The value to cache.</param>
    Task AddAsync(string key, string value);

    /// <summary>
    /// Adds multiple variable values to the cache.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <param name="values">The values to cache.</param>
    Task AddRangeAsync(string key, IEnumerable<string> values);

    /// <summary>
    /// Gets all cached values for a specific key.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <returns>List of cached values, or empty list if none exist.</returns>
    Task<List<string>> GetAsync(string key);

    /// <summary>
    /// Gets the most recent N values for a specific key.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <param name="count">Number of recent values to return.</param>
    /// <returns>List of recent cached values.</returns>
    Task<List<string>> GetRecentAsync(string key, int count = 5);

    /// <summary>
    /// Checks if a value exists in the cache for a specific key.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value exists in cache.</returns>
    Task<bool> ContainsAsync(string key, string value);

    /// <summary>
    /// Removes a specific value from the cache.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    /// <param name="value">The value to remove.</param>
    Task RemoveAsync(string key, string value);

    /// <summary>
    /// Clears all cached values for a specific key.
    /// </summary>
    /// <param name="key">The variable name or identifier.</param>
    Task ClearAsync(string key);

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Gets all variable keys that have cached values.
    /// </summary>
    /// <returns>List of variable keys.</returns>
    Task<List<string>> GetAllKeysAsync();
}
