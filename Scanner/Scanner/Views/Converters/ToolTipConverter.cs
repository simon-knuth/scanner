using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters
{
    public partial class ToolTipConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given element into its ToolTip. A null value results in an empty string.
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