using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickPrompt.Settings
{
    public class SettingsService : ISettingsService
    {
        private const string FileName = "settings.json";
        private readonly string _filePath;

        public SettingsService()
        {
            _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FileName);
        }

        public async Task<SettingsModel> GetSettingsAsync()
        {
            if (!File.Exists(_filePath))
                return new SettingsModel();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }

        public async Task SaveSettingsAsync(SettingsModel settings)
        {
            var json = JsonSerializer.Serialize(settings);
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}