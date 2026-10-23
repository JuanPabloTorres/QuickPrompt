using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts boolean favorite status to Material Design star icon glyph.
    /// Returns filled star (★) for true, empty star (☆) for false.
    /// </summary>
    public class BooleanToStarIconConverter : IValueConverter
    {
        private const string GlyphIconNotFavorite = "\ue3e6"; // Material Icons Outlined - Empty Star
        private const string GlyphIconFavorite = "\ue3e7";    // Material Icons Outlined - Filled Star

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                return isFavorite ? GlyphIconFavorite : GlyphIconNotFavorite;
            }
            
            return GlyphIconNotFavorite; // Default to empty star if value is not boolean
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BooleanToStarIconConverter does not support two-way binding.");
        }
    }
}