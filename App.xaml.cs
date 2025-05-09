using QuickPrompt.Services;

namespace QuickPrompt
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window
            {
                Page = new AppShell() // ← Aquí especificas el Shell como la página raíz
            };

            return window;
        }

        protected override void OnStart()
        {
            PromptCacheCleanupService.RunCleanupIfDue();
        }
    }
}