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

                UpdateSelectedTextLabel();
            }
            else
            {
                await AppShell.Current.DisplayAlert("Aviso", AppMessages.Prompts.PromptNotFound, "OK");
            }
        }, AppMessages.Prompts.PromptLoadError);
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var validator = new PromptValidator();

            string validationError = validator.Validate(PromptTemplate.Title, PromptTemplate.Template);

            if (!string.IsNullOrEmpty(validationError))
            {
                await AppShell.Current.DisplayAlert("Error", validationError, "OK");

                return;
            }

            await _databaseService.SavePromptAsync(this.PromptTemplate);

            await AppShell.Current.DisplayAlert("Éxito", "El prompt ha sido actualizado correctamente.", "OK");

            await GoBackAsync(); // Utiliza el método centralizado para regresar
        }, AppMessages.Prompts.PromptSaveError);
    }

    [RelayCommand]
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

                // Actualizar estado global
                //this.PromptTemplate.Template = handler.Text;

                // Si aún no detecta el cambio, reasigna el objeto completo
                this.PromptTemplate = new PromptTemplate
                {
                    Template = handler.Text,
                    Title = this.PromptTemplate.Title,
                    Description = this.PromptTemplate.Description,
                    // Agrega más propiedades si las tienes
                };

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

            UpdateSelectedTextLabel();
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
            await AlertService.ShowAlert("Error", AppMessages.Warnings.WordAlreadyHasBraces);

            return;
        }

        // Obtener el texto seleccionado
        string selectedText = this.PromptTemplate.Template.Substring(CursorPosition, SelectionLength);

        // Envolver el texto con llaves
        handler.UpdateText(CursorPosition, SelectionLength, $"{{{selectedText}}}");

        // Incrementar el contador
        this.variablesWordCount = handler.IncrementSelectedWordCount();

        // Si aún no detecta el cambio, reasigna el objeto completo
        this.PromptTemplate = new PromptTemplate
        {
            Template = handler.Text,
            Title = this.PromptTemplate.Title,
            Description = this.PromptTemplate.Description,
            // Agrega más propiedades si las tienes
        };

        variablesWordCount = handler.SelectedWordCount;

        UpdateSelectedTextLabel();
    }

    private bool IsSelectionValid() => !string.IsNullOrEmpty(this.PromptTemplate.Template) && SelectionLength > 0;

    private void UpdateSelectedTextLabel()
    {
        SelectedTextLabel = $"Texto seleccionado: {variablesWordCount}";
    }
}