using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converts filter comparison to color.
    /// Returns PrimaryBlueDark if the current filter matches the button filter, Gray300 otherwise.
    /// </summary>
    public class FilterToColorConverter : IValueConverter
    {
        // ✅ Use Design System tokens
        private static Color ActiveColor => (Color)Application.Current!.Resources["PrimaryBlueDark"];
        private static Color InactiveColor => (Color)Application.Current!.Resources["Gray300"];

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var currentFilter = value?.ToString();
            var buttonFilter = parameter?.ToString();

            return currentFilter == buttonFilter ? ActiveColor : InactiveColor;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FilterToColorConverter does not support two-way binding.");
        }
    }
}
