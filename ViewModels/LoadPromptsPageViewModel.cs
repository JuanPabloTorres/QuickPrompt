using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using QuickPrompt.ViewModels.Prompts;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels;

public partial class LoadPromptsPageViewModel : BaseViewModel
{
    // ======================= 📌 PROPIEDADES =======================
    public ObservableCollection<PromptTemplateViewModel> SelectedPromptsToDelete { get; set; } = new();  // Lista de prompts seleccionados para eliminar

    public BlockHandler<PromptTemplateViewModel> blockHandler = new();

    [ObservableProperty]
    private ObservableCollection<PromptTemplateViewModel> prompts = new();  // Inicializamos la lista vacía

    [ObservableProperty]
    private bool isMoreDataAvailable = true;  // Para indicar si hay más datos disponibles

    public bool IsSearchFlag { get; set; }

    // Constructor primario con la lógica de inicialización
    public LoadPromptsPageViewModel(PromptDatabaseService _databaseService)
    {
        this._databaseService = _databaseService;
    }

    // ======================= 📌 MÉTODO PRINCIPAL: Cargar Prompts =======================
    [RelayCommand]
    public async Task LoadInitialPrompts()
    {
        ToggleSearchFlag(false);

        if (IsAllSelected)
        {
            IsAllSelected = false;
        }

        // Reiniciar la configuración inicial
        blockHandler.Reset();

        Prompts.Clear();

        SelectedPromptsToDelete.Clear();

        CleanSearch();
        // Actualizar el total de prompts y cargar el primer bloque
        await LoadPromptsAsync();
    }

    public override void TogglePromptSelection(PromptTemplateViewModel prompt)
    {
        if (SelectedPromptsToDelete.Contains(prompt))
        {
            SelectedPromptsToDelete.Remove(prompt);  // Si ya estaba seleccionado, lo deseleccionamos
        }
        else
        {
            SelectedPromptsToDelete.Add(prompt);  // Si no estaba seleccionado, lo agregamos
        }
    }

    public void ToggleSearchFlag(bool on_Or_off)
    {
        this.IsSearchFlag = on_Or_off;
    }

    /// <summary>
    /// Devuelve el valor de la cadena de búsqueda actual.
    /// </summary>
    public string GetSearchValue()
    {
        return Search;
    }

    /// <summary>
    /// Actualiza el total de prompts disponibles en la base de datos, considerando el filtro si se proporciona.
    /// </summary>
    /// <param name="filter">
    /// Texto opcional para filtrar los prompts.
    /// </param>
    public async Task UpdateTotalPromptsCountAsync(string filter = null)
    {
        // Obtener el total de prompts de la base de datos, con o sin filtro
        blockHandler.CountInDB = string.IsNullOrEmpty(filter) ? await _databaseService.GetTotalPromptsCountAsync() : await _databaseService.GetTotalPromptsCountAsync(filter);
    }

    /// <summary>
    /// Verifica si hay más datos disponibles para cargar y actualiza la propiedad correspondiente.
    /// </summary>
    public async Task CheckForMorePromptsAsync(string filter = null)
    {
        await UpdateTotalPromptsCountAsync(filter);

        // Asignar directamente si hay más datos disponibles
        IsMoreDataAvailable = blockHandler.HasMoreData();
    }

    /// <summary>
    /// Carga más prompts o aplica el filtro de búsqueda si se especifica.
    /// </summary>
    [RelayCommand]
    public async Task LoadMorePrompts()
    {
        await (string.IsNullOrWhiteSpace(Search) ? LoadPromptsAsync() : FilterPromptsAsync());
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
            int batchSize = Math.Min(BlockHandler<PromptTemplateViewModel>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize);

            if (promptList.Any())
            {
                // Agregar los nuevos prompts y ordenar la colección
                Prompts.AddRange(promptList.ToViewModelObservableCollection(this._databaseService, TogglePromptSelection, DeletePromptAsync, NavigateTo));

                Prompts = Prompts.OrderBy(p => p.Prompt.Title).ToObservableCollection();

                // Actualizar los datos en el BlockHandler y avanzar al siguiente bloque
                blockHandler.Data = Prompts;

                blockHandler.NextBlock();
            }

            // Verificar si hay más datos por cargar
            await CheckForMorePromptsAsync();
        }, AppMessagesEng.Prompts.PromptLoadError);
    }
   

    // ======================= 🔍 FILTRAR PROMPTS =======================
    [RelayCommand]
    private async Task FilterPromptsAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (string.IsNullOrEmpty(this.Search))
            {
                await AppShell.Current.DisplayAlert("Error", AppMessagesEng.Warnings.EmptySearch, "OK");

                return;
            }

            if (!IsSearchFlag)
            {
                if (string.IsNullOrEmpty(this.oldSearch))
                {
                    this.oldSearch = this.Search;
                }

                ToggleSearchFlag(true);

                blockHandler.Reset();

                this.Prompts.Clear();
            }

            if (this.oldSearch != this.Search)
            {
                ToggleSearchFlag(true);

                blockHandler.Reset();

                this.Prompts.Clear();
            }

            //this.oldSearch = this.Search;

            await UpdateTotalPromptsCountAsync(this.Search);

            // Calcular el desplazamiento y ajustarlo para evitar duplicados
            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);

            // Calcular el tamaño del lote sin exceder los datos disponibles
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize, this.Search);

            if (promptList.Any())
            {
                // Agregar los nuevos prompts y ordenar la colección
                Prompts.AddRange(promptList.ToViewModelObservableCollection(this._databaseService, TogglePromptSelection, DeletePromptAsync, NavigateTo));

                Prompts = Prompts.OrderBy(p => p.Prompt.Title).ToObservableCollection();

                // Actualizar los datos en el BlockHandler y avanzar al siguiente bloque
                blockHandler.Data = Prompts;

                blockHandler.NextBlock();
            }

            // Verificar si hay más datos por cargar
            await CheckForMorePromptsAsync(this.Search);
        }, AppMessagesEng.Prompts.PromptLoadError);
    }

    // ======================= 🔄 REFRESCAR PROMPTS =======================
    [RelayCommand]
    private async Task RefreshPrompts()
    {
        await LoadInitialPrompts();
    }

    // ======================= ❌ ELIMINAR UN PROMPT =======================
    public override async void DeletePromptAsync(PromptTemplateViewModel selectedPrompt)
    {
        if (selectedPrompt == null) return;

        bool confirm = await AppShell.Current.DisplayAlert(
            "Confirm Deletion",
            $"Are you sure you want to delete the prompt \"{selectedPrompt.Prompt.Title}\"?",
            "Delete", "Cancel");

        if (confirm)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await _databaseService.DeletePromptAsync(selectedPrompt.Prompt.Id);

                Prompts.Remove(selectedPrompt);

                await CheckForMorePromptsAsync();

                //await AppShell.Current.DisplayAlert("Success", $"The prompt {selectedPrompt.Prompt.Title} has been deleted.", "OK");

                await Task.Delay(2000);

                await GenericToolBox.ShowLottieMessageAsync("RemoveComplete1.json", $"The prompt {selectedPrompt.Prompt.Title} has been deleted.");
            }, AppMessagesEng.Prompts.PromptDeleteError);
        }
    }

    // ======================= 📌 Comando para eliminar prompts seleccionados =======================
    [RelayCommand]
    public async Task DeleteSelectedPromptsAsync()
    {
        if (!SelectedPromptsToDelete.Any())
        {
            await AppShell.Current.DisplayAlert("Notification", "No items selected for deletion.", "OK");

            return;
        }

        bool confirm = await AppShell.Current.DisplayAlert(
            "Confirm Deletion",
            $"Are you sure you want to delete {SelectedPromptsToDelete.Count} selected items?",
            "Delete", "Cancel");

        if (confirm)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                foreach (var prompt in SelectedPromptsToDelete.ToList())
                {
                    await _databaseService.DeletePromptAsync(prompt.Prompt.Id);

                    Prompts.Remove(prompt);

                    SelectedPromptsToDelete.Remove(prompt);  // Asegurarse de limpiar la lista seleccionada
                }

                // Actualizar el BlockHandler y verificar si hay más datos
                blockHandler.Data = Prompts;

                await CheckForMorePromptsAsync();

                // Verificar si hay más datos disponibles
                if (IsMoreDataAvailable)
                {
                    //blockHandler.NextBlock();

                    await LoadInitialPrompts();
                }

                await Task.Delay(2000);

                await GenericToolBox.ShowLottieMessageAsync("RemoveComplete1.json", AppMessagesEng.Prompts.PromptsDeletedSuccess);

            }, AppMessagesEng.Prompts.PromptDeleteError);
        }
    }
}