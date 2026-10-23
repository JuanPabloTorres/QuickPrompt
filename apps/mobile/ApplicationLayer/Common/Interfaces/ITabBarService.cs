namespace QuickPrompt.ApplicationLayer.Common.Interfaces;

/// <summary>
/// Service for controlling tab bar visibility.
/// Replaces static TabBarHelperTool.
/// </summary>
public interface ITabBarService
{
    /// <summary>
    /// Sets the visibility of the tab bar.
    /// </summary>
    /// <param name="isVisible">True to show the tab bar, false to hide it.</param>
    void SetVisibility(bool isVisible);
}
