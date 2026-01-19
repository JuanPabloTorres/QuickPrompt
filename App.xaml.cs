using QuickPrompt.Services;

namespace QuickPrompt
{
    public partial class App : Application
    {
        private readonly DatabaseServiceManager _databaseServiceManager;
        private readonly IServiceProvider _serviceProvider;

        public App(DatabaseServiceManager databaseServiceManager, IServiceProvider serviceProvider)
        {
            global::System.Diagnostics.Debug.WriteLine("[App.Constructor] Starting...");

            try
            {
                InitializeComponent();
                global::System.Diagnostics.Debug.WriteLine("[App.Constructor] InitializeComponent completed");

                _databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(nameof(databaseServiceManager));
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                global::System.Diagnostics.Debug.WriteLine("[App.Constructor] Dependencies injected");

                // ✅ Database initialization in background with error handling
                Task.Run(async () =>
                {
                    try
                    {
                        global::System.Diagnostics.Debug.WriteLine("[App] Database initialization starting...");
                        await _databaseServiceManager.InitializeAsync();
                        global::System.Diagnostics.Debug.WriteLine("[App] Database initialization completed");
                    }
                    catch (Exception ex)
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[App] Database initialization error: {ex.GetType().Name}: {ex.Message}");
                        // Don't throw - app should continue without database error
                    }
                });

                global::System.Diagnostics.Debug.WriteLine("[App.Constructor] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[App.Constructor] FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
                global::System.Diagnostics.Debug.WriteLine($"[App.Constructor] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Starting...");

            try
            {
                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Resolving AppShell from service provider...");
                
                // ✅ FIX: Use injected service provider directly instead of Handler.MauiContext
                var appShell = _serviceProvider.GetRequiredService<AppShell>();
                
                if (appShell == null)
                {
                    throw new InvalidOperationException("Failed to resolve AppShell from service provider");
                }

                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] AppShell resolved successfully");
                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Creating Window...");
                
                var window = new Window(appShell)
                {
                    Title = "QuickPrompt"
                };
                
                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Window created successfully");

                return window;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[App.CreateWindow] FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
                global::System.Diagnostics.Debug.WriteLine($"[App.CreateWindow] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void OnStart()
        {
            global::System.Diagnostics.Debug.WriteLine("[App.OnStart] Starting...");

            try
            {
                PromptCacheCleanupService.RunCleanupIfDue();
                global::System.Diagnostics.Debug.WriteLine("[App.OnStart] Cache cleanup completed");
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[App.OnStart] Error in cache cleanup: {ex.GetType().Name}: {ex.Message}");
                // Don't throw - not critical
            }
        }
    }
}