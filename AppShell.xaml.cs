using QuickPrompt.Pages;

namespace QuickPrompt
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Mostrar la guía solo la primera vez
            ShowGuideIfFirstLaunch();
        }

        private async void ShowGuideIfFirstLaunch()
        {
            bool hasSeenGuide = Preferences.Get("HasSeenGuide", false);

            if (!hasSeenGuide)
            {
                Preferences.Set("HasSeenGuide", true);

                // Esperar a que se cargue el Shell completamente
                await Task.Delay(200);

                await GoToAsync($"/{nameof(GuidePage)}");
            }
        }
    }
}