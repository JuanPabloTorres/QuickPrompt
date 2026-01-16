using CommunityToolkit.Maui.Views;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.PopUps;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace QuickPrompt.Infrastructure.Services.UI;

/// <summary>
/// Implementation of IDialogService for displaying dialogs and animations.
/// Consolidates functionality from AlertService and GenericToolBox.
/// </summary>
public class DialogService : IDialogService
{
    public async Task ShowAlertAsync(string title, string message, string buttonText = "OK")
    {
        if (MauiApp.Current?.MainPage == null)
            return;

        await MauiApp.Current.MainPage.DisplayAlert(title, message, buttonText);
    }

    public async Task ShowErrorAsync(string message)
    {
        await ShowAlertAsync("Error", message, "OK");
    }

    public async Task ShowSuccessAsync(string message)
    {
        await ShowAlertAsync("Success", message, "OK");
    }

    public async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string acceptText = "Yes",
        string cancelText = "No")
    {
        if (MauiApp.Current?.MainPage == null)
            return false;

        return await MauiApp.Current.MainPage.DisplayAlert(title, message, acceptText, cancelText);
    }

    public async Task ShowLottieMessageAsync(
        string animationFileName,
        string message,
        int durationMs = 2000)
    {
        if (MauiApp.Current?.MainPage == null)
            return;

        try
        {
            // Create popup with auto-close duration
            var popup = new LottieMessagePopup(animationFileName, message, durationMs);

            // Show popup - it will auto-close after the specified duration
            // Don't await ShowPopupAsync as it will block until the popup is closed
            _ = MauiApp.Current.MainPage.ShowPopupAsync(popup);

            // Note: The popup handles its own closing via CloseAfterDelay internal method
            // We don't need to manually close it here
        }
        catch (Exception ex)
        {
            // Fallback to standard alert if Lottie popup fails
            System.Diagnostics.Debug.WriteLine($"Error showing Lottie popup: {ex.Message}");
            await ShowAlertAsync("Info", message);
        }
    }

    public async Task<string?> ShowActionSheetAsync(
        string title,
        string? cancelButton,
        string? destructiveButton,
        params string[] buttons)
    {
        if (MauiApp.Current?.MainPage == null)
            return null;

        return await MauiApp.Current.MainPage.DisplayActionSheet(
            title,
            cancelButton,
            destructiveButton,
            buttons);
    }

    public async Task<string?> ShowPromptAsync(
        string title,
        string message,
        string? placeholder = null,
        string? initialValue = null,
        int maxLength = -1,
        Keyboard? keyboard = null)
    {
        if (MauiApp.Current?.MainPage == null)
            return null;

        return await MauiApp.Current.MainPage.DisplayPromptAsync(
            title,
            message,
            placeholder: placeholder,
            initialValue: initialValue,
            maxLength: maxLength,
            keyboard: keyboard ?? Keyboard.Default);
    }
}
