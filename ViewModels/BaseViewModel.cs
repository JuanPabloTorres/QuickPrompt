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
    public abstract partial class BaseViewModel : ObservableObject
    {
        // ============================== 🌟 PROPIEDADES ==============================
        [ObservableProperty] public string emptyViewText = "No Prompts Available ";

        [ObservableProperty] protected string search;

        [ObservableProperty] protected Filters selectedDateFilter = Filters.All;

        [ObservableProperty] protected bool isAllSelected = false;

        [ObservableProperty] private string selectedTextLabelCount = $"{AppMessagesEng.TotalMessage} None";

        [ObservableProperty] private bool showPromptActions;

        [ObservableProperty] public bool isLoading;

        [ObservableProperty] private bool isVisualModeActive;

        [ObservableProperty] protected ObservableCollection<string> categories = new ObservableCollection<string>(Enum.GetNames(typeof(PromptCategory)).ToObservableCollection());

        [ObservableProperty] protected string selectedCategory;

        protected string oldSearch;

        protected string oldSelectCategory;

        protected Filters oldDateFilter;

        protected PromptCategory promptCategory;

        protected IPromptRepository _databaseService;

        protected IFinalPromptRepository _finalPromptRepository;

        protected AdmobService _adMobService;

        protected BaseViewModel()
        {
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService)
        {
            this._databaseService = promptDatabaseService;
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService, AdmobService admobService)
        {
            this._databaseService = promptDatabaseService;

            this._adMobService = admobService;
        }

        protected BaseViewModel(IPromptRepository promptDatabaseService,IFinalPromptRepository finalPromptRepository, AdmobService admobService)
        {
            this._databaseService = promptDatabaseService;

            this._finalPromptRepository = finalPromptRepository;

            this._adMobService = admobService;
        }

        protected BaseViewModel(AppSettings appSettings)
        {
        }

        // ============================== 🚀 MÉTODOS PRINCIPALES ==============================

        /// <summary>
        /// Ejecuta una tarea asíncrona con control de estado de carga y manejo de errores.
        /// </summary>
        /// <param name="action">
        /// Tarea a ejecutar.
        /// </param>
        /// <param name="errorMessage">
        /// Mensaje de error a mostrar en caso de fallo.
        /// </param>
        protected async Task ExecuteWithLoadingAsync(Func<Task> action, string errorMessage = "An error occurred. Please try again.")
        {
            try
            {
                IsLoading = true;

                await action();
            }
            catch (Exception ex)
            {
                await AppShell.Current.DisplayAlert("Error", errorMessage, "OK");
                // 🔹 Aquí puedes agregar un logging si es necesario
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RunWithLoaderAndErrorHandlingAsync(ReusableLoadingOverlay loader, Func<Task> action, string loadingMessage = "Cargando...", string errorMessage = "Ocurrió un error. Intenta nuevamente.")
        {
            try
            {
                loader.Message = loadingMessage;
                loader.IsVisible = true;

                await action(); // Ejecuta la tarea principal
            }
            catch (Exception ex)
            {
                // Muestra alerta de error
                await Shell.Current.DisplayAlert("Error", errorMessage, "OK");

                // Puedes loguear el error si deseas
                System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
            }
            finally
            {
                loader.IsVisible = false;
            }
        }

        /// <summary>
        /// Navega a una nueva página con parámetros opcionales.
        /// </summary>
        /// <param name="route">
        /// Ruta de la página destino.
        /// </param>
        /// <param name="parameters">
        /// Parámetros opcionales.
        /// </param>
        /// <param name="animate">
        /// Indica si se debe animar la transición.
        /// </param>
        protected async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null, bool animate = false)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (parameters is not null && parameters.Any())
                {
                    await Shell.Current.GoToAsync(route, animate, parameters);
                }
                else
                {
                    await Shell.Current.GoToAsync(route, animate);
                }
            });
        }

        /// <summary>
        /// Comando para regresar a la página anterior.
        /// </summary>
        [RelayCommand]
        protected async Task GoBackAsync() => await Shell.Current.Navigation.PopAsync();

        /// <summary>
        /// Muestra un mensaje de éxito y regresa a la pantalla anterior.
        /// </summary>
        protected async Task NotifySuccessAndNavigateBack()
        {
            await AppShell.Current.DisplayAlert("Success", "The prompt has been updated successfully.", "OK");

            await GoBackAsync();
        }

        // ============================== 🛠 MÉTODOS AUXILIARES ==============================

        /// <summary>
        /// Actualiza el contador de palabras seleccionadas y su etiqueta.
        /// </summary>
        /// <param name="count">
        /// Número de palabras seleccionadas.
        /// </param>
        protected void UpdateSelectedTextLabelCount(int count)
        {
            SelectedTextLabelCount = count == 0 ? $"{AppMessagesEng.TotalMessage} None" : $"{AppMessagesEng.TotalMessage} {count}";
        }

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

        protected void CleanSearch()
        {
            this.oldSearch = string.Empty;

            this.Search = string.Empty;
        }

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

        // ============================ 📌 EVENTO DE INICIALIZACIÓN ============================
        public void Initialize()
        {
            _adMobService.LoadInterstitialAd();  // Precargar el anuncio intersticial

            _adMobService.SetupAdEvents();       // Configurar eventos de AdMob
        }

        protected async void NavigateTo(string page, PromptTemplate selectedPrompt)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (selectedPrompt != null)
                {
                    await NavigateToAsync(page, new Dictionary<string, object>
            {
                { "selectedId", selectedPrompt.Id }
            });
                }
            }, AppMessagesEng.GenericError);
        }

        [RelayCommand]
        public virtual async Task MyBack()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await Shell.Current.GoToAsync("..");
            });
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