﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
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
    public SettingViewModel(AppSettings appSettings, PromptDatabaseService databaseService) : base(databaseService)
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
            bool confirm = await AppShell.Current.DisplayAlert("Confirmation", "Are you sure you want to restore all data?", "Yes", "No");

            if (confirm)
            {
                await _databaseService.RestoreDatabaseAsync();

                //await App.Current.MainPage.DisplayAlert("Éxito", "La base de datos ha sido eliminada.", "OK");

                await AppShell.Current.DisplayAlert("Success", "Database has been restore.", "OK");
            }
        }, AppMessagesEng.GenericError);
    }
}