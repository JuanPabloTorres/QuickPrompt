using QuickPrompt.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public static class PromptCacheCleanupService
    {
        private const string LastCleanupKey = "PromptCacheLastCleanup";

        private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(7);

        public static void RunCleanupIfDue()
        {
            var lastCleanupStr = Preferences.Get(LastCleanupKey, null);

            var lastCleanup = string.IsNullOrEmpty(lastCleanupStr) ? DateTime.MinValue : DateTime.Parse(lastCleanupStr);

            if ((DateTime.UtcNow - lastCleanup) > CleanupInterval)
            {
                PromptVariableCache.ClearCache();

                Preferences.Set(LastCleanupKey, DateTime.UtcNow.ToString("O")); // ISO 8601
            }
        }

        private static int GetUserCleanupDays()
        {
            return Preferences.Get("PromptCacheCleanupDays", 7); // valor por defecto: 7 días
        }

    }

}
