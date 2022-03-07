using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ScanMergeElementGlyphConverter : IValueConverter
    {
        private const string glyphStartPage = "\uE819";
        private const string glyphSinglePage = "\uE160";
        private const string glyphMultiplePages = "\uE10C";

        /// <summary>
        ///     Converts the given <see cref="ScanMergeElement"/> into a glyph string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            ScanMergeElement element = value as ScanMergeElement;

            if (element.IsStartPage) return glyphStartPage;
            else if (!element.IsPlaceholderForMultiplePages) return glyphSinglePage;
            else return glyphMultiplePages;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
