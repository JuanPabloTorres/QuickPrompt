using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts boolean favorite status to color.
    /// Returns PrimaryYellow for true (favorite), Gray300 for false (not favorite).
    /// </summary>
    public class BooleanToColorConverter : IValueConverter
    {
        // ✅ Use Design System tokens for colors
        private static Color ColorFavorite => (Color)Application.Current!.Resources["PrimaryYellow"];
        private static Color ColorNotFavorite => (Color)Application.Current!.Resources["Gray300"];

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                return isFavorite ? ColorFavorite : ColorNotFavorite;
            }

            // Return gray by default if value is not a boolean
            return ColorNotFavorite;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BooleanToColorConverter does not support two-way binding.");
        }
    }
}
