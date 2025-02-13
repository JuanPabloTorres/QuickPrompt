using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace QuickPrompt.ViewModels;

public partial class MainPageViewModel(PromptDatabaseService _databaseService, Models.AppSettings appSettings) : BaseViewModel
{
    // ============================ PROPIEDADES Y VARIABLES ============================
    [ObservableProperty] private int cursorPosition;

    [ObservableProperty] private int selectionLength;

    [ObservableProperty] private string promptText;

    [ObservableProperty] private string promptTitle;

    [ObservableProperty] private string promptDescription;

  

    // ============================ COMANDOS ============================

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var validator = new PromptValidator();

            string validationError = validator.ValidateEn(PromptTitle, PromptText);

            if (!string.IsNullOrEmpty(validationError))
            {
                await AppShell.Current.DisplayAlert("Error", validationError, "OK");

                return;
            }

            var newPrompt = CreatePromptTemplate();

            await _databaseService.SavePromptAsync(newPrompt);

            ClearPromptInputs();
            await AppShell.Current.DisplayAlert("Saved", AppMessagesEng.Prompts.PromptSavedSuccess, "OK");

        }, AppMessagesEng.Prompts.PromptSaveError);
    }

    [RelayCommand]
    private void ClearText() => ClearPromptInputs();

    [RelayCommand]
    private async Task CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(PromptText))
        {
            await Clipboard.Default.SetTextAsync(PromptText);
            await AlertService.ShowAlert("Notification", AppMessagesEng.Prompts.PromptCopiedToClipboard);
        }
    }

    [RelayCommand]
    private async Task CreateVariableAsync()
    {
        if (IsSelectionValid(PromptText, SelectionLength))
        {
            EncloseSelectedTextWithBraces();
        }
        else
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.SelectWordError);
        }
    }

    [RelayCommand]
    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(CursorPosition, SelectionLength);
    }

    // ============================ MANEJO DE VARIABLES Y BRACES ============================

    private async void EncloseSelectedTextWithBraces()
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.InvalidTextSelection);
            return;
        }

        var handler = new BraceTextHandler(PromptText);

        if (handler.IsSurroundedByBraces(CursorPosition, SelectionLength))
        {
            await HandleSelectedTextAsync(CursorPosition, SelectionLength);

            return;
        }

        string selectedText = PromptText.Substring(CursorPosition, SelectionLength);

        handler.UpdateText(CursorPosition, SelectionLength, $"{{{selectedText}}}");

        PromptText = handler.Text;

        UpdateSelectedTextLabelCount(BraceTextHandler.CountWordsWithBraces(handler.Text));
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await AlertService.ShowAlert("Warning", AppMessagesEng.Prompts.PromptEmptyAndUnSelected);

            return;
        }

        var handler = new BraceTextHandler(PromptText);

        if (!handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.InvalidTextSelection);

            return;
        }

        if (handler.IsSurroundedByBraces(cursorPosition, selectionLength))
        {
            var (startIndex, length) = handler.AdjustSelectionForBraces(cursorPosition, selectionLength);

            string selectedText = handler.ExtractTextWithoutBraces(startIndex, length);

            handler.UpdateText(startIndex, length, selectedText);

            PromptText = handler.Text;

            UpdateSelectedTextLabelCount(BraceTextHandler.CountWordsWithBraces(handler.Text));
        }
        else
        {
            await AlertService.ShowAlert("Warning", AppMessagesEng.Warnings.WordHasNoBraces);
        }
    }

    // ============================ MANEJO DE PROMPT ============================

    private PromptTemplate CreatePromptTemplate()
    {
        return new PromptTemplate
        {
            Title = PromptTitle,
            Template = PromptText,
            Description = string.IsNullOrWhiteSpace(PromptDescription) ? "N/A" : PromptDescription,
            Variables = ExtractVariables(PromptText).ToDictionary(v => v, v => string.Empty)
        };
    }

    private void ClearPromptInputs()
    {
        PromptTitle = string.Empty;

        PromptDescription = string.Empty;

        PromptText = string.Empty;

        UpdateSelectedTextLabelCount(0);
    }
}