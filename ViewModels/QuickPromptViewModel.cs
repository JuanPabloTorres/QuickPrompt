using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuickPrompt.Extensions;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using QuickPrompt.Tools.Messages;
using QuickPrompt.ViewModels.Prompts;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels
{
    public partial class QuickPromptViewModel : BaseViewModel
    {
        // ======================= 📌 PROPIEDADES =======================
        private readonly IPromptRepository _databaseService;

        public ObservableCollection<PromptTemplateViewModel> SelectedPromptsToDelete { get; set; } = new();   // Lista de prompts seleccionados para eliminar

        [ObservableProperty] private ObservableCollection<PromptTemplateViewModel> prompts = new();  // Inicializamos la lista vacía

        [ObservableProperty] private string? search;

        [ObservableProperty] private bool isMoreDataAvailable = true;  // Para indicar si hay más datos disponibles

        public BlockHandler<PromptTemplateViewModel> blockHandler = new();
        public bool IsSearchFlag { get; set; }

        [ObservableProperty] public string selectedCategory = string.Empty;

        // Constructor primario con la lógica de inicialización
        public QuickPromptViewModel(IPromptRepository _databaseService)
        {
            this._databaseService = _databaseService;

            if (!WeakReferenceMessenger.Default.IsRegistered<UpdatedPromptMessage>(this))
            {
                WeakReferenceMessenger.Default.Register<UpdatedPromptMessage>(this, async (recipient, message) =>
                {
                    if ((SelectedCategory != message.Value.Category.ToString()) && !string.IsNullOrEmpty(SelectedCategory))
                    {
                        var _prompToRemoveFromList = prompts.FirstOrDefault(v => v.Prompt.Id == message.Value.Id);

                        if (prompts.Any() && prompts.Any(v => v.Prompt.Id == _prompToRemoveFromList.Prompt.Id))
                        {
                            prompts.Remove(_prompToRemoveFromList);
                        }
                    }
                    else
                    {
                        if (prompts.Any() && prompts.Any(v => v.Prompt.Id == message.Value.Id))
                        {
                            var _currentCategory =prompts.FirstOrDefault(v => v.Prompt.Id == message.Value.Id).Prompt.Category;

                            if (_currentCategory != message.Value.Category)
                            {
                                prompts.FirstOrDefault(v => v.Prompt.Id == message.Value.Id).Prompt.Category = message.Value.Category;
                            }
                        }
                    }
                });
            }
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

            Search = string.Empty;

            SelectedCategory = string.Empty;

            SelectedDateFilter = Filters.All;

            // Actualizar el total de prompts y cargar el primer bloque
            await LoadPromptsAsync();
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

        public Filters GetFilterValue()
        {
            return selectedDateFilter;
        }

        /// <summary>
        /// Actualiza el total de prompts disponibles en la base de datos, considerando el filtro si
        /// se proporciona.
        /// </summary>
        public async Task UpdateTotalPromptsCountAsync(Filters dateFilter = Filters.All, string filter = null, string category = null)
        {
            // Obtener el total de prompts de la base de datos, con o sin filtro
            blockHandler.CountInDB = await _databaseService.GetTotalPromptsCountAsync(filter, dateFilter, category);
        }

        /// <summary>
        /// Verifica si hay más datos disponibles para cargar y actualiza la propiedad correspondiente.
        /// </summary>
        public async Task CheckForMorePromptsAsync(Filters dateFilter = Filters.All, string filter = null, string category = null)
        {
            await UpdateTotalPromptsCountAsync(dateFilter, filter, category);

            // Asignar directamente si hay más datos disponibles
            IsMoreDataAvailable = blockHandler.HasMoreData();
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

        /// <summary>
        /// Carga más prompts o aplica el filtro de búsqueda si se especifica.
        /// </summary>
        [RelayCommand]
        public async Task LoadMorePrompts()
        {
            // Validación: al menos un criterio debe estar presente
            bool isSearchEmpty = string.IsNullOrWhiteSpace(Search);

            bool isFilterEmpty = SelectedDateFilter == Filters.None;

            bool isSelectedCategoryEmpty = string.IsNullOrEmpty(SelectedCategory);

            if (isSearchEmpty || isFilterEmpty || isSelectedCategoryEmpty)
            {
                await LoadPromptsAsync();
            }
            else
            {
                await FilterPromptsAsync();
            }
        }

        /// <summary>
        /// Carga el próximo bloque de prompts desde la base de datos y actualiza la lista.
        /// </summary>
        public async Task LoadPromptsAsync()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await BlockDataHandler();
            }, AppMessagesEng.Prompts.PromptLoadError);
        }

        // ======================= 🔍 FILTRAR PROMPTS =======================
        [RelayCommand]
        private async Task FilterPromptsAsync()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                // Verificar si cambió la búsqueda o el filtro
                bool searchChanged = !string.Equals(oldSearch, Search, StringComparison.OrdinalIgnoreCase);

                bool filterChanged = oldDateFilter != SelectedDateFilter;

                bool categoryChange = oldSelectCategory != SelectedCategory;

                // Reiniciar búsqueda si es necesario
                if (!IsSearchFlag || searchChanged || filterChanged || categoryChange)
                {
                    ToggleSearchFlag(true);

                    blockHandler.Reset();

                    Prompts.Clear();

                    oldSearch = Search;

                    oldDateFilter = SelectedDateFilter;

                    oldSelectCategory = Enum.TryParse<PromptCategory>(SelectedCategory?.ToString(), out var category) ? category.ToString() : PromptCategory.General.ToString();
                }

                await BlockDataHandler();
            }, AppMessagesEng.Prompts.PromptLoadError);
        }

        private async Task BlockDataHandler()
        {
            // Actualizar conteo total según los filtros actuales
            await UpdateTotalPromptsCountAsync(SelectedDateFilter, Search, SelectedCategory);

            // Calcular el desplazamiento y ajustarlo para evitar duplicados
            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);

            // Calcular el tamaño del lote sin exceder los datos disponibles
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            // Cargar el bloque de prompts
            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize, SelectedDateFilter, this.Search, SelectedCategory);

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
            await CheckForMorePromptsAsync(this.SelectedDateFilter, this.Search, SelectedCategory);
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

                    // Verificar si hay más datos por cargar
                    await CheckForMorePromptsAsync(this.SelectedDateFilter, this.Search, SelectedCategory);

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

                   

                    // Verificar si hay más datos por cargar
                    await CheckForMorePromptsAsync(this.SelectedDateFilter, this.Search, SelectedCategory);

                    // Verificar si hay más datos disponibles
                    if (IsMoreDataAvailable)
                    {
                        //blockHandler.NextBlock();

                        await LoadInitialPrompts();
                    }

                    await GenericToolBox.ShowLottieMessageAsync("RemoveComplete1.json", AppMessagesEng.Prompts.PromptsDeletedSuccess);

                    this.IsAllSelected = false;

                }, AppMessagesEng.Prompts.PromptDeleteError);
            }
        }

        // ======================= 📌 Lógica de selección =======================

        /// <summary>
        /// Alterna el estado de favorito de un prompt.
        /// </summary>
        [RelayCommand]
        private async Task ToggleFavorite(PromptTemplateViewModel prompt)
        {
            if (prompt == null) return;

            prompt.IsFavorite = !prompt.IsFavorite;

            await _databaseService.UpdateFavoriteStatusAsync(prompt.Prompt.Id, prompt.IsFavorite);

            if (!prompt.IsFavorite && SelectedDateFilter == Filters.Favorites)
            {
                Prompts.Remove(prompt);
            }

            if (prompt.IsFavorite && SelectedDateFilter == Filters.NonFavorites)
            {
                Prompts.Remove(prompt);
            }
        }

        [RelayCommand]
        private void SelectFilter(Filters filter)
        {
            SelectedDateFilter = filter;

            FilterPromptsCommand.Execute(null);
        }
    }
}