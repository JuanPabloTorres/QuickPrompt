using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Multi-value converter for suggestion visibility based on focus state and suggestion count.
    /// Returns true if focused AND has suggestions, false otherwise.
    /// </summary>
    public class IsFocusedAndHasSuggestionsConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            bool isFocused = values[0] is bool focused && focused;
            int count = values[1] is int c ? c : 0;

            return isFocused && count > 0;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("IsFocusedAndHasSuggestionsConverter does not support two-way binding.");
        }
    }
}
