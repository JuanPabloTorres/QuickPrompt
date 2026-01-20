using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Multi-value converter for button visibility based on loading state and item count.
    /// Returns true if not loading AND count > 0, false otherwise.
    /// </summary>
    public class IsButtonVisibleConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return false;

            bool isLoading = values[0] is bool b && b;
            int count = values[1] is int c ? c : 0;

            return !isLoading && count > 0;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("IsButtonVisibleConverter does not support two-way binding.");
        }
    }
}
