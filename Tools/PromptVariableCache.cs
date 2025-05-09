using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class PromptVariableCache
    {
        private const string CacheKey = "PromptVariableCache";

        private const int MaxValuesPerVariable = 10;

        private const int MaxValueLength = 100;

        public static Dictionary<string, List<string>> LoadCache()
        {
            var json = Preferences.Get(CacheKey, "{}");

            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new();
        }

        public static void SaveValue(string variable, string value)
        {
            if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(value))
                return;

            if (value.Length > MaxValueLength)
                return;

            var cache = LoadCache();

            if (!cache.ContainsKey(variable))
                cache[variable] = new List<string>();

            if (cache[variable].Contains(value))
                return;

            cache[variable].Add(value);

            if (cache[variable].Count > MaxValuesPerVariable)
                cache[variable] = cache[variable]
                    .Skip(cache[variable].Count - MaxValuesPerVariable)
                    .ToList();

            Preferences.Set(CacheKey, JsonSerializer.Serialize(cache));
        }

        public static List<string> GetSuggestions(string variable)
        {
            var cache = LoadCache();

            return cache.TryGetValue(variable, out var list) ? list : new();
        }

        public static void ClearCache()
        {
            Preferences.Remove(CacheKey);
        }
    }

}
