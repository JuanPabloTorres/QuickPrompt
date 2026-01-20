using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Text.Json;

namespace QuickPrompt.ViewModels;

/// <summary>
/// ViewModel for the main prompt creation page.
/// Refactored to use Use Cases and services - Phase 1.
/// ✅ FIXED: Added missing properties and proper inheritance initialization
/// </summary>
public partial class MainPageViewModel : BaseViewModel
{
    // 🆕 Use Cases and Services (injected)
    private readonly CreatePromptUseCase _createPromptUseCase;
    private readonly IDialogService _dialogService;
    private readonly AdmobService _adMobService;

    // Properties
    [ObservableProperty] private int cursorPosition;
    [ObservableProperty] private int selectionLength;
    [ObservableProperty] private string promptText = string.Empty;
    [ObservableProperty] private string promptTitle = string.Empty;
    [ObservableProperty] private string promptDescription = string.Empty;
    [ObservableProperty] private string selectedCategory = string.Empty;

    // Constructor with dependency injection - ✅ FIXED: Pass admobService to base
    public MainPageViewModel(
        CreatePromptUseCase createPromptUseCase,
        IDialogService dialogService,
        AdmobService admobService) : base(admobService) // ✅ FIX: Pass to base
    {
        _createPromptUseCase = createPromptUseCase ?? throw new ArgumentNullException(nameof(createPromptUseCase));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _adMobService = admobService ?? throw new ArgumentNullException(nameof(admobService));
    }

    // ============================ COMMANDS ============================

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        IsLoading = true;

        try
        {
            // ✅ Validate required fields
            if (string.IsNullOrWhiteSpace(PromptTitle))
            {
                await _dialogService.ShowErrorAsync("Title is required");
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptText))
            {
                await _dialogService.ShowErrorAsync("Prompt template is required");
                return;
            }

            // Create request
            var request = new CreatePromptRequest
            {
                Title = PromptTitle,
                Description = PromptDescription,
                Template = PromptText,
                Category = string.IsNullOrWhiteSpace(SelectedCategory) ? "General" : SelectedCategory
            };

            // Execute use case
            var result = await _createPromptUseCase.ExecuteAsync(request);

            if (result.IsFailure)
            {
                await _dialogService.ShowErrorAsync(result.Error);
                return;
            }

            // Show ad
            await _adMobService.ShowInterstitialAdAndWaitAsync();

            // Show success
            await _dialogService.ShowLottieMessageAsync(
                "CompleteAnimation.json",
                AppMessagesEng.Prompts.PromptSavedSuccess);

            // Clear form
            ClearPromptInputs();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(
                $"{AppMessagesEng.Prompts.PromptSaveError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearText() => ClearPromptInputs();

    [RelayCommand]
    private async Task CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(PromptText))
        {
            await Clipboard.Default.SetTextAsync(PromptText);
            await _dialogService.ShowAlertAsync(
                "Notification",
                AppMessagesEng.Prompts.PromptCopiedToClipboard);
        }
    }

    [RelayCommand]
    public async Task CreateVariable()
    {
        if (IsSelectionValid(PromptText, SelectionLength))
        {
            EncloseSelectedTextWithBraces();
        }
        else
        {
            await _dialogService.ShowErrorAsync(AppMessagesEng.Warnings.SelectWordError);
        }
    }

    [RelayCommand]
    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(CursorPosition, SelectionLength);
    }

    [RelayCommand]
    private async Task GoToAdvancedBuilder()
    {
        await NavigateToAsync(nameof(PromptBuilderPage));
    }

    // ============================ VARIABLE HANDLING ============================

    private async void EncloseSelectedTextWithBraces()
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await _dialogService.ShowErrorAsync(AppMessagesEng.Warnings.InvalidTextSelection);
            return;
        }

        var handler = new AngleBraceTextHandler(PromptText);

        if (handler.IsSurroundedByAngleBraces(CursorPosition, SelectionLength))
        {
            await HandleSelectedTextAsync(CursorPosition, SelectionLength);
            return;
        }

        string selectedText = PromptText.Substring(CursorPosition, SelectionLength);

        if (AngleBraceTextHandler.ContainsVariable($"<{selectedText}>", PromptText))
        {
            var nextVersion = AngleBraceTextHandler.GetNextVariableSuffixVersion(PromptText, selectedText);
            selectedText += nextVersion;
        }

        handler.UpdateText(CursorPosition, SelectionLength, $"<{selectedText}>");
        PromptText = handler.Text;
        UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await _dialogService.ShowAlertAsync(
                "Warning",
                AppMessagesEng.Prompts.PromptEmptyAndUnSelected);
            return;
        }

        var handler = new AngleBraceTextHandler(PromptText);

        if (!handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            await _dialogService.ShowErrorAsync(AppMessagesEng.Warnings.InvalidTextSelection);
            return;
        }

        if (handler.IsSurroundedByAngleBraces(cursorPosition, selectionLength))
        {
            var (startIndex, length) = handler.AdjustSelectionForAngleBraces(cursorPosition, selectionLength);
            string selectedText = handler.ExtractTextWithoutAngleBraces(startIndex, length);
            selectedText = AngleBraceTextHandler.RemoveVariableSuffix(selectedText);
            handler.UpdateText(startIndex, length, selectedText);
            PromptText = handler.Text;
            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
        }
        else
        {
            await _dialogService.ShowAlertAsync(
                "Warning",
                AppMessagesEng.Warnings.WordHasNoBraces);
        }
    }

    // ============================ PROMPT MANAGEMENT ============================

    private void ClearPromptInputs()
    {
        PromptTitle = string.Empty;
        PromptDescription = string.Empty;
        PromptText = string.Empty;
        IsVisualModeActive = false;
        SelectedCategory = string.Empty;
        UpdateSelectedTextLabelCount(0);
    }

    [RelayCommand]
    private async Task ImportPrompt()
    {
        IsLoading = true;

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import Prompt",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.text" } },
                    { DevicePlatform.Android, new[] { "text/plain", ".json" } },
                    { DevicePlatform.WinUI, new[] { ".json", ".txt" } }
                })
            });

            if (result is null)
                return;

            // Validate file extension
            if (!result.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !result.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                await _dialogService.ShowErrorAsync("Only .json or .txt files are supported.");
                return;
            }

            // Read file
            string json;
            try
            {
                json = await File.ReadAllTextAsync(result.FullPath);
            }
            catch (Exception readEx)
            {
                await _dialogService.ShowErrorAsync($"Failed to read file: {readEx.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json.Length < 20)
            {
                await _dialogService.ShowErrorAsync("The selected file is empty or invalid.");
                return;
            }

            // Deserialize
            ImportablePrompt? data;
            try
            {
                data = JsonSerializer.Deserialize<ImportablePrompt>(json);
            }
            catch (Exception deserializationEx)
            {
                await _dialogService.ShowErrorAsync($"The file format is incorrect: {deserializationEx.Message}");
                return;
            }

            // Validate content
            if (data is null ||
                string.IsNullOrWhiteSpace(data.Title) ||
                string.IsNullOrWhiteSpace(data.Template) ||
                !AngleBraceTextHandler.ContainsAngleBraces(data.Template))
            {
                await _dialogService.ShowErrorAsync("The file does not contain a valid prompt.");
                return;
            }

            // Assign values
            PromptTitle = data.Title.Trim();
            PromptDescription = string.IsNullOrWhiteSpace(data.Description) ? "N/A" : data.Description.Trim();
            PromptText = data.Template;
            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(PromptText));

            await _dialogService.ShowLottieMessageAsync(
                "CompleteAnimation.json",
                AppMessagesEng.Prompts.PromptImportedSuccess);
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

    // ============================ MODE SWITCHING ============================

    [RelayCommand]
    private void SwitchToEditor()
    {
        System.Diagnostics.Debug.WriteLine("[MainPageViewModel.SwitchToEditor] Switching to text editor mode");
        IsVisualModeActive = false;
    }

    [RelayCommand]
    private void SwitchToChips()
    {
        System.Diagnostics.Debug.WriteLine($"[MainPageViewModel.SwitchToChips] Attempting to switch to visual mode. PromptText: {PromptText?.Length ?? 0} chars");
        
        if (!string.IsNullOrWhiteSpace(PromptText))
        {
            IsVisualModeActive = true;
            System.Diagnostics.Debug.WriteLine("[MainPageViewModel.SwitchToChips] Visual mode activated");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[MainPageViewModel.SwitchToChips] Cannot activate visual mode - PromptText is empty");
        }
    }
}