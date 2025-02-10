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

public partial class LoadPromptsPageViewModel : BaseViewModel
{
    // ======================= 📌 PROPIEDADES =======================
    [ObservableProperty]
    private ObservableCollection<PromptTemplate> selectedPromptsToDelete = new();  // Lista de prompts seleccionados para eliminar

    public BlockHandler<PromptTemplate> blockHandler = new BlockHandler<PromptTemplate>();

    [ObservableProperty]
    private ObservableCollection<PromptTemplate> prompts = new();  // Inicializamos la lista vacía

    [ObservableProperty]
    private string search;

    [ObservableProperty]
    private bool isMoreDataAvailable = true;  // Para indicar si hay más datos disponibles

    public bool isSerachFlagOn = false;

    public bool isReset = false;

    private readonly PromptDatabaseService _databaseService;

    // Constructor primario con la lógica de inicialización
    public LoadPromptsPageViewModel(PromptDatabaseService _databaseService)
    {
        this._databaseService = _databaseService;
        // Suscribirse al evento CollectionChanged una sola vez
        this.SelectedPromptsToDelete.CollectionChanged += SelectedPromptsToDelete_CollectionChanged;
    }

    // ======================= 📌 MÉTODO PRINCIPAL: Cargar Prompts =======================
    [RelayCommand]
    public async Task LoadInitialPrompts()
    {
        // Reiniciar la configuración inicial
        blockHandler.Reset();

        Prompts.Clear();

        // Reiniciar búsqueda
        Search = string.Empty;

        isSerachFlagOn = false;

        // Actualizar el total de prompts y cargar el primer bloque
        await LoadPromptsAsync();

        isReset = true;
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
        await (string.IsNullOrWhiteSpace(Search) ? LoadPromptsAsync() : FilterPromptsAsync(Search));
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
    private async Task FilterPromptsAsync(string filter)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (!isSerachFlagOn)
            {
                isSerachFlagOn = true;

                blockHandler.Reset();

                this.Prompts.Clear();
            }

            await UpdateTotalPromptsCountAsync(filter);

            // Calcular el desplazamiento y ajustarlo para evitar duplicados
            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);

            // Calcular el tamaño del lote sin exceder los datos disponibles
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize, filter);

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
            await CheckForMorePromptsAsync(filter);
        }, AppMessagesEng.Prompts.PromptLoadError);
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
        isReset = true;

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

    // ======================= 📌 Comando para eliminar prompts seleccionados =======================
    [RelayCommand]
    public async Task DeleteSelectedPromptsAsync()
    {
        if (!selectedPromptsToDelete.Any())
        {
            await AppShell.Current.DisplayAlert("Notification", "No items selected for deletion.", "OK");
            return;
        }

        bool confirm = await AppShell.Current.DisplayAlert(
            "Confirm Deletion",
            $"Are you sure you want to delete {selectedPromptsToDelete.Count} selected items?",
            "Delete", "Cancel");

        if (confirm)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                foreach (var prompt in selectedPromptsToDelete.ToList())
                {
                    await _databaseService.DeletePromptAsync(prompt);
                    Prompts.Remove(prompt);
                    selectedPromptsToDelete.Remove(prompt);  // Asegurarse de limpiar la lista seleccionada
                }

                // Actualizar el BlockHandler y verificar si hay más datos
                blockHandler.Data = Prompts;
                await CheckForMorePromptsAsync();

                await AppShell.Current.DisplayAlert("Success", "Selected prompts have been deleted.", "OK");
            }, AppMessages.Prompts.PromptDeleteError);
        }
    }

    // ======================= 📌 Lógica de selección =======================
    /// <summary>
    /// Agrega o quita el prompt de la lista de selección.
    /// </summary>
    /// <param name="prompt">
    /// El prompt a seleccionar o deseleccionar.
    /// </param>
    public void TogglePromptSelection(PromptTemplate prompt)
    {
        //this.SelectedPromptsToDelete.CollectionChanged += SelectedPromptsToDelete_CollectionChanged;

        if (selectedPromptsToDelete.Contains(prompt))
        {
            selectedPromptsToDelete.Remove(prompt);  // Si ya estaba seleccionado, lo deseleccionamos
        }
        else
        {
            selectedPromptsToDelete.Add(prompt);  // Si no estaba seleccionado, lo agregamos
        }
    }

    private void SelectedPromptsToDelete_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedPromptsToDelete));
    }
}