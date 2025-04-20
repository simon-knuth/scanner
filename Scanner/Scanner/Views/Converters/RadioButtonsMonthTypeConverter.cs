using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Scanner.Models.FileNaming;
using System;

namespace Scanner.Views.Converters
{
    public partial class RadioButtonsMonthTypeConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value;
        }


        /// <summary>
        ///     Converts the given integer into a <see cref="MonthType"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case -1:
                    return MonthType.Number;
                case 0:
                    return MonthType.Number;
                case 1:
                    return MonthType.Name;
                case 2:
                    return MonthType.ShortName;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to MonthType.");
            }
        }
    }
}