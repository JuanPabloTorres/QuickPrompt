using System;
using System.Threading.Tasks;

namespace QuickPrompt.Settings
{
    public interface ISettingsService
    {
        Task<SettingsModel> GetSettingsAsync();
        Task SaveSettingsAsync(SettingsModel settings);
    }
}