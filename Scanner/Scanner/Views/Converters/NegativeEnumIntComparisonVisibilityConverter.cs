using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class NegativeEnumIntComparisonVisibilityConverter : IValueConverter
{
    /// <summary>
    ///     Converts the given enum to its int representation, compares it to the given parameter, negates the result and converts it to a Visibility.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (int)value != int.Parse((string)parameter) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
