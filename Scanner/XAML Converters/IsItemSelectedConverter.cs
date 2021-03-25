using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class IsItemSelectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((int)value > -1) return true;
            else return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
