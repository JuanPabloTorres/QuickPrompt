using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts string to boolean visibility.
    /// Returns true if the string is not null or empty, false otherwise.
    /// </summary>
    public class FinalPromptVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FinalPromptVisibilityConverter does not support two-way binding.");
        }
    }
}