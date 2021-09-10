using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class NotNullBoolConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given object into a bool based on it not equaling null.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
