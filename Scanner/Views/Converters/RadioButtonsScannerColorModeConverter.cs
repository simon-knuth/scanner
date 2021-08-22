using System;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

using static Enums;

namespace Scanner.Views.Converters
{
    public class RadioButtonsScannerColorModeConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value - 1;
        }


        /// <summary>
        ///     Converts the given integer into a <see cref="ScannerColorMode"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case -1:
                    return ScannerColorMode.None;
                case 0:
                    return ScannerColorMode.Color;
                case 1:
                    return ScannerColorMode.Grayscale;
                case 2:
                    return ScannerColorMode.Monochrome;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to ScannerColorMode.");
            }
        }
    }
}
