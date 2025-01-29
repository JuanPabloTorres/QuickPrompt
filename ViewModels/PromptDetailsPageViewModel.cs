using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

//public partial class PromptDetailsPageViewModel(PromptDatabaseService _databaseService, IChatGPTService _chatGPTService) : BaseViewModel, IQueryAttributable
//{
//    [ObservableProperty]
//    private string promptTitle;

//    [ObservableProperty]
//    private string promptText;

//    [ObservableProperty]
//    private string description;

//    [ObservableProperty]
//    private string finalPrompt;

//    [ObservableProperty]
//    private bool isShareButtonVisible = false;

//    [ObservableProperty]
//    private Guid promptID;

//    [ObservableProperty]
//    private ObservableCollection<VariableInput> variables = new();

//    public async Task LoadPromptAsync(Guid selectedId)
//    {
//        await ExecuteWithLoadingAsync(async () =>
//        {
//            var prompt = await _databaseService.GetPromptByIdAsync(selectedId);

//            if (prompt != null)
//            {
//                PromptTitle = prompt.Title;
//                Description = prompt.Description;
//                PromptText = prompt.Template;
//                PromptID = prompt.Id;

//                // Cargar las variables con inputs vacíos
//                Variables = new ObservableCollection<VariableInput>(
//                    prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty })
//                );
//            }
//            else
//            {
//                await AppShell.Current.DisplayAlert("Noticed", AppMessagesEng.Prompts.PromptNotFound, "OK");
//            }
//        }, AppMessagesEng.Prompts.PromptLoadError);
//    }

//    [RelayCommand]
//    private async Task SendPromptToChatGPTAsync()
//    {
//        await AppShell.Current.DisplayAlert("Notification", AppMessagesEng.Prompts.PromptDevelopmentMessage, "OK");

//        return;
//    }

//    public async void ApplyQueryAttributes(IDictionary<string, object> query)
//    {
//        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"]?.ToString(), out Guid id))
//        {
//            await LoadPromptAsync(id);
//        }
//        else
//        {
//            await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
//        }
//    }

//    [RelayCommand]
//    private async Task GeneratePromptAsync()
//    {
//        await ExecuteWithLoadingAsync(async () =>
//        {
//            if (!AreVariablesFilled())
//            {
//                await AppShell.Current.DisplayAlert("Error", AppMessagesEng.Prompts.PromptVariablesError, "OK");
//                return;
//            }

//            FinalPrompt = GenerateFinalPrompt();

//            IsShareButtonVisible = true; // Mostrar el botón de compartir

//            await AppShell.Current.DisplayAlert("Prompt Generated", FinalPrompt, "OK");
//        }, AppMessagesEng.GenericError);
//    }

//    // Métodos auxiliares
//    private bool AreVariablesFilled()
//    {
//        return Variables.All(v => !string.IsNullOrWhiteSpace(v.Value));
//    }

//    private string GenerateFinalPrompt()
//    {
//        var finalPromptBuilder = new StringBuilder(PromptText);

//        foreach (var variable in Variables)
//        {
//            finalPromptBuilder.Replace($"{{{variable.Name}}}", variable.Value);
//        }

//        return finalPromptBuilder.ToString();
//    }

//    // Comando para compartir un prompt
//    [RelayCommand]
//    protected async Task SharePromptAsync()
//    {
//        if (string.IsNullOrEmpty(this.PromptTitle) || string.IsNullOrEmpty(this.FinalPrompt))
//        {
//            //await AlertService.ShowAlert("Error", "No se seleccionó un prompt para compartir.");

//            await AlertService.ShowAlert("Error", "No prompt selected for sharing.");

//            return;
//        }

//        await ExecuteWithLoadingAsync(
//            async () =>
//            {
//                await SharePromptService.SharePromptAsync(this.PromptTitle, this.FinalPrompt);
//            },
//            "An error occurred while trying to share the prompt."
//        );
//    }

//    [RelayCommand]
//    private async Task NavigateToEditPrompt(Guid promptId)
//    {
//        // Validar si el ID es válido antes de navegar
//        if (promptId == Guid.Empty)
//        {
//            await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
//            return;
//        }

//        // Navegar a la página de edición con el ID seleccionado
//        await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
//    {
//        { "selectedId", promptId }
//    });
//    }
//}

public partial class PromptDetailsPageViewModel(PromptDatabaseService _databaseService, IChatGPTService _chatGPTService) : BaseViewModel, IQueryAttributable
{
    // ===========================
    // 🔹 PROPIEDADES OBSERVABLES
    // ===========================
    [ObservableProperty] private string promptTitle;
    [ObservableProperty] private string promptText;
    [ObservableProperty] private string description;
    [ObservableProperty] private string finalPrompt;
    [ObservableProperty] private bool isShareButtonVisible = false;
    [ObservableProperty] private Guid promptID;
    [ObservableProperty] private ObservableCollection<VariableInput> variables = new();

    // ===========================
    // 🔹 MÉTODOS PRINCIPALES
    // ===========================

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

                // Inicializar variables con valores vacíos
                Variables = new ObservableCollection<VariableInput>(
                    prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty })
                );
            }
            else
            {
                await AppShell.Current.DisplayAlert("Notice", AppMessagesEng.Prompts.PromptNotFound, "OK");
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
        }
        else
        {
            await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
        }
    }

    // ===========================
    // 🔹 GENERACIÓN DEL PROMPT
    // ===========================

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

            await AppShell.Current.DisplayAlert("Prompt Generated", FinalPrompt, "OK");
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
            finalPromptBuilder.Replace($"{{{variable.Name}}}", variable.Value);
        }
        return finalPromptBuilder.ToString();
    }

    // ===========================
    // 🔹 INTEGRACIÓN CON CHATGPT
    // ===========================

    [RelayCommand]
    private async Task SendPromptToChatGPTAsync()
    {
        await AppShell.Current.DisplayAlert("Notification", AppMessagesEng.Prompts.PromptDevelopmentMessage, "OK");
    }

    // ===========================
    // 🔹 COMPARTIR PROMPT
    // ===========================

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
            "An error occurred while trying to share the prompt."
        );
    }

    // ===========================
    // 🔹 NAVEGACIÓN ENTRE PÁGINAS
    // ===========================

    /// <summary>
    /// Navega a la página de edición del prompt seleccionado.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToEditPrompt(Guid promptId)
    {
        if (promptId == Guid.Empty)
        {
            await AppShell.Current.DisplayAlert("Error", "Invalid prompt ID.", "OK");
            return;
        }

        await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
        {
            { "selectedId", promptId }
        });
    }
}


public class VariableInput
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}