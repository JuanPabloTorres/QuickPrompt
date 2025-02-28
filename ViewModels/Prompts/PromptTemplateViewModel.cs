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
        public Action<PromptTemplateViewModel> onSelectToDelete;

        public Action<PromptTemplateViewModel> onItemToDelete;

        [ObservableProperty]
        private PromptTemplate prompt;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isFavorite;

        private readonly PromptDatabaseService promptDatabaseService;

        public PromptTemplateViewModel(
            PromptTemplate prompt,
            PromptDatabaseService promptDatabaseService,
            Action<PromptTemplateViewModel> onSelectToDelete,
            Action<PromptTemplateViewModel> onItemToDelete)
        {
            Prompt = prompt;

            this.promptDatabaseService = promptDatabaseService;

            this.IsFavorite = prompt.IsFavorite;

            this.onSelectToDelete = onSelectToDelete;

            this.onItemToDelete = onItemToDelete;
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
                var _response = await this.promptDatabaseService.UpdateFavoriteStatusAsync(this.Prompt.Id, this.Prompt.IsFavorite);

                if (_response)
                {
                    IsFavorite = !IsFavorite;

                    this.Prompt.IsFavorite = IsFavorite;
                }
                else
                {
                    await AppShell.Current.DisplayAlert("Error", AppMessagesEng.GenericError, "OK");
                }
            }, AppMessagesEng.DatabaseUpdateError);
        }

        // ======================= 📌 Método para alternar selección =======================
         partial void OnIsSelectedChanged(bool isSelected)
        {
            onSelectToDelete?.Invoke(this);
        }

        // ======================= 📌 Método para borrar el item seleccionado =======================

        [RelayCommand]
        private void ItemToDelete()
        {
            onItemToDelete.Invoke(this);
        }
    }
}