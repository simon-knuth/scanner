using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ToolTipConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given element into its ToolTip. null results in an empty string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            object tooltip = ToolTipService.GetToolTip((UIElement)value);
            return (string)tooltip ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
