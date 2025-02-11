using CommunityToolkit.Mvvm.ComponentModel;
using QuickPrompt.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels.Prompts
{
    public partial class PromptTemplateViewModel : ObservableObject
    {
        public PromptTemplate Prompt { get; }

        [ObservableProperty]
        private bool isSelected;

        public PromptTemplateViewModel(PromptTemplate prompt)
        {
            Prompt = prompt;
        }
    }
}
