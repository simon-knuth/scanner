using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class StringWhitespaceVisualizationConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a string to visualize the included whitespace characters.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string text = value as string;
            return text.Replace(' ', '⌴');
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
