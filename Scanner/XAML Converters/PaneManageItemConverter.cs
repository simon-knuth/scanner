using System;
using Windows.UI.Xaml.Data;

namespace Scanner
{
    public class PaneManageItemConverter : IValueConverter
    {
        // assumption:
        //  desired item size ranges from 125 to 150
        //  width of pane ranges from 300 to 350

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double input, result;
            input = (double)value;

            result = 125 + (input - 300) / 2;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
