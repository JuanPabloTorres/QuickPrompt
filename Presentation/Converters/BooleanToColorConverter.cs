using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        // ✅ Use Design System tokens for colors
        private static Color ColorFavorite => (Color)Application.Current.Resources["PrimaryYellow"];
        private static Color ColorNotFavorite => (Color)Application.Current.Resources["Gray300"];

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Verificar si el valor es un booleano válido
            if (value is bool isFavorite)
            {
                return isFavorite ? ColorFavorite : ColorNotFavorite;
            }

            // Retornar gris por defecto si el valor no es un booleano
            return ColorNotFavorite;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // No se requiere conversión inversa
        }
    }

}
