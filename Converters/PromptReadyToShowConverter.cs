using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class PromptReadyToShowConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            bool isLoading = values[0] is bool b && b;

            string promptText = values[1] as string;

            return !isLoading && !string.IsNullOrWhiteSpace(promptText);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
