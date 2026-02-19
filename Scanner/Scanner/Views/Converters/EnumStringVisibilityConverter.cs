using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class EnumStringVisibilityConverter : IValueConverter
{
    /// <summary>
    ///     Compares the given enum to the string parameter and returns a Visibility.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return ((Enum)value).ToString() == (string)parameter ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
