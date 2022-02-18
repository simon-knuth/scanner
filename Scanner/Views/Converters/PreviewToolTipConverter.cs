using System;
using Windows.UI.Xaml.Data;
using static Utilities;

namespace Scanner.Views.Converters
{
    public class PreviewToolTipConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="object"/> into a tooltip string. If the object is null,
        ///     there is no scan region selected. If the object isn't null, a custom region is selected.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                // no region selected
                return LocalizedString("TextButtonPreviewToolTipNoRegion");
            }
            else
            {
                // region selected
                return LocalizedString("TextButtonPreviewToolTipRegion");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
