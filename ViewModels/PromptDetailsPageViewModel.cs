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

public partial class PromptDetailsPageViewModel(PromptDatabaseService _databaseService, IChatGPTService _chatGPTService) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    private string promptTitle;

    [ObservableProperty]
    private string promptText;

    [ObservableProperty]
    private string description;

    [ObservableProperty]
    private string finalPrompt;

    [ObservableProperty]
    private bool isShareButtonVisible = false;

    [ObservableProperty]
    private ObservableCollection<VariableInput> variables = new();

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

                // Cargar las variables con inputs vacíos
                Variables = new ObservableCollection<VariableInput>(
                    prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty })
                );
            }
            else
            {
                await AppShell.Current.DisplayAlert("Aviso", AppMessages.Prompts.PromptNotFound, "OK");
            }
        }, AppMessages.Prompts.PromptLoadError);
    }

    [RelayCommand]
    private async Task SendPromptToChatGPTAsync()
    {
        await AppShell.Current.DisplayAlert("Notificación", AppMessages.Prompts.PromptDevelopmentMessage, "OK");

        return;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"]?.ToString(), out Guid id))
        {
            await LoadPromptAsync(id);
        }
        else
        {
            await AppShell.Current.DisplayAlert("Error", "ID de prompt no válido.", "OK");
        }
    }

    [RelayCommand]
    private async Task GeneratePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (!AreVariablesFilled())
            {
                await AppShell.Current.DisplayAlert("Error", AppMessages.Prompts.PromptVariablesError, "OK");
                return;
            }

            FinalPrompt = GenerateFinalPrompt();

            IsShareButtonVisible = true; // Mostrar el botón de compartir

            await AppShell.Current.DisplayAlert("Prompt Generado", FinalPrompt, "OK");
        }, AppMessages.GenericError);
    }

    // Métodos auxiliares
    private bool AreVariablesFilled()
    {
        return Variables.All(v => !string.IsNullOrWhiteSpace(v.Value));
    }

    private string GenerateFinalPrompt()
    {
        var finalPromptBuilder = new StringBuilder(PromptText);

        foreach (var variable in Variables)
        {
            finalPromptBuilder.Replace($"{{{variable.Name}}}", variable.Value);
        }

        return finalPromptBuilder.ToString();
    }

    // Comando para compartir un prompt
    [RelayCommand]
    protected async Task SharePromptAsync()
    {
        if (string.IsNullOrEmpty(this.PromptTitle) || string.IsNullOrEmpty(this.FinalPrompt))
        {
            await AlertService.ShowAlert("Error", "No se seleccionó un prompt para compartir.");

            return;
        }

        await ExecuteWithLoadingAsync(
            async () =>
            {
                await SharePromptService.SharePromptAsync(this.PromptTitle, this.FinalPrompt);
            },
            "Ocurrió un error al intentar compartir el prompt."
        );
    }

    [RelayCommand]
    private async Task NavigateToEditPrompt(PromptTemplate selectedPrompt)
    {
        if (selectedPrompt != null)
        {
            await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
        {
            { "selectedId", selectedPrompt.Id }
        });
        }
    }
}

public class VariableInput
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}