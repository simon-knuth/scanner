using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class SliderThumbToolTipPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double absoluteValue = (double)value;

            return Math.Floor(absoluteValue * 100) + " %";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
