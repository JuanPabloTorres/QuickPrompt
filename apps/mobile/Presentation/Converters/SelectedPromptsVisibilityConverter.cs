using QuickPrompt.Models;
using QuickPrompt.ViewModels.Prompts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts selected prompts count to boolean visibility.
    /// Returns true if at least one prompt is selected, false otherwise.
    /// </summary>
    public class SelectedPromptsVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int selectedPromptsCount)
            {
                return selectedPromptsCount >= 1;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("SelectedPromptsVisibilityConverter does not support two-way binding.");
        }
    }
}
