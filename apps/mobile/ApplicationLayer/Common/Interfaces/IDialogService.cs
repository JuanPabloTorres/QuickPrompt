namespace QuickPrompt.ApplicationLayer.Common.Interfaces;

/// <summary>
/// Service for displaying dialogs, alerts, and animations to the user.
/// Consolidates AlertService and GenericToolBox functionality.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a simple alert dialog with a message.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog message.</param>
    /// <param name="buttonText">OK button text (default: "OK").</param>
    Task ShowAlertAsync(string title, string message, string buttonText = "OK");

    /// <summary>
    /// Shows an error alert with standard error styling.
    /// </summary>
    /// <param name="message">Error message to display.</param>
    Task ShowErrorAsync(string message);

    /// <summary>
    /// Shows a success alert with standard success styling.
    /// </summary>
    /// <param name="message">Success message to display.</param>
    Task ShowSuccessAsync(string message);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog message.</param>
    /// <param name="acceptText">Accept button text (default: "Yes").</param>
    /// <param name="cancelText">Cancel button text (default: "No").</param>
    /// <returns>True if user clicked accept, false if canceled.</returns>
    Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string acceptText = "Yes",
        string cancelText = "No");

    /// <summary>
    /// Shows a Lottie animation popup with a message.
    /// </summary>
    /// <param name="animationFileName">Name of the Lottie animation file (e.g., "CompleteAnimation.json").</param>
    /// <param name="message">Message to display with the animation.</param>
    /// <param name="durationMs">Duration to display the animation in milliseconds (default: 2000).</param>
    Task ShowLottieMessageAsync(
        string animationFileName,
        string message,
        int durationMs = 2000);

    /// <summary>
    /// Shows an action sheet with multiple options.
    /// </summary>
    /// <param name="title">Sheet title.</param>
    /// <param name="cancelButton">Cancel button text.</param>
    /// <param name="destructiveButton">Destructive action button text (optional).</param>
    /// <param name="buttons">Array of action button texts.</param>
    /// <returns>The text of the selected button, or null if canceled.</returns>
    Task<string?> ShowActionSheetAsync(
        string title,
        string? cancelButton,
        string? destructiveButton,
        params string[] buttons);

    /// <summary>
    /// Shows a prompt dialog asking for user input.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog message.</param>
    /// <param name="placeholder">Input placeholder text.</param>
    /// <param name="initialValue">Initial input value.</param>
    /// <param name="maxLength">Maximum input length.</param>
    /// <param name="keyboard">Keyboard type.</param>
    /// <returns>The user's input, or null if canceled.</returns>
    Task<string?> ShowPromptAsync(
        string title,
        string message,
        string? placeholder = null,
        string? initialValue = null,
        int maxLength = -1,
        Keyboard? keyboard = null);
}
