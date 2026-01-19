using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converter for password visibility icon.
    /// Returns "eye" icon when password is hidden, "eye-off" icon when password is visible.
    /// </summary>
    public class PasswordVisibilityIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                // If password is visible, show "eye-off" icon (to hide it)
                // If password is hidden, show "eye" icon (to show it)
                return isVisible ? "\ue8f5" : "\ue8f4"; // visibility_off : visibility
            }

            return "\ue8f4"; // Default: visibility (eye)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
