using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using QuickPrompt.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public abstract partial class RootViewModel : ObservableObject
    {
        [ObservableProperty] public bool isLoading;

        [ObservableProperty] public string emptyViewText = "No Prompts Available";

        [ObservableProperty] private string selectedTextLabelCount = $"{AppMessagesEng.TotalMessage} None";

        protected AdmobService _adMobService;

        protected RootViewModel()
        {
        }

        protected RootViewModel(AdmobService admobService)
        {
            _adMobService = admobService;
        }

        protected void UpdateSelectedTextLabelCount(int count)
        {
            SelectedTextLabelCount = count == 0 ? $"{AppMessagesEng.TotalMessage} None" : $"{AppMessagesEng.TotalMessage} {count}";
        }

        [RelayCommand]
        protected async Task GoBackAsync() => await Shell.Current.Navigation.PopAsync();

        [RelayCommand]
        protected async Task NavigateToCreateAsync() => await Shell.Current.GoToAsync("//Create");

        protected async Task ExecuteWithLoadingAsync(Func<Task> action, string errorMessage = "An error occurred. Please try again.")
        {
            try
            {
                IsLoading = true;
                await action();
            }
            catch (Exception ex)
            {
                // ✅ Log detailed error information
                System.Diagnostics.Debug.WriteLine($"[ExecuteWithLoadingAsync] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ExecuteWithLoadingAsync] StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[ExecuteWithLoadingAsync] InnerException: {ex.InnerException?.Message}");
                
                // Show custom error message with details
                await Shell.Current.DisplayAlert("Error", $"{errorMessage}\n\nDetails: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null, bool animate = false)
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                if (parameters is not null && parameters.Any())
                    await Shell.Current.GoToAsync(route, animate, parameters);
                else
                    await Shell.Current.GoToAsync(route, animate);
            });
        }

        protected async void NavigateTo(string page, PromptTemplate selectedPrompt)
        {
            try
            {
                IsLoading = true;
                
                if (selectedPrompt == null)
                {
                    await Shell.Current.DisplayAlert("Error", "No prompt selected", "OK");
                    return;
                }

                await NavigateToAsync(page, new Dictionary<string, object>
                {
                    { "selectedId", selectedPrompt.Id }
                });
            }
            catch (Exception ex)
            {
                // ✅ Show detailed error instead of generic message
                System.Diagnostics.Debug.WriteLine($"[NavigateTo] Error navigating to {page}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[NavigateTo] StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[NavigateTo] InnerException: {ex.InnerException?.Message}");
                
                await Shell.Current.DisplayAlert("Error", 
                    $"Failed to navigate to {page}:\n{ex.Message}", 
                    "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public virtual async Task MyBack()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                await Shell.Current.GoToAsync("..");
            });
        }

        public void Initialize()
        {
            _adMobService.LoadInterstitialAd();  // Precargar el anuncio intersticial

            _adMobService.SetupAdEvents();       // Configurar eventos de AdMob
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
    }
}