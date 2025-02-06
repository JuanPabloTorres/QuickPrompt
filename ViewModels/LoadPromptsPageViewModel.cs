using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels;

public partial class LoadPromptsPageViewModel(PromptDatabaseService _databaseService) : BaseViewModel
{
    // ======================= 📌 PROPIEDADES =======================

    public BlockHandler<PromptTemplate> blockHandler = new BlockHandler<PromptTemplate>();

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> prompts = new();  // Inicializamos la lista vacía

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    private bool isMoreDataAvailable = true;  // Para indicar si hay más datos disponibles

    // ======================= 📌 MÉTODO PRINCIPAL: Cargar Prompts =======================
    [RelayCommand]
    public async Task LoadInitialPrompts()
    {
        // Reiniciar el paginador
        blockHandler.Reset();

        Prompts.Clear();  // Limpiar la lista antes de cargar

        this.SearchQuery = string.Empty;

        // Obtener el total de prompts y cargar el primer bloque
        await UpdateTotalPromptsCountAsync();

        await LoadPromptsAsync();
    }

    public async Task UpdateTotalPromptsCountAsync()
    {
        // Obtener el total de prompts de la base de datos y configurar el BlockHandler
        blockHandler.CountInDB = await _databaseService.GetTotalPromptsCountAsync();
    }

    public async Task CheckForMorePromptsAsync()
    {
        await UpdateTotalPromptsCountAsync();

        // Verificar si hay más datos por cargar
        if (blockHandler.HasMoreData())
        {
            IsMoreDataAvailable = true;
        }
        else
        {
            IsMoreDataAvailable = false;
        }
    }

    [RelayCommand]
    public async Task LoadMorePrompts()
    {
        if (string.IsNullOrWhiteSpace(this.SearchQuery))
        {
            await LoadPromptsAsync();
        }
        else
        {
            await FilterPromptsAsync(this.SearchQuery);
        }
    }

    /// <summary>
    /// Carga el próximo bloque de prompts desde la base de datos y actualiza la lista.
    /// </summary>
    public async Task LoadPromptsAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            await UpdateTotalPromptsCountAsync();

            // Calcular el desplazamiento y ajustarlo para evitar duplicados
            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);

            // Calcular el tamaño del lote sin exceder los datos disponibles
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize);

            if (promptList.Any())
            {
                // Agregar los nuevos prompts y ordenar la colección
                Prompts.AddRange(promptList);

                Prompts = Prompts.OrderBy(p => p.Title).ToObservableCollection();

                // Actualizar los datos en el BlockHandler y avanzar al siguiente bloque
                blockHandler.Data = Prompts;

                blockHandler.NextBlock();
            }

            // Verificar si hay más datos por cargar
            await CheckForMorePromptsAsync();
        }, AppMessages.Prompts.PromptLoadError);
    }

    // ======================= 🔍 FILTRAR PROMPTS =======================
    [RelayCommand]
    private async Task FilterPromptsAsync(string query)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            await UpdateTotalPromptsCountAsync();

            // Calcular el desplazamiento y ajustarlo para evitar duplicados
            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);

            // Calcular el tamaño del lote sin exceder los datos disponibles
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize, query);

            if (promptList.Any())
            {
                // Agregar los nuevos prompts y ordenar la colección
                Prompts.AddRange(promptList);

                Prompts = Prompts.OrderBy(p => p.Title).ToObservableCollection();

                // Actualizar los datos en el BlockHandler y avanzar al siguiente bloque
                blockHandler.Data = Prompts;

                blockHandler.NextBlock();
            }

            // Verificar si hay más datos por cargar
            await CheckForMorePromptsAsync();
        }, AppMessages.Prompts.PromptLoadError);
    }

    // ======================= 📌 SELECCIONAR UN PROMPT =======================
    [RelayCommand]
    private async Task SelectPrompt(PromptTemplate selectedPrompt)
    {
        if (selectedPrompt != null)
        {
            await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={selectedPrompt.Id}", true);
        }
    }

    // ======================= 🔄 REFRESCAR PROMPTS =======================
    [RelayCommand]
    private async Task RefreshPrompts()
    {
        await LoadInitialPrompts();
    }

    // ======================= ✏️ EDITAR UN PROMPT =======================
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

    // ======================= ❌ ELIMINAR UN PROMPT =======================
    [RelayCommand]
    private async Task DeletePromptAsync(PromptTemplate selectedPrompt)
    {
        if (selectedPrompt == null) return;

        bool confirm = await AppShell.Current.DisplayAlert(
            "Confirm Deletion",
            $"Are you sure you want to delete the prompt \"{selectedPrompt.Title}\"?",
            "Delete", "Cancel");

        if (confirm)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await _databaseService.DeletePromptAsync(selectedPrompt);

                Prompts.Remove(selectedPrompt);

                await CheckForMorePromptsAsync();

                await AppShell.Current.DisplayAlert("Success", $"The prompt {selectedPrompt.Title} has been deleted.", "OK");
            }, AppMessagesEng.Prompts.PromptDeleteError);
        }
    }
}