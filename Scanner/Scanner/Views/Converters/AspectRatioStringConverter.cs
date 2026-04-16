using Microsoft.UI.Xaml.Data;
using Scanner.Helpers;
using System;

namespace Scanner.Views.Converters;

public partial class AspectRatioStringConverter : IValueConverter
{
    /// <summary>
    ///     Converts the given <see cref="AspectRatio"/> to its localized string representation.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        switch ((AspectRatio)value)
        {
            case AspectRatio.Custom:
                return Resources.Strings.Resources.AspectRatioCustom;
            case AspectRatio.Square:
                return Resources.Strings.Resources.AspectRatioSquare;
            case AspectRatio.ThreeByTwo:
                return Resources.Strings.Resources.AspectRatio3By2;
            case AspectRatio.FourByThree:
                return Resources.Strings.Resources.AspectRatio4By3;
            case AspectRatio.DinA:
                return Resources.Strings.Resources.AspectRatioInternationalDINA;
            case AspectRatio.AnsiA:
                return Resources.Strings.Resources.AspectRatioNAANSIA;
            case AspectRatio.AnsiB:
                return Resources.Strings.Resources.AspectRatioNAANSIB;
            case AspectRatio.AnsiC:
                return Resources.Strings.Resources.AspectRatioNAANSIC;
            case AspectRatio.Kai16:
                return Resources.Strings.Resources.AspectRatioChineseKai16;
            case AspectRatio.Kai32:
                return Resources.Strings.Resources.AspectRatioChineseKai32;
            case AspectRatio.Legal:
                return Resources.Strings.Resources.AspectRatioNALegal;
            default:
                return value.ToString();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
