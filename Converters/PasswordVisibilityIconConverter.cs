using System.Globalization;

namespace QuickPrompt.Converters
{
    /// <summary>
    /// Converter for password visibility icon.
    /// Returns "eye" icon when password is hidden, "eye-off" icon when password is visible.
    /// </summary>
    public class PasswordVisibilityIconConverter : IValueConverter
    {
        private const string VisibilityOffIcon = "\ue8f5"; // eye-off (to hide password)
        private const string VisibilityIcon = "\ue8f4";    // eye (to show password)

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                // If password is visible, show "eye-off" icon (to hide it)
                // If password is hidden, show "eye" icon (to show it)
                return isVisible ? VisibilityOffIcon : VisibilityIcon;
            }

            return VisibilityIcon; // Default: visibility (eye)
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("PasswordVisibilityIconConverter does not support two-way binding.");
        }
    }
}
