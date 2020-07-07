using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class HeaderScrollShadowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double offset = (double)value;
            double maxOpacity = 1;

            double a = offset - 5;

            if (a <= 0) return 0;
            else if (a >= 20) return maxOpacity;
            else return maxOpacity * a / 20;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
