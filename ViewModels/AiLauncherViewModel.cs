using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class AiLauncherViewModel : ObservableObject
    {
        [RelayCommand]
        private async Task SendPromptToChatGPT()
        {
            await Shell.Current.GoToAsync("ExternalAiPage?url=https://chat.openai.com/");
        }

        [RelayCommand]
        private async Task SendPromptToGemini()
        {
            await Shell.Current.GoToAsync("ExternalAiPage?url=https://gemini.google.com/");
        }

        [RelayCommand]
        private async Task SendPromptToGrok()
        {
            await Shell.Current.GoToAsync("ExternalAiPage?url=https://grok.com/");
        }

        [RelayCommand]
        private async Task SendPromptToCopilot()
        {
            await Shell.Current.GoToAsync("ExternalAiPage?url=https://copilot.microsoft.com/chats/Wt2qDSvnmnFtgZVr6RQRc/");
        }
    }
}
