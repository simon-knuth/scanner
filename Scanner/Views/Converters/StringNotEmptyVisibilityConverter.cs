using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converter
{
    public class StringNotEmptyVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a string to a visibility, which is useful for hiding parts of the UI when
        ///     no text is available.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!string.IsNullOrEmpty((string)value)) return Visibility.Visible;
            else return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
