using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public class NullBoolConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given object into a bool based on it equaling null.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}