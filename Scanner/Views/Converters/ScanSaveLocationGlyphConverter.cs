using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ScanSaveLocationGlyphConverter : IValueConverter
    {
        private const string glyphDefault = "\uE838";
        private const string glyphNotDefault = "\uEC25";

        /// <summary>
        ///     Converts the given bool into a glyph string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isDefault = (bool)value;

            return isDefault ? glyphDefault : glyphNotDefault;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
