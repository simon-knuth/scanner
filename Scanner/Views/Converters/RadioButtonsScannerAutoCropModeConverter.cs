using System;
using Windows.UI.Xaml.Data;

using static Enums;

namespace Scanner.Views.Converters
{
    public class RadioButtonsScannerAutoCropModeConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value - 1;
        }


        /// <summary>
        ///     Converts the given integer into a <see cref="ScannerAutoCropMode"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case -1:
                    return ScannerAutoCropMode.None;
                case 0:
                    return ScannerAutoCropMode.Disabled;
                case 1:
                    return ScannerAutoCropMode.SingleRegion;
                case 2:
                    return ScannerAutoCropMode.MultipleRegions;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to ScannerAutoCropMode.");
            }
        }
    }
}
