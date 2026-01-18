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
using QuickPrompt.Constants;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Reflection;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Infrastructure.Services.Cache;
using QuickPrompt.Infrastructure.Services.UI;

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

            return mauiApp; // ✅ FIX: Retornar el mauiApp que ya fue built
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
            // 🆕 PHASE 1: Application Layer Services
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IPromptCacheService, PromptCacheService>();
            builder.Services.AddSingleton<ITabBarService, TabBarService>();

            // 🆕 PHASE 1: Use Cases
            builder.Services.AddTransient<CreatePromptUseCase>();
            builder.Services.AddTransient<UpdatePromptUseCase>();
            builder.Services.AddTransient<DeletePromptUseCase>();
            builder.Services.AddTransient<ExecutePromptUseCase>();
            builder.Services.AddTransient<GetPromptByIdUseCase>();

            // ✅ Database
            builder.Services.AddSingleton<DatabaseConnectionProvider>();

            // ✅ Existing Repositories
            builder.Services.AddSingleton<IPromptRepository, PromptRepository>();
            builder.Services.AddSingleton<IFinalPromptRepository, FinalPromptRepository>();
            builder.Services.AddSingleton<DatabaseServiceManager>();

            // ✅ Existing Services
            builder.Services.AddSingleton<AdmobService>();

            // 🆕 ENGINE SERVICES
            builder.Services.AddSingleton<IWebViewInjectionService, WebViewInjectionService>();

            // 🆕 HISTORY SERVICES
            builder.Services.AddSingleton<IExecutionHistoryRepository>(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");
                return new SqliteExecutionHistoryRepository(dbPath);
            });

            builder.Services.AddSingleton<IExecutionHistoryCloudRepository>(sp =>
            {
                // TODO: Replace with real Firestore repo when Firebase is configured
                return new NullExecutionHistoryCloudRepository();
            });

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
                        var settings = settingsService.GetSettingsAsync().GetAwaiter().GetResult();
                        return settings.CloudSyncEnabled;
                    }
                );
            });

            builder.Services.AddSingleton<ExecutionHistoryIntegration>(sp =>
            {
                var localRepo = sp.GetRequiredService<IExecutionHistoryRepository>();
                var syncService = sp.GetRequiredService<SyncService>();
                var deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
                Preferences.Set("DeviceId", deviceId);
                return new ExecutionHistoryIntegration(localRepo, syncService, deviceId);
            });

            // 🆕 SETTINGS SERVICES
            builder.Services.AddSingleton<ISettingsService, SettingsService>();
            builder.Services.AddSingleton<ISessionService, SessionService>();

            // ✅ Config
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

            // 🆕 AI PAGES
            builder.Services.AddTransient<EngineWebViewPage>();
            builder.Services.AddTransient<Features.AI.Pages.AiLauncherPage>();
        }

        private static void ConfigureRouting()
        {
            Routing.RegisterRoute(NavigationRoutes.PromptDetails, typeof(PromptDetailsPage));
            Routing.RegisterRoute(NavigationRoutes.EditPrompt, typeof(EditPromptPage));
            Routing.RegisterRoute(NavigationRoutes.Guide, typeof(GuidePage));
            Routing.RegisterRoute(NavigationRoutes.PromptBuilder, typeof(PromptBuilderPage));
            Routing.RegisterRoute(NavigationRoutes.EngineWebView, typeof(EngineWebViewPage));
        }
    }
}