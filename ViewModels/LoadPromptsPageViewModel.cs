using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels;

public partial class LoadPromptsPageViewModel(PromptDatabaseService _databaseService) : BaseViewModel
{
    //private readonly PromptDatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> prompts = new();  // Inicializamos la lista vacía

    [ObservableProperty]
    private string searchQuery;

    public async Task LoadPromptsAsync()
    {
        if (!string.IsNullOrEmpty(this.SearchQuery))
        {
            this.SearchQuery = string.Empty;
        }

        await ExecuteWithLoadingAsync(async () =>
        {
            var promptList = await _databaseService.GetAllPromptsAsync();

            Prompts = new ObservableCollection<PromptTemplate>(promptList.OrderBy(v => v.Title) ?? Enumerable.Empty<PromptTemplate>());
        }, AppMessages.Prompts.PromptLoadError);
    }

    [RelayCommand]
    private async Task FilterPromptsAsync(string query)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                this.Prompts = new ObservableCollection<PromptTemplate>(Prompts);
            }
            else
            {
                var filtered = Prompts.Where(p => p.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

                this.Prompts = new ObservableCollection<PromptTemplate>(filtered);
            }
        }, AppMessages.Prompts.PromptFilterError);
    }

    // Comando para seleccionar un prompt
    [RelayCommand]
    private async Task SelectPrompt(PromptTemplate selectedPrompt)
    {
        if (selectedPrompt != null)
        {
            await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={selectedPrompt.Id}", true);
        }
    }

    [RelayCommand]
    private async Task RefreshPrompts()
    {
        await LoadPromptsAsync();
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

    [RelayCommand]
    private async Task DeletePromptAsync(PromptTemplate selectedPrompt)
    {
        if (selectedPrompt == null) return;

        bool confirm = await AppShell.Current.DisplayAlert(
            "Confirmar Eliminación",
            $"¿Estás seguro de que deseas eliminar el prompt \"{selectedPrompt.Title}\"?",
            "Eliminar", "Cancelar");

        if (confirm)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await _databaseService.DeletePromptAsync(selectedPrompt);
                Prompts.Remove(selectedPrompt);

                await AppShell.Current.DisplayAlert("Éxito", "El prompt ha sido eliminado.", "OK");
            }, AppMessages.Prompts.PromptDeleteError);
        }
    }
}