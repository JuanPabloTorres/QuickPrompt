using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;

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
    public SettingViewModel(AppSettings appSettings, DatabaseServiceManager dbManager) : base(dbManager)
    {
        appVersion = appSettings?.Version ?? "Unknown Version"; // Evita valores nulos
    }

    /// <summary>
    /// Comando para borrar la base de datos.
    /// </summary>
    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            bool confirm = await AppShell.Current.DisplayAlert(
          AppMessagesEng.ConfirmationTitle,
          AppMessagesEng.RestoreConfirmationMessage,
          AppMessagesEng.Yes,
          AppMessagesEng.No);

            if (!confirm)
                return;

            await databaseServiceManager.RestoreAsync();

            await GenericToolBox.ShowLottieMessageAsync(
                "CompleteAnimation.json",
                AppMessagesEng.DatabaseRestore);
        }, AppMessagesEng.GenericError);
    }

    [RelayCommand]
    private async Task OpenGuideLinkAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var uri = new Uri("https://estjuanpablotorres.wixsite.com/quickprompt");

            await Launcher.Default.OpenAsync(uri);
        }, AppMessagesEng.GenericError);
    }
}