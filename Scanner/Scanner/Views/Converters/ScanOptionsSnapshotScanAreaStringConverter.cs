   using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Scanner.Data;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.ItemNaming;
using System;
using static Scanner.Helpers.Helpers;

namespace Scanner.Views.Converters;

public partial class ScanOptionsSnapshotScanAreaStringConverter : IValueConverter
{
    /// <summary>
    ///     Converts a <see cref="ScanOptionsSnapshot"/> to a scan area representation.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ScanOptionsSnapshot snapshot = (ScanOptionsSnapshot)value;
        
        ScanAreaKind? kind = snapshot.ScanAreaKind;
        switch (kind)
        {
            case null:
                return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanAreaEverything);
            case ScanAreaKind.AutoCrop:
                return snapshot.AutoCropMode switch
                {
                    ScannerAutoCropMode.Disabled => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AutoCropDisabled),
                    ScannerAutoCropMode.SingleRegion => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AutoCropSingle),
                    ScannerAutoCropMode.MultipleRegions => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AutoCropMultiple),
                    _ => "",
                };
            case ScanAreaKind.PaperSize:
                return snapshot.PaperSize switch
                {
                    PaperSize.DinA3 => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.PaperSizeInternationalDINA3),
                    PaperSize.DinA4 => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.PaperSizeInternationalDINA4),
                    PaperSize.DinA5 => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.PaperSizeInternationalDINA5),
                    PaperSize.AnsiA => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AspectRatioNAANSIA),
                    PaperSize.AnsiB => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AspectRatioNAANSIB),
                    PaperSize.AnsiC => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AspectRatioNAANSIC),
                    PaperSize.Legal => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.AspectRatioNALegal),
                    _ => "",
                };
            case ScanAreaKind.PreviewSelection:
                return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanAreaPreviewSelection);
            case ScanAreaKind.None:
            default:
                return "";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}