using QuickPrompt.ApplicationLayer.Common.Interfaces;

namespace QuickPrompt.Infrastructure.Services.UI;

/// <summary>
/// Service for controlling tab bar visibility.
/// Replaces static TabBarHelperTool.
/// </summary>
public class TabBarService : ITabBarService
{
    public void SetVisibility(bool isVisible)
    {
        var tabContext = Shell.Current?.CurrentItem?.CurrentItem;

        if (tabContext != null)
        {
            Shell.SetTabBarIsVisible(tabContext, isVisible);
        }
    }
}
