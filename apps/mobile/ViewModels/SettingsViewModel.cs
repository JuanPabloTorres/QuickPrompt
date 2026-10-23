using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Settings;
using QuickPrompt.History.Sync;
using QuickPrompt.Services.ServiceInterfaces;
using System;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly SyncService _syncService;
        private readonly ISessionService _sessionService;
        private SettingsModel _settings;

        [ObservableProperty]
        private bool autoSendEnabled;
        [ObservableProperty]
        private bool openExternalEnabled;
        [ObservableProperty]
        private bool cloudSyncEnabled;
        [ObservableProperty]
        private DateTime? lastSyncAt;

        public SettingsViewModel(ISettingsService settingsService, SyncService syncService, ISessionService sessionService)
        {
            _settingsService = settingsService;
            _syncService = syncService;
            _sessionService = sessionService;
            _settings = new SettingsModel();
            LoadSettingsAsync();
        }

        private async void LoadSettingsAsync()
        {
            _settings = await _settingsService.GetSettingsAsync();
            AutoSendEnabled = _settings.AutoSendEnabled;
            OpenExternalEnabled = _settings.OpenExternalEnabled;
            CloudSyncEnabled = _settings.CloudSyncEnabled;
            LastSyncAt = _settings.LastSyncAt;
        }

        partial void OnAutoSendEnabledChanged(bool value) => Save();
        partial void OnOpenExternalEnabledChanged(bool value) => Save();
        partial void OnCloudSyncEnabledChanged(bool value) => Save();

        private async void Save()
        {
            _settings.AutoSendEnabled = AutoSendEnabled;
            _settings.OpenExternalEnabled = OpenExternalEnabled;
            _settings.CloudSyncEnabled = CloudSyncEnabled;
            _settings.LastSyncAt = LastSyncAt;
            await _settingsService.SaveSettingsAsync(_settings);
        }

        [RelayCommand]
        public async Task SyncNowAsync()
        {
            if (!_sessionService.IsLoggedIn || !CloudSyncEnabled)
                return;
            await _syncService.SyncNowAsync();
            LastSyncAt = DateTime.UtcNow;
            Save();
        }

        [RelayCommand]
        public async Task LogoutAsync()
        {
            var option = await _sessionService.ShowLogoutConfirmationAsync();
            if (option == LogoutOption.DeleteLocal)
                await _sessionService.DeleteLocalDataAsync();
            await _sessionService.LogoutAsync();
        }
    }
}