using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class NotNullBoolConverter : IValueConverter
{
    /// <summary>
    ///     Converts the given object into a bool based on it not equaling null.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}