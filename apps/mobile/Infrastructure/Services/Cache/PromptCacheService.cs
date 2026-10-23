using QuickPrompt.ApplicationLayer.Common.Interfaces;
using System.Text.Json;

namespace QuickPrompt.Infrastructure.Services.Cache;

/// <summary>
/// Thread-safe, persistent implementation of prompt variable caching.
/// Stores variable suggestions in Preferences for persistence across app restarts.
/// Replaces the static PromptVariableCache.
/// </summary>
public class PromptCacheService : IPromptCacheService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, List<string>> _memoryCache = new();
    private const string CachePrefix = "PromptCache_";
    private const int MaxCachedValuesPerKey = 10;

    public PromptCacheService()
    {
        // Load cache from preferences on initialization
        _ = LoadCacheAsync();
    }

    public async Task AddAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;

        await _lock.WaitAsync();
        try
        {
            if (!_memoryCache.ContainsKey(key))
                _memoryCache[key] = new List<string>();

            // Avoid duplicates
            if (_memoryCache[key].Contains(value))
                return;

            _memoryCache[key].Insert(0, value); // Most recent first

            // Keep only the most recent N values
            if (_memoryCache[key].Count > MaxCachedValuesPerKey)
                _memoryCache[key].RemoveAt(_memoryCache[key].Count - 1);

            await PersistKeyAsync(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddRangeAsync(string key, IEnumerable<string> values)
    {
        if (string.IsNullOrWhiteSpace(key) || values == null)
            return;

        await _lock.WaitAsync();
        try
        {
            if (!_memoryCache.ContainsKey(key))
                _memoryCache[key] = new List<string>();

            foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                if (!_memoryCache[key].Contains(value))
                {
                    _memoryCache[key].Insert(0, value);
                }
            }

            // Keep only the most recent N values
            if (_memoryCache[key].Count > MaxCachedValuesPerKey)
                _memoryCache[key] = _memoryCache[key].Take(MaxCachedValuesPerKey).ToList();

            await PersistKeyAsync(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new List<string>();

        await _lock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(key, out var values))
                return new List<string>(values);

            return new List<string>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> GetRecentAsync(string key, int count = 5)
    {
        var allValues = await GetAsync(key);
        return allValues.Take(count).ToList();
    }

    public async Task<bool> ContainsAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return false;

        await _lock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(key, out var values))
                return values.Contains(value);

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;

        await _lock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(key, out var values))
            {
                values.Remove(value);
                await PersistKeyAsync(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await _lock.WaitAsync();
        try
        {
            _memoryCache.Remove(key);
            Preferences.Remove($"{CachePrefix}{key}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Remove all keys from preferences
            foreach (var key in _memoryCache.Keys.ToList())
            {
                Preferences.Remove($"{CachePrefix}{key}");
            }

            _memoryCache.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> GetAllKeysAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _memoryCache.Keys.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    #region Private Helpers

    private async Task LoadCacheAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Load cache index (list of keys)
            var cacheIndexJson = Preferences.Get("PromptCacheIndex", string.Empty);
            if (string.IsNullOrWhiteSpace(cacheIndexJson))
                return;

            var keys = JsonSerializer.Deserialize<List<string>>(cacheIndexJson);
            if (keys == null)
                return;

            // Load each key's values
            foreach (var key in keys)
            {
                var valuesJson = Preferences.Get($"{CachePrefix}{key}", string.Empty);
                if (!string.IsNullOrWhiteSpace(valuesJson))
                {
                    var values = JsonSerializer.Deserialize<List<string>>(valuesJson);
                    if (values != null)
                    {
                        _memoryCache[key] = values;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - cache is non-critical
            System.Diagnostics.Debug.WriteLine($"Error loading prompt cache: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task PersistKeyAsync(string key)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var values))
            {
                var json = JsonSerializer.Serialize(values);
                Preferences.Set($"{CachePrefix}{key}", json);
            }

            // Update cache index
            var allKeys = _memoryCache.Keys.ToList();
            var indexJson = JsonSerializer.Serialize(allKeys);
            Preferences.Set("PromptCacheIndex", indexJson);
        }
        catch (Exception ex)
        {
            // Log error but don't crash - cache is non-critical
            System.Diagnostics.Debug.WriteLine($"Error persisting prompt cache key '{key}': {ex.Message}");
        }

        return Task.CompletedTask;
    }

    #endregion
}
