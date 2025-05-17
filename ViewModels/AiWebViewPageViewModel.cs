using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class AiWebViewPageViewModel : BaseViewModel
    {
        [ObservableProperty] private string finalPrompt = string.Empty;

        [ObservableProperty] private Guid templateId;

        public override async Task MyBack()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await Shell.Current.GoToAsync(nameof(PromptDetailsPage), new Dictionary<string, object>
            {
                { "selectedId", templateId }
            });
            });
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("FinalPrompt", out var prompt))
                FinalPrompt = Uri.UnescapeDataString(prompt?.ToString() ?? "");

            if (query.TryGetValue("TemplateId", out var id) && Guid.TryParse(id?.ToString(), out var parsedId))
                TemplateId = parsedId;
        }
    }
}