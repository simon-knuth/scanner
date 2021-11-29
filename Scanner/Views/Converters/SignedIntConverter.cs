using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class SignedIntConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given int into a signed number string. "0" will always remain unsigned.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((double)value > 0)
            {
                return (-(double)value).ToString().Replace('-', '+');
            }
            else
            {
                return ((double)value).ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
