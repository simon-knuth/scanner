using System;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

using static Enums;

namespace Scanner.Views.Converters
{
    public class RadioButtonsScannerSourceConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value - 1;
        }


        /// <summary>
        ///     Converts the given integer into a <see cref="ScannerSource"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case -1:
                    return ScannerSource.None;
                case 0:
                    return ScannerSource.Auto;
                case 1:
                    return ScannerSource.Flatbed;
                case 2:
                    return ScannerSource.Feeder;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to ScannerSource.");
            }
        }
    }
}
