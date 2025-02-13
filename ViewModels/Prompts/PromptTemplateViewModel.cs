using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels.Prompts
{
    public partial class PromptTemplateViewModel : BaseViewModel
    {
        [ObservableProperty]
        private PromptTemplate prompt;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isFavorite;

        private readonly PromptDatabaseService promptDatabaseService;

        public PromptTemplateViewModel(PromptTemplate prompt, PromptDatabaseService promptDatabaseService)
        {
            Prompt = prompt;

            this.promptDatabaseService = promptDatabaseService;

            this.IsFavorite = prompt.IsFavorite;
        }

        public PromptTemplateViewModel(PromptTemplate prompt)
        {
        }

        // ======================= 📌 Comando para agregar a favorito =======================
        [RelayCommand]
        private async Task ToFavoriteOrNot()
        {
            IsFavorite = !IsFavorite;

            this.Prompt.IsFavorite = IsFavorite;

            await ExecuteWithLoadingAsync(async () =>
            {
                // Actualizar en la base de datos
                await this.promptDatabaseService.UpdateFavoriteStatusAsync(this.Prompt.Id, this.Prompt.IsFavorite);

                await AppShell.Current.DisplayAlert("Success", prompt.IsFavorite ? "Prompt added to favorites." : "Prompt removed from favorites.", "OK");
            }, AppMessagesEng.DatabaseUpdateError);
        }
    }
}