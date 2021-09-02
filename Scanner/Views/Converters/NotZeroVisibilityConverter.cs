using System;
using System.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class NotZeroVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given int into the corresponding <see cref="Visibility"/> based on it
        ///     not equaling zero.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value != 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
