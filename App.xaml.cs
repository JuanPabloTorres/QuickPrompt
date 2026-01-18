using QuickPrompt.Services;

namespace QuickPrompt
{
    public partial class App : Application
    {
        DatabaseServiceManager _databaseServiceManager;
        private readonly AppShell _appShell;

        public App(DatabaseServiceManager databaseServiceManager, AppShell appShell)
        {
            global::System.Diagnostics.Debug.WriteLine("[App.Constructor] Starting...");

            try
            {
                InitializeComponent();
                global::System.Diagnostics.Debug.WriteLine("[App.Constructor] InitializeComponent completed");

                _databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(nameof(databaseServiceManager));
                _appShell = appShell ?? throw new ArgumentNullException(nameof(appShell));
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
                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Using injected AppShell");

                global::System.Diagnostics.Debug.WriteLine("[App.CreateWindow] Creating Window...");
                var window = new Window
                {
                    Page = _appShell
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