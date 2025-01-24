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

namespace QuickPrompt.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isLoading; // Controla el estado de carga

        /// <summary>
        /// Ejecuta una tarea con manejo de excepciones y control de estado de carga.
        /// </summary>
        /// <param name="action">Tarea asíncrona a ejecutar.</param>
        /// <param name="errorMessage">Mensaje de error que se mostrará en caso de fallo.</param>
        protected async Task ExecuteWithLoadingAsync(Func<Task> action, string errorMessage = "Ocurrió un error. Por favor, inténtalo nuevamente.")
        {
            try
            {
                IsLoading = true;

                await action();
            }
            catch (Exception ex)
            {
                await AppShell.Current.DisplayAlert("Error", errorMessage, "OK");
                // Si es necesario, puedes registrar el error aquí
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Navegar a otra página con parámetros.
        /// </summary>
        /// <param name="route">Ruta de la página.</param>
        /// <param name="parameters">Parámetros opcionales para la navegación.</param>
        /// <param name="animate">Si se debe animar la transición.</param>
        protected async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null, bool animate = true)
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
                await AppShell.Current.DisplayAlert("Error de Navegación", $"No se pudo navegar a la página: {ex.Message}", "OK");
            }
        }

        // Comando para regresar a la página anterior
        [RelayCommand]
        protected async Task GoBackAsync()
        {
            await NavigateToAsync("..");
        }

        // Extra las palabras que continene {} 
        protected List<string> ExtractVariables(string text)
        {
            var variables = new List<string>();

            int startIndex = text.IndexOf('{');

            while (startIndex != -1)
            {
                int endIndex = text.IndexOf('}', startIndex);

                if (endIndex != -1)
                {
                    string variable = text.Substring(startIndex + 1, endIndex - startIndex - 1);

                    if (!variables.Contains(variable))
                    {
                        variables.Add(variable);
                    }
                    startIndex = text.IndexOf('{', endIndex);
                }
                else
                {
                    break;
                }
            }

            return variables;
        }


    }

}
