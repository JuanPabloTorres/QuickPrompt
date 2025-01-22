using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using QuickPrompt.CustomEntries;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;
using System.Text.Json;

namespace QuickPrompt
{
    public static class MauiProgram
    {
 

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Cargar el archivo JSON
            var appSettings = LoadAppSettings();

            builder.Services.AddSingleton<PromptDatabaseService>();  // Inyectar el servicio de base de datos

            // Registro del servicio para ChatGPT
            builder.Services.AddSingleton<IChatGPTService>(sp => new ChatGPTService(appSettings.ApiKeys.Key1));

            // Registro del ViewModel
            builder.Services.AddTransient<MainPageViewModel>();

            builder.Services.AddTransient<LoadPromptsPageViewModel>();

            builder.Services.AddTransient<PromptDetailsPageViewModel>();

            builder.Services.AddTransient<EditPromptPageViewModel>();

            // Registro de la página principal con inyección de dependencias
            builder.Services.AddTransient<MainPage>();

            builder.Services.AddTransient<PromptDetailsPage>();

            builder.Services.AddTransient<EditPromptPage>();

            Routing.RegisterRoute("PromptDetailsPage", typeof(PromptDetailsPage));
            
            Routing.RegisterRoute(nameof(EditPromptPage), typeof(EditPromptPage));

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static AppSettings LoadAppSettings()
        {
            var jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"No se encontró el archivo de configuración: {jsonFilePath}");

            var json = File.ReadAllText(jsonFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
    }
}