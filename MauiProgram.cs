using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
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
            ConfigureBuilder(builder);

            var appSettings = LoadAppSettings();

            RegisterServices(builder, appSettings);

            RegisterViewModels(builder);

            RegisterPages(builder);

            ConfigureRouting();

#if DEBUG
            builder.Logging.AddDebug();
#else
            builder.Logging.AddConsole(); // Habilita logging en release
#endif

            return builder.Build();
        }

        // Configura el builder base de la aplicación
        private static void ConfigureBuilder(MauiAppBuilder builder)
        {
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIconsOutlined-Regular.otf", "MaterialIconsOutlined-Regular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons-Regular");
                });
        }

        // Carga las configuraciones desde un recurso incrustado
        private static IConfiguration LoadAppSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();

            const string resourceName = "QuickPrompt.appsettings.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"El recurso incrustado '{resourceName}' no fue encontrado.");
            }

            return new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();
        }

        // Registra los servicios necesarios en el contenedor de dependencias
        private static void RegisterServices(MauiAppBuilder builder, IConfiguration appSettings)
        {
            var apiKey = appSettings["GPTApiKeys:Key1"];

            builder.Services.AddSingleton<PromptDatabaseService>();

            builder.Services.AddSingleton<IChatGPTService>(sp => new ChatGPTService(apiKey));

            // Registrar configuración de versión como servicio
            var appSettingsModel = new AppSettings
            {
                Version = appSettings["AppSettings:Version"] ?? "1.0.0", // Valor predeterminado si no se encuentra
            };

            builder.Services.AddSingleton(appSettingsModel);
        }

        // Registra los ViewModels en el contenedor de dependencias
        private static void RegisterViewModels(MauiAppBuilder builder)
        {
            builder.Services.AddTransient<MainPageViewModel>();

            builder.Services.AddTransient<LoadPromptsPageViewModel>();

            builder.Services.AddTransient<PromptDetailsPageViewModel>();

            builder.Services.AddTransient<EditPromptPageViewModel>();

            builder.Services.AddTransient<QuickPromptViewModel>();

            builder.Services.AddTransient<SettingViewModel>();
        }

        // Registra las páginas en el contenedor de dependencias
        private static void RegisterPages(MauiAppBuilder builder)
        {
            builder.Services.AddTransient<MainPage>();

            builder.Services.AddTransient<PromptDetailsPage>();

            builder.Services.AddTransient<EditPromptPage>();

            builder.Services.AddTransient<SettingPage>();

            builder.Services.AddTransient<QuickPromptPage>();
        }

        // Configura las rutas para la navegación
        private static void ConfigureRouting()
        {
            Routing.RegisterRoute(nameof(PromptDetailsPage), typeof(PromptDetailsPage));

            Routing.RegisterRoute(nameof(EditPromptPage), typeof(EditPromptPage));
        }
    }
}