using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public class NegativeBoolConverter : IValueConverter
    {
        /// <summary>
        ///     Inverts the given bool value.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && (bool)value == true) return false;
            else return true;
        }

        /// <summary>
        ///     Inverts the given bool value.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if ((bool)value == true) return false;
            else return true;
        }
    }
}
