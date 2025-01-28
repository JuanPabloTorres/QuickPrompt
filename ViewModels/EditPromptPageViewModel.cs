using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

public partial class EditPromptPageViewModel(PromptDatabaseService _databaseService) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    private PromptTemplate promptTemplate;

    [ObservableProperty]
    private int cursorPosition;

    [ObservableProperty]
    private int selectionLength;

    private int variablesWordCount = 0;

    [ObservableProperty]
    private string promptText;

    [ObservableProperty]
    private string selectedTextLabel = "Texto seleccionado: 0";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"].ToString(), out Guid promptId))
        {
            await LoadPromptAsync(promptId);
        }
    }

    private async Task LoadPromptAsync(Guid promptId)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var prompt = await _databaseService.GetPromptByIdAsync(promptId);

            if (prompt != null)
            {
                this.PromptTemplate = prompt;

                this.variablesWordCount = BraceTextHandler.CountWordsWithBraces(prompt.Template);

                this.PromptTemplate.Variables = ExtractVariables(this.PromptTemplate.Template).ToDictionary(v => v, v => string.Empty);

                UpdateSelectedTextLabel();
            }
            else
            {
                await AppShell.Current.DisplayAlert("Aviso", AppMessages.Prompts.PromptNotFound, "OK");
            }
        }, AppMessages.Prompts.PromptLoadError);
    }

    [RelayCommand]
    private async Task UpdateChangesAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (!ValidatePromptTemplate())
                return;

            UpdatePromptVariables();

            await UpdatePromptChangesAsync();

            await NotifySuccessAndNavigateBack();
        }, AppMessages.Prompts.PromptSaveError);
    }

    private bool ValidatePromptTemplate()
    {
        var validator = new PromptValidator();

        string validationError = validator.Validate(PromptTemplate.Title, PromptTemplate.Template);

        if (!string.IsNullOrEmpty(validationError))
        {
            AppShell.Current.DisplayAlert("Error", validationError, "OK").ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private void UpdatePromptVariables()
    {
        PromptTemplate.Variables = ExtractVariables(PromptTemplate.Template).ToDictionary(v => v, v => string.Empty);
    }

    private async Task UpdatePromptChangesAsync()
    {
        await _databaseService.UpdatePromptAsync(
            PromptTemplate.Id,
            PromptTemplate.Title,
            PromptTemplate.Template,
            PromptTemplate.Description,
            PromptTemplate.Variables);
    }

    private PromptTemplate InitializePromptTemplate(PromptTemplate existingPrompt, string newTemplate)
    {
        return new PromptTemplate
        {
            Id = existingPrompt.Id,
            Template = newTemplate,
            Title = existingPrompt.Title,
            Description = existingPrompt.Description,
            Variables = ExtractVariables(newTemplate).ToDictionary(v => v, v => string.Empty)
        };
    }

    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(this.CursorPosition, this.SelectionLength);
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        // Crear instancia de la herramienta
        var handler = new BraceTextHandler(this.PromptTemplate.Template, variablesWordCount);

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

                this.PromptTemplate = InitializePromptTemplate(this.PromptTemplate, handler.Text);

                variablesWordCount = handler.SelectedWordCount;

                UpdateSelectedTextLabel();
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

    [RelayCommand]
    private async Task CreateVariableAsync()
    {
        if (IsSelectionValid())
        {
            EncloseSelectedTextWithBraces();
        }
        else
        {
            await AlertService.ShowAlert("Error", AppMessages.Warnings.SelectWordError);
        }
    }

    private async void EncloseSelectedTextWithBraces()
    {
        // Crear instancia de BraceTextHandler
        var handler = new BraceTextHandler(this.PromptTemplate.Template, variablesWordCount);

        // Verificar si la selección es válida
        if (!handler.IsSelectionValid(CursorPosition, SelectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessages.Warnings.SelectWordError);

            return;
        }

        // Verificar si el texto ya está rodeado por llaves
        if (handler.IsSurroundedByBraces(CursorPosition, SelectionLength))
        {
            await RemoveBracesFromSelectedText();

            return;
        }

        // Obtener el texto seleccionado
        string selectedText = this.PromptTemplate.Template.Substring(CursorPosition, SelectionLength);

        // Envolver el texto con llaves
        handler.UpdateText(CursorPosition, SelectionLength, $"{{{selectedText}}}");

        // Incrementar el contador
        this.variablesWordCount = handler.IncrementSelectedWordCount();

        this.PromptTemplate = InitializePromptTemplate(this.PromptTemplate, handler.Text);

        variablesWordCount = handler.SelectedWordCount;

        UpdateSelectedTextLabel();
    }

    private bool IsSelectionValid() => !string.IsNullOrEmpty(this.PromptTemplate.Template) && SelectionLength > 0;

    private void UpdateSelectedTextLabel()
    {
        SelectedTextLabel = $"Texto seleccionado: {variablesWordCount}";
    }

    [RelayCommand]
    private async Task GoToDetail()
    {
        if (this.PromptTemplate != null)
        {
            await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={this.PromptTemplate.Id}", true);
        }
    }
}