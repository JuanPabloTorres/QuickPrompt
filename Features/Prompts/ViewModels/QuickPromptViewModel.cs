using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Extensions;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using QuickPrompt.Tools.Messages;
using QuickPrompt.ViewModels.Prompts;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels
{
    /// <summary>
    /// ViewModel for the Quick Prompt page (main list).
    /// Refactored to use Use Cases and services - Phase 1.
    /// ✅ PHASE 2: IDisposable implemented to fix WeakReferenceMessenger leak
    /// </summary>
    public partial class QuickPromptViewModel : BaseViewModel, IDisposable
    {
        // 🆕 Use Cases and Services (injected)
        private readonly Services.ServiceInterfaces.IPromptRepository _databaseService;
        private readonly DeletePromptUseCase _deletePromptUseCase;
        private readonly IDialogService _dialogService;

        // ✅ PHASE 2: Disposal tracking
        private bool _disposed = false;

        // Properties
        public ObservableCollection<PromptTemplateViewModel> SelectedPromptsToDelete { get; set; } = new();
        [ObservableProperty] private ObservableCollection<PromptTemplateViewModel> prompts = new();
        [ObservableProperty] private string? search;
        [ObservableProperty] private bool isMoreDataAvailable = true;
        [ObservableProperty] public string selectedCategory = string.Empty;

        public BlockHandler<PromptTemplateViewModel> blockHandler = new();
        public bool IsSearchFlag { get; set; }
        private string oldSearch = string.Empty;
        private string oldSelectCategory = string.Empty;
        private Filters oldDateFilter = Filters.All;

        public QuickPromptViewModel(
            Services.ServiceInterfaces.IPromptRepository databaseService,
            DeletePromptUseCase deletePromptUseCase,
            IDialogService dialogService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _deletePromptUseCase = deletePromptUseCase ?? throw new ArgumentNullException(nameof(deletePromptUseCase));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            RegisterMessenger();
        }

        private void RegisterMessenger()
        {
            if (!WeakReferenceMessenger.Default.IsRegistered<UpdatedPromptMessage>(this))
            {
                WeakReferenceMessenger.Default.Register<UpdatedPromptMessage>(this, async (recipient, message) =>
                {
                    if ((SelectedCategory != message.Value.Category.ToString()) && !string.IsNullOrEmpty(SelectedCategory))
                    {
                        var promptToRemove = prompts.FirstOrDefault(v => v.Prompt.Id == message.Value.Id);
                        if (prompts.Any() && promptToRemove != null)
                        {
                            prompts.Remove(promptToRemove);
                        }
                    }
                    else
                    {
                        var existingPrompt = prompts.FirstOrDefault(v => v.Prompt.Id == message.Value.Id);
                        if (existingPrompt != null && existingPrompt.Prompt.Category != message.Value.Category)
                        {
                            existingPrompt.Prompt.Category = message.Value.Category;
                        }
                    }
                });
            }
        }

        // ============================ LOAD PROMPTS ============================

        [RelayCommand]
        public async Task LoadInitialPrompts()
        {
            ToggleSearchFlag(false);
            IsAllSelected = false;
            blockHandler.Reset();
            Prompts.Clear();
            SelectedPromptsToDelete.Clear();
            Search = string.Empty;
            SelectedCategory = string.Empty;
            SelectedDateFilter = Filters.All;

            await LoadPromptsAsync();
        }

        public void ToggleSearchFlag(bool onOrOff) => IsSearchFlag = onOrOff;

        public string GetSearchValue() => Search ?? string.Empty;

        public Filters GetFilterValue() => selectedDateFilter;

        public async Task UpdateTotalPromptsCountAsync(Filters dateFilter = Filters.All, string? filter = null, string? category = null)
        {
            blockHandler.CountInDB = await _databaseService.GetTotalPromptsCountAsync(filter, dateFilter, category);
        }

        public async Task CheckForMorePromptsAsync(Filters dateFilter = Filters.All, string? filter = null, string? category = null)
        {
            await UpdateTotalPromptsCountAsync(dateFilter, filter, category);
            IsMoreDataAvailable = blockHandler.HasMoreData();
        }

        public override void TogglePromptSelection(PromptTemplateViewModel prompt)
        {
            if (SelectedPromptsToDelete.Contains(prompt))
                SelectedPromptsToDelete.Remove(prompt);
            else
                SelectedPromptsToDelete.Add(prompt);
        }

        [RelayCommand]
        public async Task LoadMorePrompts()
        {
            bool isSearchEmpty = string.IsNullOrWhiteSpace(Search);
            bool isFilterEmpty = SelectedDateFilter == Filters.None;
            bool isSelectedCategoryEmpty = string.IsNullOrEmpty(SelectedCategory);

            if (isSearchEmpty || isFilterEmpty || isSelectedCategoryEmpty)
                await LoadPromptsAsync();
            else
                await FilterPromptsAsync();
        }

        public async Task LoadPromptsAsync()
        {
            IsLoading = true;
            try
            {
                await BlockDataHandler();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptLoadError}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ FILTERING ============================

        [RelayCommand]
        private async Task FilterPromptsAsync()
        {
            IsLoading = true;
            try
            {
                bool searchChanged = !string.Equals(oldSearch, Search, StringComparison.OrdinalIgnoreCase);
                bool filterChanged = oldDateFilter != SelectedDateFilter;
                bool categoryChanged = oldSelectCategory != SelectedCategory;

                if (!IsSearchFlag || searchChanged || filterChanged || categoryChanged)
                {
                    ToggleSearchFlag(true);
                    blockHandler.Reset();
                    Prompts.Clear();
                    oldSearch = Search ?? string.Empty;
                    oldDateFilter = SelectedDateFilter;
                    oldSelectCategory = SelectedCategory ?? PromptCategory.General.ToString();
                }

                await BlockDataHandler();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptLoadError}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BlockDataHandler()
        {
            await UpdateTotalPromptsCountAsync(SelectedDateFilter, Search, SelectedCategory);

            int toSkip = blockHandler.AdjustToSkip(blockHandler.ToSkip(), Prompts.Count);
            int batchSize = Math.Min(BlockHandler<PromptTemplate>.SIZE, Math.Max(0, blockHandler.CountInDB - toSkip));

            var promptList = await _databaseService.GetPromptsByBlockAsync(toSkip, batchSize, SelectedDateFilter, Search, SelectedCategory);

            if (promptList.Any())
            {
                Prompts.AddRange(promptList.ToViewModelObservableCollection(_databaseService, TogglePromptSelection, DeletePromptAsync, NavigateTo));
                Prompts = Prompts.OrderBy(p => p.Prompt.Title).ToObservableCollection();
                blockHandler.Data = Prompts;
                blockHandler.NextBlock();
            }

            await CheckForMorePromptsAsync(SelectedDateFilter, Search, SelectedCategory);
        }

        [RelayCommand]
        private async Task RefreshPrompts() => await LoadInitialPrompts();

        // ============================ DELETE PROMPTS ============================

        public override async void DeletePromptAsync(PromptTemplateViewModel selectedPrompt)
        {
            if (selectedPrompt == null) return;

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Confirm Deletion",
                $"Are you sure you want to delete the prompt \"{selectedPrompt.Prompt.Title}\"?",
                "Delete",
                "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                var result = await _deletePromptUseCase.ExecuteAsync(selectedPrompt.Prompt.Id);

                if (result.IsFailure)
                {
                    await _dialogService.ShowErrorAsync(result.Error);
                    return;
                }

                Prompts.Remove(selectedPrompt);
                await CheckForMorePromptsAsync(SelectedDateFilter, Search, SelectedCategory);

                await _dialogService.ShowLottieMessageAsync(
                    "RemoveComplete1.json",
                    $"The prompt {selectedPrompt.Prompt.Title} has been deleted.");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptDeleteError}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task DeleteSelectedPromptsAsync()
        {
            if (!SelectedPromptsToDelete.Any())
            {
                await _dialogService.ShowAlertAsync("Notification", "No items selected for deletion.");
                return;
            }

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Confirm Deletion",
                $"Are you sure you want to delete {SelectedPromptsToDelete.Count} selected items?",
                "Delete",
                "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                foreach (var prompt in SelectedPromptsToDelete.ToList())
                {
                    var result = await _deletePromptUseCase.ExecuteAsync(prompt.Prompt.Id);
                    
                    if (result.IsSuccess)
                    {
                        Prompts.Remove(prompt);
                        SelectedPromptsToDelete.Remove(prompt);
                    }
                }

                blockHandler.Data = Prompts;
                await CheckForMorePromptsAsync(SelectedDateFilter, Search, SelectedCategory);

                if (IsMoreDataAvailable)
                {
                    await LoadInitialPrompts();
                }

                await _dialogService.ShowLottieMessageAsync(
                    "RemoveComplete1.json",
                    AppMessagesEng.Prompts.PromptsDeletedSuccess);

                IsAllSelected = false;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptDeleteError}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ FAVORITES ============================

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

        // ============================ PHASE 2: IDisposable Implementation ============================

        /// <summary>
        /// Disposes resources and unregisters from WeakReferenceMessenger to prevent memory leaks.
        /// Called automatically when ViewModel is no longer needed.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // ✅ Unregister from messenger to prevent memory leaks
                WeakReferenceMessenger.Default.Unregister<UpdatedPromptMessage>(this);

                // Clear collections to release references
                Prompts?.Clear();
                SelectedPromptsToDelete?.Clear();
                blockHandler?.Data?.Clear();
            }

            _disposed = true;
        }

        /// <summary>
        /// Destructor for safety (should not be called if Dispose is called properly)
        /// </summary>
        ~QuickPromptViewModel()
        {
            Dispose(disposing: false);
        }
    }
}