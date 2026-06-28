using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class IntComparisonConverter : IValueConverter
{
    /// <summary>
    ///     Compares the given int to the parameter.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int intValue && int.TryParse((string?)parameter, out int baseValue) && intValue == baseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}