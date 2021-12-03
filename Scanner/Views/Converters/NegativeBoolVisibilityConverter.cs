using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class NegativeBoolVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Inverts the given bool value and converts it to a <see cref="Visibility"/>.
        ///     Also returns <see cref="Visibility.Collapsed"/> if null is received.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || (bool)value == true) return Visibility.Collapsed;
            else return Visibility.Visible;
        }

        /// <summary>
        ///     Inverts the given <see cref="Visibility"/> value and converts it to a bool.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if ((Visibility)value == Visibility.Visible) return false;
            else return true;
        }
    }
}
