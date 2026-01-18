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
/// </summary>
public partial class SettingViewModel : BaseViewModel
{
    // 🆕 Services (injected)
    private readonly DatabaseServiceManager _databaseServiceManager;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private string appVersion;

    // Constructor with dependency injection
    public SettingViewModel(
        AppSettings appSettings,
        DatabaseServiceManager dbManager,
        IDialogService dialogService)
    {
        _databaseServiceManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        appVersion = appSettings?.Version ?? "Unknown Version";
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