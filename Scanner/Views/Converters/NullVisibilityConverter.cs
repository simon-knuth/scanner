using System;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml;

namespace Scanner.Views.Converters
{
    public class NullVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given object into a <see cref="Visibility"/> based on it equaling null.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Visible;
            else return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
