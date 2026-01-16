using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using QuickPrompt.Constants;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class AiLauncherViewModel : BaseViewModel
    {
        [ObservableProperty] 
        private string selectedPrompt = string.Empty;

        [ObservableProperty] 
        public string selectedCategory;

        [ObservableProperty]
        private string selectedEngine = string.Empty;

        public AiLauncherViewModel(IFinalPromptRepository finalPromptRepository) : base(finalPromptRepository)
        {
        }

        /// <summary>
        /// Load all final prompts from the database
        /// </summary>
        public async Task LoadFinalPrompts()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                var prompts = await _finalPromptRepository.GetAllAsync();

                if (prompts is null || !prompts.Any())
                    return;

                var newPrompts = prompts
                    .DistinctBy(v => v.CompletedText)
                    .Select(s => s.CompletedText)
                    .OrderByDescending(p => p) // Most recent first
                    .Take(10) // Limit to 10 most recent
                    .ToList();

                FinalPrompts.Clear();
                
                if (newPrompts.Any())
                {
                    FinalPrompts.AddRange(newPrompts);
                }
            });
        }

        /// <summary>
        /// Launch an AI engine with or without a prompt
        /// </summary>
        [RelayCommand]
        private async Task LaunchEngine(string engineName)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(engineName))
                {
                    await AlertService.ShowAlert("Error", "Invalid AI engine selected.");
                    return;
                }

                SelectedEngine = engineName;

                // If no prompt selected, launch with empty prompt (user can type in the engine)
                var promptToSend = string.IsNullOrWhiteSpace(SelectedPrompt) 
                    ? string.Empty 
                    : SelectedPrompt;

                var toast = Toast.Make($"Launching {engineName}...", ToastDuration.Short);
                await toast.Show();

                // Navigate to EngineWebViewPage
                await NavigateToAsync(NavigationRoutes.EngineWebView, new Dictionary<string, object>
                {
                    { NavigationParameters.EngineName, engineName },
                    { NavigationParameters.Prompt, promptToSend }
                });
            });
        }

        /// <summary>
        /// Select a prompt from the list and prompt user to choose an engine
        /// </summary>
        [RelayCommand]
        private async Task SelectPrompt(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return;

            SelectedPrompt = promptText;

            // Show action sheet to choose engine
            var action = await Shell.Current.DisplayActionSheet(
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

        /// <summary>
        /// Clear all prompts and reset selection
        /// </summary>
        [RelayCommand]
        public async Task Clear()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm",
                "Are you sure you want to clear all recent prompts?",
                "Yes",
                "No"
            );

            if (!confirm)
                return;

            await ExecuteWithLoadingAsync(async () =>
            {
                // Delete all final prompts from database
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
            });
        }

        /// <summary>
        /// Filter prompts by category
        /// </summary>
        async partial void OnSelectedCategoryChanged(string value)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (Enum.TryParse<PromptCategory>(value, out var parsedCategory))
                {
                    if (_finalPromptRepository is null)
                    {
                        return;
                    }

                    var prompts = await _finalPromptRepository.GetFinalPromptsByCategoryAsync(parsedCategory);

                    FinalPrompts.Clear();

                    foreach (var prompt in prompts)
                    {
                        FinalPrompts.Add(prompt.CompletedText);
                    }
                }
            });
        }

        /// <summary>
        /// Delete a single final prompt
        /// </summary>
        [RelayCommand]
        private async Task DeleteFinalPrompt(string promptText)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                var prompt = await _finalPromptRepository.FindByCompletedTextAsync(promptText);

                if (prompt is not null)
                {
                    await _finalPromptRepository.DeleteAsync(prompt.Id);

                    FinalPrompts.Remove(promptText);

                    var toast = Toast.Make($"Prompt removed", ToastDuration.Short);

                    await toast.Show();
                }
            });
        }

        /// <summary>
        /// Navigate to Quick Prompts tab
        /// </summary>
        [RelayCommand]
        private async Task GoToQuickPrompts()
        {
            await Shell.Current.GoToAsync("//Quick");
        }

        /// <summary>
        /// Navigate to Create tab
        /// </summary>
        [RelayCommand]
        private async Task GoToCreate()
        {
            await Shell.Current.GoToAsync("//Create");
        }
    }
}