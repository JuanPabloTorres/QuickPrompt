using Microsoft.Maui.Graphics;
using QuickPrompt.ApplicationLayer.Common.Interfaces;

namespace QuickPrompt.Infrastructure.Services.UI;

/// <summary>
/// Implementation of IThemeService for accessing theme resources safely.
/// ? PHASE 4: Provides testable, crash-safe access to theme colors and brushes.
/// ? UX IMPROVEMENTS: Added Dark Mode support with persistence.
/// </summary>
public class ThemeService : IThemeService
{
    private const string ThemePreferenceKey = "app_theme_preference";
    
    // Default fallback colors
    private static readonly Color DefaultPrimary = Color.FromArgb("#512BD4");
    private static readonly Color DefaultSecondary = Color.FromArgb("#DFD8F7");
    private static readonly Color DefaultBackground = Colors.White;
    private static readonly Color DefaultText = Colors.Black;

    public Color GetColor(string key, Color? fallback = null)
    {
        try
        {
            if (TryGetColor(key, out var color))
            {
                return color;
            }

            // Use provided fallback or default
            return fallback ?? GetDefaultColor(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error getting color '{key}': {ex.Message}");
            return fallback ?? GetDefaultColor(key);
        }
    }

    public Brush GetBrush(string key, Brush? fallback = null)
    {
        try
        {
            if (Application.Current?.Resources != null &&
                Application.Current.Resources.TryGetValue(key, out var resource))
            {
                if (resource is Brush brush)
                {
                    return brush;
                }

                // If it's a Color, convert to SolidColorBrush
                if (resource is Color color)
                {
                    return new SolidColorBrush(color);
                }
            }

            // Use provided fallback or create from color
            return fallback ?? new SolidColorBrush(GetColor(key));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error getting brush '{key}': {ex.Message}");
            return fallback ?? new SolidColorBrush(GetDefaultColor(key));
        }
    }

    public bool TryGetColor(string key, out Color color)
    {
        color = Colors.Transparent;

        try
        {
            if (Application.Current?.Resources == null)
            {
                return false;
            }

            if (Application.Current.Resources.TryGetValue(key, out var resource))
            {
                if (resource is Color resourceColor)
                {
                    color = resourceColor;
                    return true;
                }

                // Try to convert string to color
                if (resource is string colorString)
                {
                    color = Color.FromArgb(colorString);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error in TryGetColor '{key}': {ex.Message}");
            return false;
        }
    }

    public bool HasColorResource(string key)
    {
        try
        {
            if (Application.Current?.Resources == null)
            {
                return false;
            }

            return Application.Current.Resources.ContainsKey(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error checking resource '{key}': {ex.Message}");
            return false;
        }
    }

    public AppTheme GetCurrentTheme()
    {
        try
        {
            return Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error getting theme: {ex.Message}");
            return AppTheme.Unspecified;
        }
    }

    public void SetTheme(AppTheme theme)
    {
        try
        {
            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = theme;
                
                // Persist theme preference
                Preferences.Set(ThemePreferenceKey, (int)theme);
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Theme changed to: {theme}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error setting theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the saved theme preference from storage.
    /// Should be called on app startup.
    /// </summary>
    public void LoadSavedTheme()
    {
        try
        {
            if (Preferences.ContainsKey(ThemePreferenceKey))
            {
                var savedTheme = (AppTheme)Preferences.Get(ThemePreferenceKey, (int)AppTheme.Unspecified);
                SetTheme(savedTheme);
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Loaded saved theme: {savedTheme}");
            }
            else
            {
                // First run - use system preference
                SetTheme(AppTheme.Unspecified);
                System.Diagnostics.Debug.WriteLine("[ThemeService] No saved theme, using system preference");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error loading saved theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the effective theme (resolves Unspecified to actual Light/Dark).
    /// </summary>
    public AppTheme GetEffectiveTheme()
    {
        try
        {
            var userTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
            
            if (userTheme == AppTheme.Unspecified)
            {
                // Return system theme
                return Application.Current?.RequestedTheme ?? AppTheme.Light;
            }
            
            return userTheme;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error getting effective theme: {ex.Message}");
            return AppTheme.Light;
        }
    }

    /// <summary>
    /// Gets a default color based on the key name (smart fallback).
    /// </summary>
    private static Color GetDefaultColor(string key)
    {
        // Smart defaults based on common naming patterns
        if (key.Contains("Primary", StringComparison.OrdinalIgnoreCase))
            return DefaultPrimary;

        if (key.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
            return DefaultSecondary;

        if (key.Contains("Background", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("White", StringComparison.OrdinalIgnoreCase))
            return DefaultBackground;

        if (key.Contains("Text", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Black", StringComparison.OrdinalIgnoreCase))
            return DefaultText;

        if (key.Contains("Gray", StringComparison.OrdinalIgnoreCase))
        {
            // Extract gray level from key (e.g., "Gray400" ? 400)
            var numbers = new string(key.Where(char.IsDigit).ToArray());
            if (int.TryParse(numbers, out var level))
            {
                // Map 0-900 to grayscale
                var normalized = Math.Clamp(level / 900.0, 0.0, 1.0);
                var grayValue = (int)(normalized * 255);
                return Color.FromRgb(grayValue, grayValue, grayValue);
            }

            return Color.FromRgb(128, 128, 128); // Default gray
        }

        // Ultimate fallback
        System.Diagnostics.Debug.WriteLine($"[ThemeService] Using default primary for unknown key: {key}");
        return DefaultPrimary;
    }
}
