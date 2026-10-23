using System;
using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Multi-value converter for final prompt visibility based on content and loading state.
    /// Returns true if final prompt has content AND not loading, false otherwise.
    /// </summary>
    public class FinalPromptAndNotLoadingConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return false;

            var finalPrompt = values[0] as string;
            var isLoading = values[1] is bool loading && loading;

            if (string.IsNullOrWhiteSpace(finalPrompt))
                return false;

            return !isLoading;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FinalPromptAndNotLoadingConverter does not support two-way binding.");
        }
    }
}
