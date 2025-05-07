using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Plugin.MauiMTAdmob;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;
using System.Reflection;

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

            var mauiApp = builder.Build();

            //Ioc.Default.ConfigureServices(mauiApp.Services);

            InitializeIoC(mauiApp);

            return builder.Build();
        }

        // Configura el builder base de la aplicación
        private static void ConfigureBuilder(MauiAppBuilder builder)
        {
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiMTAdmob()  // Habilitar logs
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIconsOutlined-Regular.otf", "MaterialIconsOutlined-Regular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons-Regular");
                    fonts.AddFont("Designer.otf", "Designer");
                });
        }

        private static void InitializeIoC(MauiApp mauiApp)
        {
            Ioc.Default.ConfigureServices(mauiApp.Services);
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

            return new ConfigurationBuilder().AddJsonStream(stream).Build();
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

            // 🔹 Registrar configuración de AdMob utilizando IOptions
            builder.Services.Configure<AdMobSettings>(options =>
            {
                options.InterstitialAdId = appSettings["AdMobSettings:InterstitialAdId"] ?? string.Empty;
                options.Android = appSettings["AdMobSettings:Android"] ?? string.Empty;
                options.iOs = appSettings["AdMobSettings:iOs"] ?? string.Empty;
            });

            //var adMobSettings = new AdMobSettings()
            //{
            //    InterstitialAdId = appSettings["AdMobSettings:InterstitialAdId"],
            //    Android = appSettings["AdMobSettings:Android"],
            //    iOs = appSettings["AdMobSettings:iOs"],
            //};

            //builder.Services.AddSingleton(adMobSettings);

            builder.Services.AddSingleton(appSettingsModel);


            builder.Services.AddSingleton<AdmobService>();
        }

        // Registra los ViewModels en el contenedor de dependencias
        private static void RegisterViewModels(MauiAppBuilder builder)
        {
            // ViewModels que necesitan recrearse en cada navegación
            builder.Services.AddTransient<MainPageViewModel>();

            builder.Services.AddTransient<SettingViewModel>();

            // ViewModels que deben mantener su estado dentro de una sesión
            builder.Services.AddScoped<LoadPromptsPageViewModel>();

            builder.Services.AddScoped<PromptDetailsPageViewModel>();

            builder.Services.AddScoped<EditPromptPageViewModel>();

            builder.Services.AddScoped<QuickPromptViewModel>();

            builder.Services.AddScoped<AdmobBannerViewModel>();
        }

        // Registra las páginas en el contenedor de dependencias
        private static void RegisterPages(MauiAppBuilder builder)
        {
            builder.Services.AddTransient<MainPage>();

            builder.Services.AddTransient<PromptDetailsPage>();

            builder.Services.AddTransient<EditPromptPage>();

            builder.Services.AddTransient<SettingPage>();

            builder.Services.AddScoped<QuickPromptPage>();
        }

        // Configura las rutas para la navegación
        private static void ConfigureRouting()
        {
            Routing.RegisterRoute(nameof(PromptDetailsPage), typeof(PromptDetailsPage));

            Routing.RegisterRoute(nameof(EditPromptPage), typeof(EditPromptPage));

            Routing.RegisterRoute(nameof(GuidePage), typeof(GuidePage));

            Routing.RegisterRoute("createPromptPage", typeof(MainPage));
        }
    }
}