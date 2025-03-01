using CommunityToolkit.Mvvm.ComponentModel;
using QuickPrompt.Models;

namespace QuickPrompt.ViewModels;

/// <summary>
/// ViewModel para la página de configuración. Maneja la información de la versión de la aplicación.
/// </summary>
public partial class SettingViewModel : BaseViewModel
{
    [ObservableProperty]
    private string appVersion;

    /// <summary>
    /// Constructor que inicializa la versión de la aplicación.
    /// </summary>
    /// <param name="appSettings">
    /// Instancia de la configuración de la aplicación.
    /// </param>
    public SettingViewModel(AppSettings appSettings)
    {
        appVersion = appSettings?.Version ?? "Unknown Version"; // Evita valores nulos
    }
}