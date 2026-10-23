using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Pages;

namespace QuickPrompt
{
    public partial class AppShell : Shell
    {
        private readonly ITabBarService _tabBarService;

        public AppShell(ITabBarService tabBarService)
        {
            InitializeComponent();

            _tabBarService = tabBarService ?? throw new ArgumentNullException(nameof(tabBarService));

            // Mostrar la guía solo la primera vez
            ShowGuideIfFirstLaunch();
        }

        private async void ShowGuideIfFirstLaunch()
        {
            bool hasSeenGuide = Preferences.Get("HasSeenGuide", false);

            if (!hasSeenGuide)
            {
                Preferences.Set("HasSeenGuide", true);

                // Esperar carga del Shell (opcional)
                await Task.Delay(500);

                _tabBarService.SetVisibility(false);

                await GoToAsync($"/{nameof(GuidePage)}");
            }
        }
    }
}