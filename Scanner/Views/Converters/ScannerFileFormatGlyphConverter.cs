using System;
using Windows.Devices.Scanners;
using Windows.UI.Xaml.Data;

using static Enums;

namespace Scanner.Views.Converters
{
    public class ScannerFileFormatGlyphConverter : IValueConverter
    {
        private const string glyphImage = "\uEB9F";
        private const string glyphPdf = "\uEA90";

        /// <summary>
        ///     Converts the given <see cref="ImageScannerFormat"/> into a glyph string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            ImageScannerFormat format = (ImageScannerFormat)value;

            switch (format)
            {
                case ImageScannerFormat.Jpeg:
                    return glyphImage;
                case ImageScannerFormat.Png:
                    return glyphImage;
                case ImageScannerFormat.DeviceIndependentBitmap:
                    return glyphImage;
                case ImageScannerFormat.Tiff:
                    return glyphImage;
                case ImageScannerFormat.Xps:
                    return glyphImage;
                case ImageScannerFormat.OpenXps:
                    return glyphImage;
                case ImageScannerFormat.Pdf:
                    return glyphPdf;
                default:
                    break;
            }

            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
