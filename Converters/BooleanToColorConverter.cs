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
        // Colores estáticos para mejorar el rendimiento y evitar creaciones innecesarias
        private static readonly Color ColorFavorite = Color.FromArgb("#EFB036"); // Amarillo (PrimaryYellow)

        private static readonly Color ColorNotFavorite = Color.FromArgb("#E6E6E6"); // Negro (AppBlack)

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Verificar si el valor es un booleano válido
            if (value is bool isFavorite)
            {
                return isFavorite ? ColorFavorite : ColorNotFavorite;
            }

            // Retornar negro por defecto si el valor no es un booleano
            return ColorNotFavorite;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // No se requiere conversión inversa
        }
    }

}
