using Scanner.ViewModels;
using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class AspectRatioOptionIntConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((int)value).ToString();
        }


        /// <summary>
        ///     Converts the given integer string into a <see cref="AspectRatioOption"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (AspectRatioOption)int.Parse((string)value);
        }
    }
}
