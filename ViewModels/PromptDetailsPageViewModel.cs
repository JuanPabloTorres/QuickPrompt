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

            FinalPrompt = string.Empty;
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

            FinalPrompt = GenerateFinalPrompt();

            IsShareButtonVisible = true; // Mostrar botón de compartir

            // Mostrar anuncio intersticial después de guardar el prompt
            await _adMobService.ShowInterstitialAdAsync();

          
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
        await ExecuteWithLoadingAsync(async () =>
        {
            if (string.IsNullOrEmpty(this.FinalPrompt))
            {
                await AlertService.ShowAlert("Error", "No prompt generated.");

                return;
            }

            // Show a Toast notification instead of DisplayAlert
            var toast = Toast.Make("Opening ChatGPT...", ToastDuration.Short);

            await toast.Show();

            // Open ChatGPT in WebView with prompt
            await Application.Current.MainPage.Navigation.PushAsync(new ChatGptPage(FinalPrompt));
        });
    }

    [RelayCommand]
    private async Task SendPromptToGeminiAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (string.IsNullOrEmpty(this.FinalPrompt))
            {
                await AlertService.ShowAlert("Error", "No prompt generated.");

                return;
            }

            // Show a Toast notification instead of DisplayAlert
            var toast = Toast.Make("Opening Gemini...", ToastDuration.Short);

            await toast.Show();

            // Open ChatGPT in WebView with prompt
            await Application.Current.MainPage.Navigation.PushAsync(new GeminiPage(FinalPrompt));
        });
    }

    [RelayCommand]
    private async Task SendPromptToGrokAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (string.IsNullOrEmpty(this.FinalPrompt))
            {
                await AlertService.ShowAlert("Error", "No prompt generated.");

                return;
            }

            // Show a Toast notification instead of DisplayAlert
            var toast = Toast.Make("Opening Gemini...", ToastDuration.Short);

            await toast.Show();

            // Open ChatGPT in WebView with prompt
            await Application.Current.MainPage.Navigation.PushAsync(new GrokPage(FinalPrompt));
        });
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
    private void ClearText() => ClearPromptInputs();

    private void ClearPromptInputs()
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

        this.IsShareButtonVisible = !string.IsNullOrEmpty(this.FinalPrompt);
    }
}

public partial class VariableInput : ObservableObject
{
    [ObservableProperty]
    private string? name;

    [ObservableProperty]
    private string? value;
}