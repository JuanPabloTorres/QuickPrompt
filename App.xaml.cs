using Microsoft.Extensions.Logging;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Infrastructure.Logging;
using QuickPrompt.Services;

namespace QuickPrompt
{
    public partial class App : Application
    {
        private readonly DatabaseServiceManager _databaseServiceManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<App>? _logger;
        private readonly IDialogService _dialogService;
        private readonly IThemeService _themeService;

        public App(
            DatabaseServiceManager databaseServiceManager, 
            IServiceProvider serviceProvider,
            ILogger<App>? logger,
            IDialogService dialogService,
            IThemeService themeService)
        {
            try
            {
                _logger = logger;
                _logger?.LogInformation("[App.Constructor] Starting...");

                InitializeComponent();
                _logger?.LogInformation("[App.Constructor] InitializeComponent completed");

                _databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(nameof(databaseServiceManager));
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
                _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
                _logger?.LogInformation("[App.Constructor] Dependencies injected");

                // ✅ UX IMPROVEMENTS: Load saved theme preference
                _themeService.LoadSavedTheme();
                _logger?.LogInformation($"[App.Constructor] Theme loaded: {_themeService.GetEffectiveTheme()}");

                // Initialize global exception handler with null-safe logger
                if (_logger != null)
                {
                    GlobalExceptionHandler.Initialize(_logger, async (title, message) =>
                    {
                        try
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await _dialogService.ShowErrorAsync($"{title}: {message}");
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[App] Failed to show error dialog");
                            System.Diagnostics.Debug.WriteLine($"[App] Failed to show error dialog: {ex.Message}");
                        }
                    });
                    _logger.LogInformation("[App.Constructor] Global exception handler initialized");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[App.Constructor] WARNING: Logger is null, using Debug fallback");
                }

                // Database initialization in background with error handling
                Task.Run(async () =>
                {
                    try
                    {
                        _logger?.LogInformation("[App] Database initialization starting...");
                        await _databaseServiceManager.InitializeAsync();
                        _logger?.LogInformation("[App] Database initialization completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[App] Database initialization failed");
                        System.Diagnostics.Debug.WriteLine($"[App] Database initialization failed: {ex.Message}");
                    }
                });

                _logger?.LogInformation("[App.Constructor] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "[App.Constructor] FATAL ERROR during initialization");
                System.Diagnostics.Debug.WriteLine($"[App.Constructor] FATAL: {ex.Message}");
                throw;
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            _logger?.LogInformation("[App.CreateWindow] Starting...");

            try
            {
                _logger?.LogInformation("[App.CreateWindow] Resolving AppShell from service provider...");
                
                var appShell = _serviceProvider.GetRequiredService<AppShell>();
                
                if (appShell == null)
                {
                    _logger?.LogError("[App.CreateWindow] AppShell resolution returned null");
                    throw new InvalidOperationException("Failed to resolve AppShell from service provider");
                }

                _logger?.LogInformation("[App.CreateWindow] AppShell resolved successfully");
                
                var window = new Window(appShell)
                {
                    Title = "QuickPrompt"
                };
                
                _logger?.LogInformation("[App.CreateWindow] Window created successfully");

                return window;
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "[App.CreateWindow] FATAL ERROR creating window");
                System.Diagnostics.Debug.WriteLine($"[App.CreateWindow] FATAL: {ex.Message}");
                throw;
            }
        }

        protected override void OnStart()
        {
            _logger?.LogInformation("[App.OnStart] Starting...");

            try
            {
                PromptCacheCleanupService.RunCleanupIfDue();
                _logger?.LogInformation("[App.OnStart] Cache cleanup completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[App.OnStart] Error in cache cleanup");
                System.Diagnostics.Debug.WriteLine($"[App.OnStart] Cache cleanup error: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            _logger?.LogInformation("[App.OnSleep] App entering sleep mode");
            base.OnSleep();
        }

        protected override void OnResume()
        {
            _logger?.LogInformation("[App.OnResume] App resuming from sleep");
            base.OnResume();
        }
    }
}