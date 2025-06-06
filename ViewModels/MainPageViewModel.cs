using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using System.Text.Json;

namespace QuickPrompt.ViewModels;

public partial class MainPageViewModel(IPromptRepository promptDatabaseService, AdmobService admobService) : BaseViewModel(promptDatabaseService, admobService: admobService)
{
    // ============================ PROPIEDADES Y VARIABLES ============================
    [ObservableProperty] private int cursorPosition;

    [ObservableProperty] private int selectionLength;

    [ObservableProperty] private string promptText;

    [ObservableProperty] private string promptTitle;

    [ObservableProperty] private string promptDescription;

    [ObservableProperty] public string selectedCategory;

    // ============================ COMANDOS ============================

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var validator = new PromptValidator();

            string validationError = validator.ValidateEn(PromptTitle, PromptText, SelectedCategory);

            if (!string.IsNullOrEmpty(validationError))
            {
                await AppShell.Current.DisplayAlert("Error", validationError, "OK");

                return;
            }

            var _category = Enum.TryParse(typeof(PromptCategory), SelectedCategory.ToString(), out var category) ? (PromptCategory)category : PromptCategory.General;

            var newPrompt = PromptTemplate.CreatePromptTemplate(PromptTitle, PromptDescription, PromptText, _category);

            await _databaseService.SavePromptAsync(newPrompt);

            // ✅ Espera que el anuncio se cierre
            await _adMobService.ShowInterstitialAdAndWaitAsync();

            await GenericToolBox.ShowLottieMessageAsync("CompleteAnimation.json", AppMessagesEng.Prompts.PromptSavedSuccess);

            ClearPromptInputs();

            //await AppShell.Current.DisplayAlert("Saved", AppMessagesEng.Prompts.PromptSavedSuccess, "OK");
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
    public async Task CreateVariable()
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

    [RelayCommand]
    private async Task GoToAdvancedBuilder()
    {
        await NavigateToAsync(nameof(PromptBuilderPage));
    }

    // ============================ MANEJO DE VARIABLES Y BRACES ============================
    private async void EncloseSelectedTextWithBraces()
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.InvalidTextSelection);

            return;
        }

        var handler = new AngleBraceTextHandler(PromptText);

        if (handler.IsSurroundedByAngleBraces(CursorPosition, SelectionLength))
        {
            await HandleSelectedTextAsync(CursorPosition, SelectionLength);

            return;
        }

        string selectedText = PromptText.Substring(CursorPosition, SelectionLength);

        if (AngleBraceTextHandler.ContainsVariable($"<{selectedText}>", this.PromptText))
        {
            var _nextVariableVersion = AngleBraceTextHandler.GetNextVariableSuffixVersion(this.PromptText, selectedText);

            // Agregar sufijo numérico para hacer el nombre único
            selectedText += _nextVariableVersion;
        }

        handler.UpdateText(CursorPosition, SelectionLength, $"<{selectedText}>");

        PromptText = handler.Text;

        UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        if (!IsSelectionValid(PromptText, SelectionLength))
        {
            await AlertService.ShowAlert("Warning", AppMessagesEng.Prompts.PromptEmptyAndUnSelected);

            return;
        }

        var handler = new AngleBraceTextHandler(PromptText);

        if (!handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.InvalidTextSelection);

            return;
        }

        if (handler.IsSurroundedByAngleBraces(cursorPosition, selectionLength))
        {
            var (startIndex, length) = handler.AdjustSelectionForAngleBraces(cursorPosition, selectionLength);

            string selectedText = handler.ExtractTextWithoutAngleBraces(startIndex, length);

            // Remover el sufijo "/n" si existe en la variable
            selectedText = AngleBraceTextHandler.RemoveVariableSuffix(selectedText);

            handler.UpdateText(startIndex, length, selectedText);

            PromptText = handler.Text;

            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
        }
        else
        {
            await AlertService.ShowAlert("Warning", AppMessagesEng.Warnings.WordHasNoBraces);
        }
    }

    // ============================ MANEJO DE PROMPT ============================

    private void ClearPromptInputs()
    {
        PromptTitle = string.Empty;

        PromptDescription = string.Empty;

        PromptText = string.Empty;

        IsVisualModeActive = false; // Forzar regreso al modo texto

        SelectedCategory = "";

        UpdateSelectedTextLabelCount(0);
    }

    [RelayCommand]
    private async Task ImportPrompt()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import Prompt",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "public.text" } },
            { DevicePlatform.Android, new[] { "text/plain",".json" } },
            { DevicePlatform.WinUI, new[] { ".json", ".txt" } }
        })
            });

            if (result is null)
                return;

            // Validar extensión del archivo
            if (!result.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !result.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                await AlertService.ShowAlert("Error", "Only .json or .txt files are supported.");
                return;
            }

            string json;

            try
            {
                json = await File.ReadAllTextAsync(result.FullPath);
            }
            catch (Exception readEx)
            {
                await AlertService.ShowAlert("Error", $"Failed to read file: {readEx.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json.Length < 20)
            {
                await AlertService.ShowAlert("Error", "The selected file is empty or invalid.");
                return;
            }

            ImportablePrompt? data;

            try
            {
                data = JsonSerializer.Deserialize<ImportablePrompt>(json);
            }
            catch (Exception deserializationEx)
            {
                await AlertService.ShowAlert("Error", $"The file format is incorrect: {deserializationEx.Message}");

                return;
            }

            if (data is null ||
                string.IsNullOrWhiteSpace(data.Title) ||
                string.IsNullOrWhiteSpace(data.Template) ||
                !AngleBraceTextHandler.ContainsAngleBraces(data.Template))
            {
                await AlertService.ShowAlert("Error", "The file does not contain a valid prompt.");

                return;
            }

            // Asignar los valores
            PromptTitle = data.Title.Trim();

            PromptDescription = string.IsNullOrWhiteSpace(data.Description) ? "N/A" : data.Description.Trim();

            PromptText = data.Template;

            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(PromptText));

            await GenericToolBox.ShowLottieMessageAsync("CompleteAnimation.json", AppMessagesEng.Prompts.PromptImportedSuccess);
        }, AppMessagesEng.GenericError);
    }
}