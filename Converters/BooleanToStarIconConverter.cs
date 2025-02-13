using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class BooleanToStarIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Unicode para estrella vacía (☆) y estrella llena (★)
            string glyphIconNotFavorite = "\ue836"; // Material Icons Outlined - Empty Star

            string glyphIconFavorite = "\ue2e6";   // Material Icons Outlined - Filled Star


            if (value is bool isFavorite)
            {
                var val= isFavorite ? glyphIconFavorite : glyphIconNotFavorite; // Filled star or empty star

                return val;
            }
            return glyphIconNotFavorite; // Default to empty star if value is not boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}