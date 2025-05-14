using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

public partial class PromptDetailsPageViewModel(PromptDatabaseService _databaseService, IChatGPTService _chatGPTService, AdmobService admobService) : BaseViewModel(_databaseService, admobService), IQueryAttributable
{
    // =========================== 🔹 PROPIEDADES OBSERVABLES ===========================
    [ObservableProperty] private string promptTitle;

    [ObservableProperty] private string promptText;

    [ObservableProperty] private string description;

    [ObservableProperty] private string finalPrompt;

    [ObservableProperty] private bool isShareButtonVisible = false;

    [ObservableProperty] private Guid promptID;

    [ObservableProperty] private ObservableCollection<VariableInput> variables = new();

    // =========================== 🔹 MÉTODOS PRINCIPALES ===========================

    /// <summary>
    /// Carga el prompt desde la base de datos usando su ID.
    /// </summary>
    public async Task LoadPromptAsync(Guid selectedId)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var prompt = await _databaseService.GetPromptByIdAsync(selectedId);

            if (prompt != null)
            {
                PromptTitle = prompt.Title;

                Description = prompt.Description;

                PromptText = prompt.Template;

                PromptID = prompt.Id;

                var _savedVariables = prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty });

                // Inicializar variables con valores vacíos
                Variables = new ObservableCollection<VariableInput>(_savedVariables);
            }
            else
            {
                await AppShell.Current.DisplayAlert("Notice", AppMessagesEng.Prompts.PromptNotFound, "OK");

                await GoBackAsync();
            }
        }, AppMessagesEng.Prompts.PromptLoadError);
    }

    /// <summary>
    /// Aplica los atributos de consulta para cargar un prompt específico.
    /// </summary>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"]?.ToString(), out Guid id))
        {
            await LoadPromptAsync(id);

            Clear();
        }
        else
        {
            await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
        }
    }

    // =========================== 🔹 GENERACIÓN DEL PROMPT ===========================

    [RelayCommand]
    private async Task GeneratePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (!AreVariablesFilled())
            {
                await AppShell.Current.DisplayAlert("Error", AppMessagesEng.Prompts.PromptVariablesError, "OK");

                return;
            }

            // Mostrar anuncio intersticial después de guardar el prompt
            await _adMobService.ShowInterstitialAdAsync();

            FinalPrompt = GenerateFinalPrompt();

            foreach (var variable in Variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.Value))
                    PromptVariableCache.SaveValue(variable.Name!, variable.Value);
            }

            UpdateVisibility(); // Mostrar botón de compartir

         
        }, AppMessagesEng.GenericError);
    }

    /// <summary>
    /// Verifica si todas las variables han sido llenadas.
    /// </summary>
    private bool AreVariablesFilled()
    {
        return Variables.All(v => !string.IsNullOrWhiteSpace(v.Value));
    }

    /// <summary>
    /// Genera el prompt reemplazando las variables en el texto.
    /// </summary>
    private string GenerateFinalPrompt()
    {
        var finalPromptBuilder = new StringBuilder(PromptText);

        foreach (var variable in Variables)
        {
            finalPromptBuilder.Replace($"<{variable.Name}>", variable.Value);
        }
        return finalPromptBuilder.ToString();
    }

    // =========================== 🔹 INTEGRACIÓN CON AI ===========================
    [RelayCommand]
    private async Task SendPromptToChatGPTAsync()
    {
        IsShareButtonVisible = !string.IsNullOrEmpty(FinalPrompt); // Mostrar botón de compartir // Ocultar botón de compartir al enviar a ChatGPT

        await SendPromptToAsync(nameof(ChatGptPage), "ChatGPT", PromptID, FinalPrompt);
    }

    [RelayCommand]
    private async Task SendPromptToGeminiAsync()
    {
        IsShareButtonVisible = !string.IsNullOrEmpty(FinalPrompt); // Mostrar botón de compartir // Ocultar botón de compartir al enviar a Gemini

        await SendPromptToAsync(nameof(GeminiPage), "Gemini", PromptID, FinalPrompt);
    }

    [RelayCommand]
    private async Task SendPromptToGrokAsync()
    {
        IsShareButtonVisible = !string.IsNullOrEmpty(FinalPrompt); // Mostrar botón de compartir // Ocultar botón de compartir al enviar a Grok

        await SendPromptToAsync(nameof(GrokPage), "Grok", PromptID, FinalPrompt);
    }

    [RelayCommand]
    private async Task SendPromptToMistralAsync()
    {
        IsShareButtonVisible = !string.IsNullOrEmpty(FinalPrompt); // Mostrar botón de compartir // Ocultar botón de compartir al enviar a Grok

        await SendPromptToAsync(nameof(MistralChatPage), "Mistral AI", PromptID, FinalPrompt);
    }

    [RelayCommand]
    private async Task SendPromptToCopilotAsync()
    {
        IsShareButtonVisible = !string.IsNullOrEmpty(FinalPrompt); // Mostrar botón de compartir // Ocultar botón de compartir al enviar a Grok

        await SendPromptToAsync(nameof(CopilotChatPage), "Copilot", PromptID, FinalPrompt);
    }
    private void UpdateVisibility()
    {
        IsShareButtonVisible = !string.IsNullOrWhiteSpace(FinalPrompt);

        ShowPromptActions = !string.IsNullOrWhiteSpace(FinalPrompt);
    }

   

    [RelayCommand]
    private void SelectSuggestion(VariableSuggestionSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection?.VariableName) || string.IsNullOrWhiteSpace(selection.SuggestedValue))
            return;

        var variable = Variables.FirstOrDefault(v => v.Name == selection.VariableName);

        if (variable != null)
        {
            variable.Value = selection.SuggestedValue;
        }
    }

    [RelayCommand]
    protected async Task SharePromptAsync()
    {
        if (string.IsNullOrEmpty(this.PromptTitle) || string.IsNullOrEmpty(this.FinalPrompt))
        {
            await AlertService.ShowAlert("Error", "No prompt selected for sharing.");

            return;
        }

        await ExecuteWithLoadingAsync(
            async () =>
            {
                await SharePromptService.SharePromptAsync(this.PromptTitle, this.FinalPrompt);
            },
            AppMessagesEng.Prompts.PromptSharedError

        );
    }

    // =========================== 🔹 NAVEGACIÓN ENTRE PÁGINAS ===========================

    /// <summary>
    /// Navega a la página de edición del prompt seleccionado.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToEditPrompt(Guid promptId)
    {
        await ExecuteWithLoadingAsync(
          async () =>
          {
              if (promptId == Guid.Empty)
              {
                  //await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
                  throw new Exception("Invalid prompt ID.");
              }

              await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
        {
            { "selectedId", promptId }
        });
          },
          AppMessagesEng.GenericError);
    }

    /// <summary>
    /// Comando para limpiar los campos de entrada de texto del prompt.
    /// </summary>
    [RelayCommand]
    private void ClearText() => Clear();

    public void Clear()
    {
        if (Variables.All(v => string.IsNullOrEmpty(v.Value)))
        {
            return;
        }

        foreach (var variable in Variables)
        {
            variable.Value = string.Empty;
        }

        this.FinalPrompt = string.Empty;

        UpdateVisibility();
    }
}