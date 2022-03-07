using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class DoubleStringConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="double"/> to a string with the decimal places given as parameter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double number = (double)value;
            int places = int.Parse((string)parameter);

            string placesConfig = "";
            for (int i = 0; i < places; i++)
            {
                placesConfig += "0";
            }
            return number.ToString($"0.{placesConfig}");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string number = (string)value;
            return double.Parse(number);
        }
    }
}
