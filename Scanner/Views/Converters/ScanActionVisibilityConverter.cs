using Scanner.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ScanActionVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Compares the parameter int to the given <see cref="ScanAction"/> and returns
        ///     a corresponding <see cref="Visibility"/>.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((ScanAction)value == (ScanAction)int.Parse((string)parameter))
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }


        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
