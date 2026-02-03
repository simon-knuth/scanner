using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public partial class BoolOpacityConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given bool to an opacity value.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool invert = parameter is string stringParameter && stringParameter == "invert";
            return (bool?)value == true ^ invert ? 1.0 : 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
