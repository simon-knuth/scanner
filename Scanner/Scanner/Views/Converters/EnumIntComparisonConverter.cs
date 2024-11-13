using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public class EnumIntComparisonConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum to its int representation and compares it to the given parameter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value == int.Parse((string)parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
