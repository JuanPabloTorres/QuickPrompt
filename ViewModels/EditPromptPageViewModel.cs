using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

public partial class EditPromptPageViewModel(PromptDatabaseService _databaseService, AdmobService admobService) : BaseViewModel(_databaseService, admobService), IQueryAttributable
{
    // ============================== 🌟 PROPIEDADES ==============================

    [ObservableProperty] private PromptTemplate promptTemplate;

    [ObservableProperty] private int cursorPosition;

    [ObservableProperty] private int selectionLength;

    [ObservableProperty] private string promptText;

    // ============================== 📌 MÉTODOS DE CARGA Y NAVEGACIÓN ==============================

    /// <summary>
    /// Aplica atributos de navegación y carga el prompt si el ID es válido.
    /// </summary>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("selectedId", out var selectedId) && Guid.TryParse(selectedId?.ToString(), out Guid promptId))
        {
            await LoadPromptAsync(promptId);
        }
    }

    /// <summary>
    /// Carga un prompt desde la base de datos y actualiza la interfaz.
    /// </summary>
    private async Task LoadPromptAsync(Guid promptId)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var prompt = await _databaseService.GetPromptByIdAsync(promptId);

            if (prompt != null)
            {
                this.PromptTemplate = prompt;

                this.PromptTemplate.Variables = AngleBraceTextHandler.ExtractVariables(prompt.Template).ToDictionary(v => v, v => string.Empty);

                UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(prompt.Template));
            }
            else
            {
                await AppShell.Current.DisplayAlert("Notice", AppMessagesEng.Prompts.PromptNotFound, "OK");

                await GoBackAsync();
            }
        }, AppMessagesEng.Prompts.PromptLoadError);
    }

    /// <summary>
    /// Navega a la página de detalles del prompt.
    /// </summary>
    [RelayCommand]
    private async Task GoToDetail()
    {
        if (this.PromptTemplate != null)
        {
            await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={this.PromptTemplate.Id}", true);
        }
    }

    // ============================== 🔄 MÉTODOS PARA ACTUALIZACIÓN DEL PROMPT ==============================

    /// <summary>
    /// Actualiza los cambios del prompt si es válido.
    /// </summary>
    [RelayCommand]
    private async Task UpdateChangesAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            _adMobService.LoadInterstitialAd();

            if (!ValidatePromptTemplate())
                return;

            UpdatePromptVariables();

            await UpdatePromptChangesAsync();

            // ✅ Espera que el anuncio se cierre
            await _adMobService.ShowInterstitialAdAndWaitAsync();

            await GenericToolBox.ShowLottieMessageAsync("CompleteAnimation.json", AppMessagesEng.Prompts.PromptSavedSuccess);

            await GoBackAsync();
        }, AppMessagesEng.Prompts.PromptSaveError);
    }

    /// <summary>
    /// Valida que el prompt tenga un título y contenido válido.
    /// </summary>
    private bool ValidatePromptTemplate()
    {
        var validator = new PromptValidator();

        string validationError = validator.ValidateEn(PromptTemplate.Title, PromptTemplate.Template);

        if (!string.IsNullOrEmpty(validationError))
        {
            AppShell.Current.DisplayAlert("Error", validationError, "OK").ConfigureAwait(false);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Extrae y actualiza las variables del prompt.
    /// </summary>
    private void UpdatePromptVariables()
    {
        PromptTemplate.Variables = AngleBraceTextHandler.ExtractVariables(PromptTemplate.Template).ToDictionary(v => v, v => string.Empty);
    }

    /// <summary>
    /// Guarda los cambios del prompt en la base de datos.
    /// </summary>
    private async Task UpdatePromptChangesAsync()
    {
        await _databaseService.UpdatePromptAsync(
            PromptTemplate.Id,
            PromptTemplate.Title,
            PromptTemplate.Template,
            PromptTemplate.Description,
            PromptTemplate.Variables);
    }

    // ============================== 🔠 MANEJO DE TEXTO Y VARIABLES ==============================

    /// <summary> Elimina las llaves `<>` del texto seleccionado. </summary>
    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(this.CursorPosition, this.SelectionLength);
    }

    /// <summary>
    /// Maneja la selección de texto para quitar llaves `{}` si están presentes.
    /// </summary>
    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        var handler = new AngleBraceTextHandler(this.PromptTemplate.Template);

        if (handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            var (startIndex, length) = handler.AdjustSelectionForAngleBraces(cursorPosition, selectionLength);

            string selectedText = handler.ExtractTextWithoutAngleBraces(startIndex, length);

            // Remover el sufijo "/n" si existe en la variable
            selectedText = AngleBraceTextHandler.RemoveVariableSuffix(selectedText);

            handler.UpdateText(startIndex, length, selectedText);

            this.PromptTemplate = InitializePromptTemplate(this.PromptTemplate, handler.Text);

            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
        }
        else
        {
            await AlertService.ShowAlert("Warning", AppMessagesEng.Warnings.InvalidTextSelection);
        }
    }

    /// <summary>
    /// Convierte el texto seleccionado en una variable rodeada de `{}`.
    /// </summary>
    [RelayCommand]
    private async Task CreateVariableAsync()
    {
        if (IsSelectionValid(this.PromptTemplate.Template, this.SelectionLength))
        {
            EncloseSelectedTextWithBraces();
        }
        else
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.SelectWordError);
        }
    }

    private async void EncloseSelectedTextWithBraces()
    {
        var handler = new AngleBraceTextHandler(this.PromptTemplate.Template);

        // Verificar si la selección es válida
        if (!handler.IsSelectionValid(CursorPosition, SelectionLength))
        {
            await AlertService.ShowAlert("Error", AppMessagesEng.Warnings.SelectWordError);
            return;
        }

        // Si ya está rodeado por llaves, eliminarlas
        if (handler.IsSurroundedByAngleBraces(CursorPosition, SelectionLength))
        {
            await RemoveBracesFromSelectedText();

            return;
        }

        // Extraer el texto seleccionado
        string selectedText = this.PromptTemplate.Template.Substring(CursorPosition, SelectionLength);

        if (AngleBraceTextHandler.ContainsVariable($"<{selectedText}>", this.PromptTemplate.Template))
        {
            var _nextVariableVersion = AngleBraceTextHandler.GetNextVariableSuffixVersion(this.PromptTemplate.Template, selectedText);

            // Agregar sufijo numérico para hacer el nombre único
            selectedText += _nextVariableVersion;
        }

        // Actualizar el texto con las llaves `<>` incluidas
        handler.UpdateText(CursorPosition, SelectionLength, $"<{selectedText}>");

        // Actualizar la plantilla con el nuevo texto
        this.PromptTemplate = InitializePromptTemplate(this.PromptTemplate, handler.Text);

        // Actualizar el contador de variables
        UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
    }

    // ============================== 🔧 MÉTODO AUXILIAR PARA INICIALIZAR PROMPTS ==============================

    /// <summary>
    /// Inicializa un nuevo objeto `PromptTemplate` con valores actualizados.
    /// </summary>
    private PromptTemplate InitializePromptTemplate(PromptTemplate existingPrompt, string newTemplate)
    {
        return new PromptTemplate
        {
            Id = existingPrompt.Id,
            Template = newTemplate,
            Title = existingPrompt.Title,
            Description = existingPrompt.Description,
            Variables = AngleBraceTextHandler.ExtractVariables(newTemplate).ToDictionary(v => v, v => string.Empty)
        };
    }
}