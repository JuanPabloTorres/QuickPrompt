using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using QuickPrompt.ViewModels.Prompts;
using QuickPrompt.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        // ============================== 🌟 PROPIEDADES ==============================
        [ObservableProperty]
        public string emptyViewText = "No Prompts Available ";

        [ObservableProperty]
        protected string search;

        [ObservableProperty]
        protected bool isAllSelected = false;

        protected string oldSearch;

        /// <summary>
        /// Controla el estado de carga de la aplicación.
        /// </summary>
        [ObservableProperty] public bool isLoading;

        /// <summary>
        /// Etiqueta que muestra el conteo de variables seleccionadas.
        /// </summary>
        [ObservableProperty] private string selectedTextLabelCount = $"{AppMessagesEng.TotalMessage} None";

        [ObservableProperty] private bool showPromptActions;

        protected PromptDatabaseService _databaseService;

        protected AdmobService _adMobService;

        protected BaseViewModel()
        {
        }

        protected BaseViewModel(PromptDatabaseService promptDatabaseService)
        {
            this._databaseService = promptDatabaseService;
        }

        protected BaseViewModel(PromptDatabaseService promptDatabaseService, AdmobService admobService)
        {
            this._databaseService = promptDatabaseService;

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
            try
            {
                if (parameters is not null && parameters.Any())
                {
                    var query = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value.ToString()!)}"));

                    route = $"{route}?{query}";
                }

                await Shell.Current.GoToAsync(route, animate);
            }
            catch (Exception ex)
            {
                await AppShell.Current.DisplayAlert("Navigation Error", $"Could not navigate to the page: {ex.Message}", "OK");
            }
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
        protected List<string> ExtractVariables(string promptText)
        {
            var variables = new List<string>();

            int startIndex = promptText.IndexOf('<');

            while (startIndex != -1)
            {
                int endIndex = promptText.IndexOf('>', startIndex);

                if (endIndex == -1) break;

                string variable = promptText.Substring(startIndex + 1, endIndex - startIndex - 1);

                if (!variables.Contains(variable))
                {
                    variables.Add(variable);
                }

                startIndex = promptText.IndexOf('<', endIndex);
            }

            return variables;
        }

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
        public async Task MyBack()
        {
            await Shell.Current.GoToAsync("..");
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