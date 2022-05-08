using Scanner.Models.FileNaming;
using System;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class RadioButtonsDayTypeConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value;
        }


        /// <summary>
        ///     Converts the given integer into a <see cref="DayType"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case -1:
                    return DayType.DayOfMonth;
                case 0:
                    return DayType.DayOfWeek;
                case 1:
                    return DayType.DayOfMonth;
                case 2:
                    return DayType.DayOfYear;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to DayType.");
            }
        }
    }
}
