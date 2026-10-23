using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.FinalPrompts.UseCases;
using QuickPrompt.Constants;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels
{
    /// <summary>
    /// ViewModel for AI Engine Launcher page.
    /// Refactored to use Use Cases - Phase 1 (Complete).
    /// ✅ UX IMPROVEMENTS: Added filter functionality for Recent Prompts.
    /// </summary>
    public partial class AiLauncherViewModel : BaseViewModel
    {
        // 🆕 Use Cases (injected)
        private readonly GetAllFinalPromptsUseCase _getAllFinalPromptsUseCase;
        private readonly DeleteFinalPromptUseCase _deleteFinalPromptUseCase;
        private readonly ClearAllFinalPromptsUseCase _clearAllFinalPromptsUseCase;
        private readonly IFinalPromptRepository _finalPromptRepository;
        private readonly IDialogService _dialogService;

        // ✅ UX IMPROVEMENTS: Store all prompts for filtering
        private List<string> _allPrompts = new();

        // Properties
        [ObservableProperty] private string selectedPrompt = string.Empty;
        [ObservableProperty] public string selectedCategory = string.Empty;
        [ObservableProperty] private string selectedEngine = string.Empty;
        
        // ✅ UX IMPROVEMENTS: Filter properties
        [ObservableProperty] private string selectedFilter = "All";

        public ObservableCollection<string> FilterOptions { get; } = new()
        {
            "All",
            "Last 5",
            "Last 10",
            "Last 20"
        };

        // Constructor with dependency injection
        public AiLauncherViewModel(
            GetAllFinalPromptsUseCase getAllFinalPromptsUseCase,
            DeleteFinalPromptUseCase deleteFinalPromptUseCase,
            ClearAllFinalPromptsUseCase clearAllFinalPromptsUseCase,
            IFinalPromptRepository finalPromptRepository,
            IDialogService dialogService)
        {
            _getAllFinalPromptsUseCase = getAllFinalPromptsUseCase ?? throw new ArgumentNullException(nameof(getAllFinalPromptsUseCase));
            _deleteFinalPromptUseCase = deleteFinalPromptUseCase ?? throw new ArgumentNullException(nameof(deleteFinalPromptUseCase));
            _clearAllFinalPromptsUseCase = clearAllFinalPromptsUseCase ?? throw new ArgumentNullException(nameof(clearAllFinalPromptsUseCase));
            _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        // ============================ LOAD PROMPTS ============================

        public async Task LoadFinalPrompts()
        {
            IsLoading = true;
            try
            {
                var result = await _getAllFinalPromptsUseCase.ExecuteAsync();

                if (result.IsFailure)
                {
                    await _dialogService.ShowErrorAsync(result.Error);
                    return;
                }

                var prompts = result.Value;

                if (prompts is null || !prompts.Any())
                    return;

                var distinctPrompts = prompts
                    .DistinctBy(v => v.CompletedText)
                    .Select(s => s.CompletedText)
                    .OrderByDescending(p => p)
                    .ToList();

                // ✅ Store all prompts for filtering
                _allPrompts = distinctPrompts;

                // Apply current filter
                ApplyFilter();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error loading prompts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ✅ UX IMPROVEMENTS: Handle filter changes
        partial void OnSelectedFilterChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_allPrompts == null || !_allPrompts.Any())
                return;

            var filteredPrompts = SelectedFilter switch
            {
                "Last 5" => _allPrompts.Take(5).ToList(),
                "Last 10" => _allPrompts.Take(10).ToList(),
                "Last 20" => _allPrompts.Take(20).ToList(),
                _ => _allPrompts.ToList() // "All"
            };

            FinalPrompts.Clear();
            foreach (var prompt in filteredPrompts)
            {
                FinalPrompts.Add(prompt);
            }
        }

        // ============================ LAUNCH ENGINE ============================

        [RelayCommand]
        private async Task LaunchEngine(string engineName)
        {
            if (string.IsNullOrWhiteSpace(engineName))
            {
                await _dialogService.ShowErrorAsync("Invalid AI engine selected.");
                return;
            }

            IsLoading = true;
            try
            {
                SelectedEngine = engineName;

                var promptToSend = string.IsNullOrWhiteSpace(SelectedPrompt)
                    ? string.Empty
                    : SelectedPrompt;

                var toast = Toast.Make($"Launching {engineName}...", ToastDuration.Short);
                await toast.Show();

                await NavigateToAsync(NavigationRoutes.EngineWebView, new Dictionary<string, object>
                {
                    { NavigationParameters.EngineName, engineName },
                    { NavigationParameters.Prompt, promptToSend }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error launching engine: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ SELECT PROMPT ============================

        [RelayCommand]
        private async Task SelectPrompt(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return;

            SelectedPrompt = promptText;

            var action = await _dialogService.ShowActionSheetAsync(
                "Choose AI Engine",
                "Cancel",
                null,
                "ChatGPT",
                "Gemini",
                "Grok",
                "Copilot"
            );

            if (action != null && action != "Cancel")
            {
                await LaunchEngine(action);
            }
        }

        // ============================ CLEAR PROMPTS ============================

        [RelayCommand]
        public async Task Clear()
        {
            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Confirm",
                "Are you sure you want to clear all recent prompts?",
                "Yes",
                "No"
            );

            if (!confirm)
                return;

            IsLoading = true;
            try
            {
                var result = await _clearAllFinalPromptsUseCase.ExecuteAsync();

                if (result.IsFailure)
                {
                    await _dialogService.ShowErrorAsync(result.Error);
                    return;
                }

                _allPrompts.Clear();
                FinalPrompts.Clear();
                SelectedPrompt = string.Empty;
                SelectedCategory = string.Empty;

                var toast = Toast.Make($"{result.Value} prompts cleared", ToastDuration.Short);
                await toast.Show();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error clearing prompts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ FILTER BY CATEGORY ============================

        async partial void OnSelectedCategoryChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            IsLoading = true;
            try
            {
                if (Enum.TryParse<PromptCategory>(value, out var parsedCategory))
                {
                    var prompts = await _finalPromptRepository.GetFinalPromptsByCategoryAsync(parsedCategory);

                    _allPrompts = prompts.Select(p => p.CompletedText).ToList();
                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error filtering prompts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ DELETE SINGLE PROMPT ============================

        [RelayCommand]
        private async Task DeleteFinalPrompt(string promptText)
        {
            IsLoading = true;
            try
            {
                var prompt = await _finalPromptRepository.FindByCompletedTextAsync(promptText);

                if (prompt is not null)
                {
                    var result = await _deleteFinalPromptUseCase.ExecuteAsync(prompt.Id);

                    if (result.IsFailure)
                    {
                        await _dialogService.ShowErrorAsync(result.Error);
                        return;
                    }

                    // ✅ Remove from both collections
                    _allPrompts.Remove(promptText);
                    FinalPrompts.Remove(promptText);

                    var toast = Toast.Make("Prompt removed", ToastDuration.Short);
                    await toast.Show();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error deleting prompt: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================ NAVIGATION ============================

        [RelayCommand]
        private async Task GoToQuickPrompts()
        {
            await Shell.Current.GoToAsync("//Quick");
        }

        [RelayCommand]
        private async Task GoToCreate()
        {
            await Shell.Current.GoToAsync("//Create");
        }
    }
}