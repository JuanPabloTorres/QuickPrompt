using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Multi-value converter for prompt ready state based on loading and content.
    /// Returns true if not loading AND prompt text has content, false otherwise.
    /// </summary>
    public class PromptReadyToShowConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            bool isLoading = values[0] is bool b && b;
            string? promptText = values[1] as string;

            return !isLoading && !string.IsNullOrWhiteSpace(promptText);
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("PromptReadyToShowConverter does not support two-way binding.");
        }
    }
}
