using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class VisibilityOpacityConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="Visibility"/> to an opacity value.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((Visibility)value == Visibility.Visible) return 1.0;
            else return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
