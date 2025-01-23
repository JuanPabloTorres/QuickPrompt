using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

public partial class MainPageViewModel(PromptDatabaseService _databaseService) : BaseViewModel
{
    [ObservableProperty]
    private int cursorPosition;

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> savedPrompts = new();

    [ObservableProperty]
    private string promptText;

    [ObservableProperty]
    private string selectedTextLabel = "Texto seleccionado: 0";

    private int selectedWordCount = 0;

    [ObservableProperty]
    private int selectionLength;

    [ObservableProperty]
    private string promptTitle;  // Título del prompt

    [ObservableProperty]
    private string promptDescription;  // Descripción del prompt

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var validator = new PromptValidator();

            string validationError = validator.Validate(PromptTitle, PromptText);

            if (!string.IsNullOrEmpty(validationError))
            {
                await AppShell.Current.DisplayAlert("Error", validationError, "OK");

                return;
            }

            var newPrompt = CreatePromptTemplate();

            await _databaseService.SavePromptAsync(newPrompt);

            ClearPromptInputs();

            await AppShell.Current.DisplayAlert("Guardado", AppMessages.Prompts.PromptSavedSuccess, "OK");
        }, AppMessages.Prompts.PromptSaveError);
    }

    private PromptTemplate CreatePromptTemplate()
    {
        return new PromptTemplate
        {
            Title = PromptTitle,
            Template = PromptText,
            Description = string.IsNullOrWhiteSpace(PromptDescription) ? "Sin descripción" : PromptDescription,
            Variables = ExtractVariables(PromptText).ToDictionary(v => v, v => string.Empty)
        };
    }

    // Método para extraer variables del texto entre llaves {}
    private List<string> ExtractVariables(string text)
    {
        var variables = new List<string>();

        int startIndex = text.IndexOf('{');

        while (startIndex != -1)
        {
            int endIndex = text.IndexOf('}', startIndex);

            if (endIndex != -1)
            {
                string variable = text.Substring(startIndex + 1, endIndex - startIndex - 1);

                if (!variables.Contains(variable))
                {
                    variables.Add(variable);
                }
                startIndex = text.IndexOf('{', endIndex);
            }
            else
            {
                break;
            }
        }

        return variables;
    }

    // Método para limpiar los campos de entrada
    private void ClearPromptInputs()
    {
        PromptTitle = string.Empty;

        PromptDescription = string.Empty;

        PromptText = string.Empty;

        selectedWordCount = 0;

        SelectedTextLabel = $"Texto seleccionado: {selectedWordCount}";
    }

    [RelayCommand]
    private void ClearText()
    {
        ClearPromptInputs();
    }

    [RelayCommand]
    private async Task CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(PromptText))
        {
            await Clipboard.Default.SetTextAsync(PromptText);

            await AlertService.ShowAlert("Copiado", "El prompt se ha guardado en el portapapeles.");
        }
    }

    [RelayCommand]
    private async Task CreateVariableAsync()
    {
        if (IsSelectionValid())
        {
            EncloseSelectedTextWithBraces();

            UpdateSelectedTextLabel();
        }
        else
        {
            await AlertService.ShowAlert("Error", AppMessages.Warnings.SelectWordError);
        }
    }

    private async void EncloseSelectedTextWithBraces()
    {
        if (SelectionLength <= 0 || string.IsNullOrEmpty(PromptText))
        {
            await AlertService.ShowAlert("Error", "Selecciona un texto válido.");

            return;
        }

        // Crear instancia de BraceTextHandler
        var handler = new BraceTextHandler(PromptText, selectedWordCount);

        // Verificar si la selección es válida
        if (!handler.IsSelectionValid(CursorPosition, SelectionLength))
        {
            await AlertService.ShowAlert("Error", "Selecciona un texto válido.");

            return;
        }

        // Verificar si el texto ya está rodeado por llaves
        if (handler.IsSurroundedByBraces(CursorPosition, SelectionLength))
        {
            await AlertService.ShowAlert("Aviso", "El texto seleccionado ya está rodeado por llaves.");

            return;
        }

        // Obtener el texto seleccionado
        string selectedText = PromptText.Substring(CursorPosition, SelectionLength);

        // Envolver el texto con llaves
        handler.UpdateText(CursorPosition, SelectionLength, $"{{{selectedText}}}");

        // Incrementar el contador
        this.selectedWordCount = handler.IncrementSelectedWordCount();

        // Actualizar el estado global
        PromptText = handler.Text;

        selectedWordCount = handler.SelectedWordCount;

        SelectedTextLabel = $"Texto seleccionado: {selectedWordCount}";
    }

    [RelayCommand]
    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(this.CursorPosition, this.SelectionLength);
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        // Crear instancia de la herramienta
        var handler = new BraceTextHandler(PromptText, selectedWordCount);

        if (handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            if (handler.IsSurroundedByBraces(cursorPosition, selectionLength))
            {
                // Ajustar selección para incluir las llaves
                var (startIndex, length) = handler.AdjustSelectionForBraces(cursorPosition, selectionLength);

                // Extraer texto sin llaves
                string selectedText = handler.ExtractTextWithoutBraces(startIndex, length);

                // Actualizar texto y contador
                handler.UpdateText(startIndex, length, selectedText);

                handler.DecrementSelectedWordCount();

                // Actualizar estado global
                PromptText = handler.Text;

                selectedWordCount = handler.SelectedWordCount;

                SelectedTextLabel = $"Texto seleccionado: {selectedWordCount}";
            }
            else
            {
                await AlertService.ShowAlert("Aviso", "La palabra seleccionada no tiene llaves alrededor para quitar.");
            }
        }
        else
        {
            await AlertService.ShowAlert("Error", "Selecciona una palabra válida para restaurar.");
        }
    }

    private bool IsSelectionValid() => !string.IsNullOrEmpty(PromptText) && SelectionLength > 0;

    private void UpdateSelectedTextLabel()
    {
        SelectedTextLabel = $"Texto seleccionado: {selectedWordCount}";
    }
}