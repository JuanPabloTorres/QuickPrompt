using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class FinalPromptAndNotLoadingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var finalPrompt = values[0] as string;

            var isLoading = values[1] is bool loading && loading;

            if (string.IsNullOrWhiteSpace(finalPrompt))
                return false;

            return !isLoading;
        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

}
