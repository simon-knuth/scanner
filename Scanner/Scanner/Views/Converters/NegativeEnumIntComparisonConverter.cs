using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public class NegativeEnumIntComparisonConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum to its int representation, compares it to the given parameter and negates the result.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value != int.Parse((string)parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
