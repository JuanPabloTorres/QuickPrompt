using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Plugin.MauiMTAdmob;
using QuickPrompt.Models;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.ViewModels;
using QuickPrompt.Engines.Injection;
using QuickPrompt.History.Repositories;
using QuickPrompt.History.Sync;
using QuickPrompt.History;
using QuickPrompt.Settings;
using QuickPrompt.Engines.WebView;
using SkiaSharp.Views.Maui.Controls.Hosting;
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
            builder.Logging.AddConsole();
#endif

            var mauiApp = builder.Build();

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
                .UseSkiaSharp()
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

            // ✅ Database
            builder.Services.AddSingleton<DatabaseConnectionProvider>();

            // ✅ Existing Repositories
            builder.Services.AddSingleton<IPromptRepository, PromptRepository>();
            builder.Services.AddSingleton<IFinalPromptRepository, FinalPromptRepository>();
            builder.Services.AddSingleton<DatabaseServiceManager>();

            // ✅ Existing Services
            builder.Services.AddSingleton<IChatGPTService>(sp => new ChatGPTService(apiKey));
            builder.Services.AddSingleton<AdmobService>();

            // 🆕 ENGINE SERVICES (Step 1-2)
            // Registry is static, but we register as singleton for consistency
            builder.Services.AddSingleton<IWebViewInjectionService, WebViewInjectionService>();

            // 🆕 HISTORY SERVICES (Step 3)
            builder.Services.AddSingleton<IExecutionHistoryRepository>(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");
                return new SqliteExecutionHistoryRepository(dbPath);
            });

            // Cloud repository: use Null implementation if Firestore not configured
            builder.Services.AddSingleton<IExecutionHistoryCloudRepository>(sp =>
            {
                // TODO: Replace with real Firestore repo when Firebase is configured
                // Check if Firestore config exists (google-services.json, etc.)
                return new NullExecutionHistoryCloudRepository();
            });

            // SyncService depends on repos and auth state
            builder.Services.AddSingleton<SyncService>(sp =>
            {
                var localRepo = sp.GetRequiredService<IExecutionHistoryRepository>();
                var cloudRepo = sp.GetRequiredService<IExecutionHistoryCloudRepository>();
                var sessionService = sp.GetRequiredService<ISessionService>();
                var settingsService = sp.GetRequiredService<ISettingsService>();

                return new SyncService(
                    localRepo,
                    cloudRepo,
                    () => sessionService.IsLoggedIn,
                    () =>
                    {
                        // Check if CloudSyncEnabled (async call in sync context - gets cached settings)
                        var settings = settingsService.GetSettingsAsync().GetAwaiter().GetResult();
                        return settings.CloudSyncEnabled;
                    }
                );
            });

            // ExecutionHistoryIntegration
            builder.Services.AddSingleton<ExecutionHistoryIntegration>(sp =>
            {
                var localRepo = sp.GetRequiredService<IExecutionHistoryRepository>();
                var syncService = sp.GetRequiredService<SyncService>();
                var deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
                Preferences.Set("DeviceId", deviceId); // Persist if new
                return new ExecutionHistoryIntegration(localRepo, syncService, deviceId);
            });

            // 🆕 SETTINGS SERVICES (Step 4)
            builder.Services.AddSingleton<ISettingsService, SettingsService>();

            // 🆕 SESSION SERVICE (Step 4, placeholder implementation)
            builder.Services.AddSingleton<ISessionService, SessionService>();

            // ✅ Existing Config
            var appSettingsModel = new AppSettings
            {
                Version = appSettings["AppSettings:Version"] ?? "1.0.0",
            };

            builder.Services.Configure<AdMobSettings>(options =>
            {
                options.InterstitialAdId = appSettings["AdMobSettings:InterstitialAdId"] ?? string.Empty;
                options.Android = appSettings["AdMobSettings:Android"] ?? string.Empty;
                options.iOs = appSettings["AdMobSettings:iOs"] ?? string.Empty;
            });

            builder.Services.AddSingleton(appSettingsModel);
        }

        private static void RegisterViewModels(MauiAppBuilder builder)
        {
            // Existing ViewModels
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<SettingViewModel>();
            builder.Services.AddScoped<PromptDetailsPageViewModel>();
            builder.Services.AddScoped<EditPromptPageViewModel>();
            builder.Services.AddScoped<QuickPromptViewModel>();
            builder.Services.AddScoped<AdmobBannerViewModel>();
            builder.Services.AddTransient<AiWebViewPageViewModel>();
            builder.Services.AddTransient<AiLauncherViewModel>();
            builder.Services.AddTransient<PromptBuilderPageViewModel>();

            // 🆕 NEW VIEWMODELS (Step 4)
            builder.Services.AddTransient<SettingsViewModel>();
        }

        private static void RegisterPages(MauiAppBuilder builder)
        {
            // Existing Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<PromptDetailsPage>();
            builder.Services.AddTransient<EditPromptPage>();
            builder.Services.AddTransient<SettingPage>();
            builder.Services.AddScoped<QuickPromptPage>();

            // 🆕 NEW PAGES (Step 2)
            builder.Services.AddTransient<EngineWebViewPage>();
        }

        private static void ConfigureRouting()
        {
            // Existing routes
            Routing.RegisterRoute(nameof(PromptDetailsPage), typeof(PromptDetailsPage));
            Routing.RegisterRoute(nameof(EditPromptPage), typeof(EditPromptPage));
            Routing.RegisterRoute(nameof(GuidePage), typeof(GuidePage));
            Routing.RegisterRoute(nameof(GrokPage), typeof(GrokPage));
            Routing.RegisterRoute(nameof(ChatGptPage), typeof(ChatGptPage));
            Routing.RegisterRoute(nameof(GeminiPage), typeof(GeminiPage));
            Routing.RegisterRoute(nameof(CopilotChatPage), typeof(CopilotChatPage));
            Routing.RegisterRoute(nameof(PromptBuilderPage), typeof(PromptBuilderPage));

            // 🆕 NEW ROUTES (Step 2)
            Routing.RegisterRoute(nameof(EngineWebViewPage), typeof(EngineWebViewPage));
        }
    }
}