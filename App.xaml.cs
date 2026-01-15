using QuickPrompt.Services;

namespace QuickPrompt
{
    public partial class App : Application
    {

        DatabaseServiceManager _databaseServiceManager;

        public App(DatabaseServiceManager databaseServiceManager)
        {
            InitializeComponent();

            _databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(nameof(databaseServiceManager));

            Task.Run(async () =>
            {
                try
                {
                    await _databaseServiceManager.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Database initialization error: {ex.Message}");
                    // No lanzar excepción - dejar que la app continúe
                }
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                var window = new Window
                {
                    Page = new AppShell()
                };
                return window;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App.CreateWindow] Error: {ex.Message}");
                throw;
            }
        }

        protected override void OnStart()
        {
            try
            {
                PromptCacheCleanupService.RunCleanupIfDue();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App.OnStart] Error: {ex.Message}");
            }
        }
    }
}