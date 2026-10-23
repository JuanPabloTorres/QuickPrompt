using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts string equality comparison to boolean.
    /// Returns true if value equals parameter, false otherwise.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? parameter?.ToString() : Binding.DoNothing;
        }
    }
}
