using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ScanSaveLocationGlyphConverter : IValueConverter
    {
        private const string glyphDefault = "\uE838";
        private const string glyphNotDefault = "\uEC25";
        private const string glyphAskForLocation = "\uE81C";

        /// <summary>
        ///     Converts the given bool into a glyph string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool? isDefault = (bool?)value;

            if (isDefault == true) return glyphDefault;
            else if (isDefault == false) return glyphNotDefault;
            else return glyphAskForLocation;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
