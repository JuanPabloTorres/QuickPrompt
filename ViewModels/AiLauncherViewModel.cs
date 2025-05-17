using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Services.ServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class AiLauncherViewModel(IFinalPromptRepository finalPromptRepository) : BaseViewModel
    {
        public ObservableCollection<string> FinalPrompts { get; set; } = new();

        public async Task LoadFinalPrompts()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                var prompts = await finalPromptRepository.GetAllAsync();

                if (prompts is null || !prompts.Any())
                    return;

                var newPrompts = prompts.DistinctBy(v => v.CompletedText).Select(s => s.CompletedText).ToHashSet().ToList();

                if (newPrompts.Any())
                {
                    FinalPrompts.AddRange(newPrompts);
                }
            });
        }
    }
}