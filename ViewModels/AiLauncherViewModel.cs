using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Constants;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;

namespace QuickPrompt.ViewModels
{
    /// <summary>
    /// ViewModel for AI Engine Launcher page.
    /// Refactored to use Use Cases and services - Phase 1.
    /// </summary>
    public partial class AiLauncherViewModel : BaseViewModel
    {
        // 🆕 Services (injected)
        private readonly IFinalPromptRepository _finalPromptRepository;
        private readonly IDialogService _dialogService;

        // Properties
        [ObservableProperty] private string selectedPrompt = string.Empty;
        [ObservableProperty] public string selectedCategory = string.Empty;
        [ObservableProperty] private string selectedEngine = string.Empty;

        // Constructor with dependency injection
        public AiLauncherViewModel(
            IFinalPromptRepository finalPromptRepository,
            IDialogService dialogService)
        {
            _finalPromptRepository = finalPromptRepository ?? throw new ArgumentNullException(nameof(finalPromptRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        // ============================ LOAD PROMPTS ============================

        public async Task LoadFinalPrompts()
        {
            IsLoading = true;
            try
            {
                var prompts = await _finalPromptRepository.GetAllAsync();

                if (prompts is null || !prompts.Any())
                    return;

                var newPrompts = prompts
                    .DistinctBy(v => v.CompletedText)
                    .Select(s => s.CompletedText)
                    .OrderByDescending(p => p)
                    .Take(10)
                    .ToList();

                FinalPrompts.Clear();

                if (newPrompts.Any())
                {
                    foreach (var prompt in newPrompts)
                    {
                        FinalPrompts.Add(prompt);
                    }
                }
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
                var prompts = await _finalPromptRepository.GetAllAsync();

                if (prompts != null && prompts.Any())
                {
                    foreach (var prompt in prompts)
                    {
                        await _finalPromptRepository.DeleteAsync(prompt.Id);
                    }
                }

                FinalPrompts.Clear();
                SelectedPrompt = string.Empty;
                SelectedCategory = string.Empty;

                var toast = Toast.Make("All prompts cleared", ToastDuration.Short);
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

                    FinalPrompts.Clear();

                    foreach (var prompt in prompts)
                    {
                        FinalPrompts.Add(prompt.CompletedText);
                    }
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
                    await _finalPromptRepository.DeleteAsync(prompt.Id);
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