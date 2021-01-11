using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Scanner
{
    public class PaneManageItemDescriptorBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = (bool)value;
            if (isSelected) return (Brush)Application.Current.Resources["SystemControlAccentAcrylicElementAccentMediumHighBrush"];
            else return (Brush)Application.Current.Resources["SystemControlAcrylicElementMediumHighBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
