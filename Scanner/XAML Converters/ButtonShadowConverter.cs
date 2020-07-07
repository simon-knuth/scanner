using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class ButtonShadowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double shadowOpacity = 0.2;

            if ((bool)value == true) return shadowOpacity;
            else return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
