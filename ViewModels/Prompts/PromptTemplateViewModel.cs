using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Pages;
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

        public Action<PromptTemplateViewModel> onRemoveFavorite;

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

        public PromptTemplateViewModel(
          PromptTemplate prompt,
          PromptDatabaseService promptDatabaseService,
          Action<PromptTemplateViewModel> onSelectToDelete,
          Action<PromptTemplateViewModel> onItemToDelete, Action<PromptTemplateViewModel> onRemoveFavorite)
        {
            Prompt = prompt;

            this.promptDatabaseService = promptDatabaseService;

            this.IsFavorite = prompt.IsFavorite;

            this.onSelectToDelete = onSelectToDelete;

            this.onItemToDelete = onItemToDelete;

            this.onRemoveFavorite = onRemoveFavorite;
        }

        public PromptTemplateViewModel(PromptTemplate prompt)
        {
        }

        // ======================= 📌 Comando para agregar a favorito =======================
        [RelayCommand]
        private async Task ToFavoriteOrNot()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                // Actualizar en la base de datos
                var _response = await this.promptDatabaseService.UpdateFavoriteStatusAsync(this.Prompt.Id, !this.Prompt.IsFavorite);

                if (!_response)
                {
                    await AppShell.Current.DisplayAlert("Error", AppMessagesEng.GenericError, "OK");
                }

                IsFavorite = !IsFavorite;

                this.Prompt.IsFavorite = IsFavorite;
            }, AppMessagesEng.DatabaseUpdateError);
        }

        // ======================= 📌 Método para alternar selección =======================
         partial void OnIsSelectedChanged(bool value)
        {
            onSelectToDelete?.Invoke(this);
        }

        // ======================= 📌 Método para borrar el item seleccionado =======================

        [RelayCommand]
        private void ItemToDelete()
        {
            onItemToDelete.Invoke(this);
        }

        // ======================= ✏️ EDITAR UN PROMPT =======================
        [RelayCommand]
        private async Task NavigateToEditPrompt(PromptTemplate selectedPrompt)
        {
            if (selectedPrompt != null)
            {
                await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
            {
                { "selectedId", selectedPrompt.Id }
            });
            }
        }

        // ======================= 📌 SELECCIONAR UN PROMPT =======================
        [RelayCommand]
        private async Task SelectPrompt(PromptTemplate selectedPrompt)
        {
            if (selectedPrompt != null)
            {
                await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={selectedPrompt.Id}", true);
            }
        }
    }
}