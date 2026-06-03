using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class IntNotZeroVisibilityConverter : IValueConverter
{
    /// <summary>
    ///     Converts the given int to a <see cref="Visibility"/> based on it not being zero.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int intValue && intValue != 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}