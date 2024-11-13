using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public class EnumStringConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum to its string representation.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
