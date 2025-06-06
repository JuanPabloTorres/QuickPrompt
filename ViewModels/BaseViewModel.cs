using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Extensions;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using QuickPrompt.ViewModels.Prompts;
using QuickPrompt.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public abstract partial class BaseViewModel : RootViewModel
    {
        // ============================== 🌟 PROPIEDADES ==============================

        [ObservableProperty] protected string search;

        [ObservableProperty] protected Filters selectedDateFilter = Filters.All;

        [ObservableProperty] protected bool isAllSelected = false;

        [ObservableProperty] private bool showPromptActions;

        [ObservableProperty] private bool isVisualModeActive;

        [ObservableProperty] protected ObservableCollection<string> categories = new ObservableCollection<string>(Enum.GetNames(typeof(PromptCategory)).ToObservableCollection());

        public ObservableCollection<string> FinalPrompts { get; set; } = new();

        protected string oldSearch;

        protected string oldSelectCategory;

        protected Filters oldDateFilter;

        protected PromptCategory promptCategory;

        protected IPromptRepository _databaseService;

        protected IFinalPromptRepository _finalPromptRepository;

        protected DatabaseServiceManager databaseServiceManager;

        protected BaseViewModel()
        {
        }

        protected BaseViewModel(AppSettings appSettings)
        {
        }

        protected BaseViewModel(DatabaseServiceManager dbManager)
        {
            databaseServiceManager = dbManager;
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService, IFinalPromptRepository finalPromptRepository)
        {
            this._databaseService = promptDatabaseService;

            this._finalPromptRepository = finalPromptRepository;
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService, AdmobService admobService) : base(admobService)
        {
            this._databaseService = promptDatabaseService;

            //this._adMobService = admobService;
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService, IFinalPromptRepository finalPromptRepository, AdmobService admobService) : base(admobService)
        {
            this._databaseService = promptDatabaseService;

            this._finalPromptRepository = finalPromptRepository;

            //this._adMobService = admobService;
        }

        protected BaseViewModel(IFinalPromptRepository finalPromptRepository)
        {
            this._finalPromptRepository = finalPromptRepository;
        }

        public IFinalPromptRepository GetService()
        {
            return _finalPromptRepository;
        }

        // ============================== 🛠 MÉTODOS AUXILIARES ==============================

        /// <summary>
        /// Valida si la selección de texto es válida.
        /// </summary>
        /// <param name="promptText">
        /// Texto del prompt.
        /// </param>
        /// <param name="selectionLength">
        /// Longitud de la selección.
        /// </param>
        /// <returns>
        /// Verdadero si la selección es válida.
        /// </returns>
        protected bool IsSelectionValid(string promptText, int selectionLength) => !string.IsNullOrEmpty(promptText) && selectionLength > 0;

        /// <summary>
        /// Alterna la selección de un prompt en la lista de eliminación. Si el prompt ya está
        /// seleccionado, lo elimina; de lo contrario, lo agrega.
        /// </summary>
        /// <param name="prompt">
        /// El prompt a seleccionar o deseleccionar.
        /// </param>
        public virtual void TogglePromptSelection(PromptTemplateViewModel prompt)
        {
        }

        // ======================= ❌ ELIMINAR UN PROMPT =======================
        public virtual void DeletePromptAsync(PromptTemplateViewModel selectedPrompt)
        {
        }

        protected async Task SendPromptToAsync(string pageName, string toastMessage, Guid promptID, string finalPrompt)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(finalPrompt))
                {
                    await AlertService.ShowAlert("Error", "No prompt generated.");

                    return;
                }

                var toast = Toast.Make($"Opening {toastMessage}...", ToastDuration.Short);

                await toast.Show();

                await NavigateToAsync(pageName, new Dictionary<string, object>
        {
            { "TemplateId", promptID },
            { "FinalPrompt", finalPrompt }
        });
            });
        }
    }
}