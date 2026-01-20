using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts a string to boolean indicating whether it's not null or empty.
    /// Returns true if the string has content, false otherwise.
    /// </summary>
    public class StringNotNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value?.ToString());
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringNotNullOrEmptyConverter does not support two-way binding.");
        }
    }
}
