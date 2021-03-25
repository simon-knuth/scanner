using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class NegativeThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Thickness result = (Thickness)value;
            result.Left *= -1;
            result.Top *= -1;
            result.Right *= -1;
            result.Bottom *= -1;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
