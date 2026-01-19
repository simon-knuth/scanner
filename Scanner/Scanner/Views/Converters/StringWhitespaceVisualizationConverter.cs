using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Scanner.Models.ItemNaming;
using System;

namespace Scanner.Views.Converters
{
    public partial class StringWhitespaceVisualizationConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a string to visualize the included whitespace characters.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((string)value).Replace(' ', '⌴');
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}