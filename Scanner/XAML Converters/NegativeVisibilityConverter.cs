using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class NegativeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((Visibility)value == Visibility.Visible) return Visibility.Collapsed;
            else return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
