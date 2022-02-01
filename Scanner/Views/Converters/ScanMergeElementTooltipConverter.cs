using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ScanMergeElementTooltipConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="ScanMergeElement"/> into a tooltip string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            ScanMergeElement element = value as ScanMergeElement;

            if (element.IsPotentialPage) return element.ItemDescriptor;
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
