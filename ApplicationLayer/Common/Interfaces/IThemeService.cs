using Microsoft.Maui.Graphics;

namespace QuickPrompt.ApplicationLayer.Common.Interfaces;

/// <summary>
/// Service for accessing theme resources (colors, brushes) in a testable, safe way.
/// ? PHASE 4: Eliminates hardcoded colors and provides fallback mechanism.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets a color from the current theme with fallback.
    /// </summary>
    /// <param name="key">Resource key (e.g., "PrimaryColor", "Gray400")</param>
    /// <param name="fallback">Fallback color if key not found (optional)</param>
    /// <returns>Color from theme or fallback</returns>
    Color GetColor(string key, Color? fallback = null);

    /// <summary>
    /// Gets a brush from the current theme with fallback.
    /// </summary>
    /// <param name="key">Resource key</param>
    /// <param name="fallback">Fallback brush if key not found (optional)</param>
    /// <returns>Brush from theme or fallback</returns>
    Brush GetBrush(string key, Brush? fallback = null);

    /// <summary>
    /// Tries to get a color from the current theme.
    /// </summary>
    /// <param name="key">Resource key</param>
    /// <param name="color">Output color if found</param>
    /// <returns>True if color was found, false otherwise</returns>
    bool TryGetColor(string key, out Color color);

    /// <summary>
    /// Checks if a color resource exists in the current theme.
    /// </summary>
    /// <param name="key">Resource key</param>
    /// <returns>True if resource exists, false otherwise</returns>
    bool HasColorResource(string key);

    /// <summary>
    /// Gets the current theme mode (Light or Dark).
    /// </summary>
    /// <returns>Current app theme</returns>
    AppTheme GetCurrentTheme();

    /// <summary>
    /// Sets the application theme.
    /// </summary>
    /// <param name="theme">Theme to apply</param>
    void SetTheme(AppTheme theme);
}
