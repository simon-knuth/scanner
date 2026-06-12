using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Scanner.Views.Converters;

public partial class BoolOnAccentForegroundConverter : IValueConverter
{
    /// <summary>
    ///     Converts the given bool to the on-accent foreground brush when true,
    ///     or the disabled foreground brush when false.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string key = (bool?)value == true ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorDisabledBrush";
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
