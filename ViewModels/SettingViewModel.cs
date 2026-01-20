using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;

namespace QuickPrompt.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// Refactored to use Use Cases and services - Phase 1.
/// ✅ UX IMPROVEMENTS: Added Dark Mode theme selection.
/// </summary>
public partial class SettingViewModel : BaseViewModel
{
    // Services (injected)
    private readonly DatabaseServiceManager _databaseServiceManager;
    private readonly IDialogService _dialogService;
    private readonly IThemeService _themeService;

    [ObservableProperty] private string appVersion;
    
    // ✅ UX IMPROVEMENTS: Theme selection properties
    [ObservableProperty] private int selectedThemeIndex;
    
    public List<string> ThemeOptions { get; } = new List<string>
    {
        "🌞 Light Mode",
        "🌙 Dark Mode",
        "🔄 System Default"
    };

    // Constructor with dependency injection
    public SettingViewModel(
        AppSettings appSettings,
        DatabaseServiceManager dbManager,
        IDialogService dialogService,
        IThemeService themeService)
    {
        _databaseServiceManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        appVersion = appSettings?.Version ?? "Unknown Version";
        
        // ✅ Initialize theme selection based on current theme
        InitializeThemeSelection();
    }

    private void InitializeThemeSelection()
    {
        var currentTheme = _themeService.GetCurrentTheme();
        selectedThemeIndex = currentTheme switch
        {
            AppTheme.Light => 0,
            AppTheme.Dark => 1,
            AppTheme.Unspecified => 2,
            _ => 2
        };
    }

    // ✅ UX IMPROVEMENTS: Handle theme change
    partial void OnSelectedThemeIndexChanged(int value)
    {
        var newTheme = value switch
        {
            0 => AppTheme.Light,
            1 => AppTheme.Dark,
            2 => AppTheme.Unspecified,
            _ => AppTheme.Unspecified
        };
        
        _themeService.SetTheme(newTheme);
    }

    // ============================ DATABASE ============================

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        bool confirm = await _dialogService.ShowConfirmationAsync(
            AppMessagesEng.ConfirmationTitle,
            AppMessagesEng.RestoreConfirmationMessage,
            AppMessagesEng.Yes,
            AppMessagesEng.No);

        if (!confirm)
            return;

        IsLoading = true;
        try
        {
            await _databaseServiceManager.RestoreAsync();

            await _dialogService.ShowLottieMessageAsync(
                "CompleteAnimation.json",
                AppMessagesEng.DatabaseRestore);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"{AppMessagesEng.GenericError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ============================ NAVIGATION ============================

    [RelayCommand]
    private async Task OpenGuideLinkAsync()
    {
        IsLoading = true;
        try
        {
            var uri = new Uri("https://estjuanpablotorres.wixsite.com/quickprompt");
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"Error opening guide: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}