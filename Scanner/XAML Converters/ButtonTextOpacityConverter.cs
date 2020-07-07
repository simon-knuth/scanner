using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class ButtonTextOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool buttonEnabled = (bool) value;

            if (buttonEnabled) return 1;
            else return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
