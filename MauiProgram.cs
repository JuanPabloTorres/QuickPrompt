using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuickPrompt.CustomEntries;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;
using System.Reflection;
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

            // Recuperar la API Key desde la configuración
            var apiKey = appSettings["GPTApiKeys:Key1"];

            builder.Services.AddSingleton<PromptDatabaseService>();  // Inyectar el servicio de base de datos

            // Registro del servicio para ChatGPT
            builder.Services.AddSingleton<IChatGPTService>(sp => new ChatGPTService(apiKey));

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

        public static IConfiguration LoadAppSettings()
        {
            var assembly = Assembly.GetExecutingAssembly(); // Obtener el ensamblado actual

            var resourceName = "QuickPrompt.appsettings.json"; // Nombre completo del recurso incrustado

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"El recurso incrustado '{resourceName}' no fue encontrado.");
            }

            var configuration = new ConfigurationBuilder().AddJsonStream(stream) // Cargar desde el recurso incrustado
                .Build();

            return configuration;
        }

        //private static AppSettings LoadAppSettings()
        //{
        //    // Directorio de salida para aplicaciones MAUI
        //    var jsonFilePath = Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");

        //    // Validar existencia del archivo
        //    if (!File.Exists(jsonFilePath))
        //    {
        //        throw new FileNotFoundException($"No se encontró el archivo de configuración en: {jsonFilePath}");
        //    }

        //    // Leer y deserializar el archivo JSON
        //    var json = File.ReadAllText(jsonFilePath);
        //    return JsonSerializer.Deserialize<AppSettings>(json);
        //}


    }
}