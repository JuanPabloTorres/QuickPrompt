using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
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
        public Action? ClearWebViewTextAction { get; set; }

        [ObservableProperty] public string selectedCategory;

        public AiLauncherViewModel(IFinalPromptRepository finalPromptRepository) : base(finalPromptRepository)
        {
        }

        public async Task LoadFinalPrompts()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                var prompts = await _finalPromptRepository.GetAllAsync();

                if (prompts is null || !prompts.Any())
                    return;

                var newPrompts = prompts.DistinctBy(v => v.CompletedText).Select(s => s.CompletedText).ToHashSet().ToList();

                if (newPrompts.Any())
                {
                    FinalPrompts.AddRange(newPrompts);
                }
            });
        }

        [RelayCommand]
        public   void Clear()
        {
            // 3. Ejecuta la limpieza del WebView si está asignado
            ClearWebViewTextAction?.Invoke();

            FinalPrompts.Clear();

            SelectedCategory = string.Empty;

         
        }

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
    }
}