using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class FilterToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentFilter = value?.ToString();
            var buttonFilter = parameter?.ToString();

            // ✅ Use Design System tokens
            var activeColor = (Color)Application.Current.Resources["PrimaryBlueDark"];
            var inactiveColor = (Color)Application.Current.Resources["Gray300"];

            return currentFilter == buttonFilter ? activeColor : inactiveColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
