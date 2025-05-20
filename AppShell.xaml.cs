using QuickPrompt.Pages;
using QuickPrompt.Tools;

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

                // Esperar carga del Shell (opcional)
                await Task.Delay(500);

                //var tabContext = Shell.Current.CurrentItem?.CurrentItem;

                //if (tabContext is not null)
                //    Shell.SetTabBarIsVisible(tabContext, false);

                TabBarHelperTool.SetVisibility(false);



                await GoToAsync($"/{nameof(GuidePage)}");
            }
        }
    }
}