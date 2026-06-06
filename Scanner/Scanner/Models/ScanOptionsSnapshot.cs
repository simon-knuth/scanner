using Microsoft.EntityFrameworkCore.Query;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using static Scanner.Helpers.Helpers;

namespace Scanner.Data;

/// <summary>
/// Base for entities that store a snapshot of <see cref="ScanOptions"/>. Provides the
/// columns plus the <see cref="CaptureOptions"/> / <see cref="TryRestoreOptions"/> logic
/// shared between <see cref="KnownScannerEntry"/> and <see cref="TemplateEntry"/>.
/// </summary>
public abstract class ScanOptionsSnapshot
{
    public ScannerSource? SourceMode { get; set; }
    public string FriendlySourceMode => GetFriendlySourceMode();
    public TargetFormat? TargetFormat { get; set; }
    public ScannerColorMode? ColorMode { get; set; }
    public string FriendlyColorMode => GetFriendlyColorMode();

    public float? ResolutionDpiX { get; set; }
    public float? ResolutionDpiY { get; set; }
    public string FriendlyResolution => GetFriendlyResolution();

    public bool? Duplex { get; set; }
    public bool? ScanMultiplePages { get; set; }
    public int? Brightness { get; set; }
    public int? Contrast { get; set; }

    public ScanAreaKind? ScanAreaKind { get; set; }

    // AutoCropArea
    public ScannerAutoCropMode? AutoCropMode { get; set; }

    // PaperSizeArea
    public PaperSize? PaperSize { get; set; }
    public ScanCorner? PaperSizeCorner { get; set; }
    public ScanOrientation? PaperSizeOrientation { get; set; }

    // PreviewSelectionArea
    public double? PreviewSelectionX { get; set; }
    public double? PreviewSelectionY { get; set; }
    public double? PreviewSelectionWidth { get; set; }
    public double? PreviewSelectionHeight { get; set; }

    /// <summary>
    /// Copies a snapshot of <paramref name="options"/> onto this entry.
    /// </summary>
    public void CaptureOptions(ScanOptions options)
    {
        SourceMode = options.SourceMode;
        TargetFormat = options.TargetFormat;
        ColorMode = options.ColorMode;
        Duplex = options.Duplex;
        ScanMultiplePages = options.ScanMultiplePages;
        Brightness = options.Brightness;
        Contrast = options.Contrast;
        ResolutionDpiX = (float?)options.Resolution?.Resolution.DpiX;
        ResolutionDpiY = (float?)options.Resolution?.Resolution.DpiY;

        ClearScanAreaColumns();

        switch (options.ScanArea)
        {
            case AutoCropArea autoCrop:
                ScanAreaKind = Scanner.Data.ScanAreaKind.AutoCrop;
                AutoCropMode = autoCrop.AutoCropMode;
                break;

            case PaperSizeArea paperSize:
                ScanAreaKind = Scanner.Data.ScanAreaKind.PaperSize;
                PaperSize = paperSize.PaperSize;
                PaperSizeCorner = paperSize.Corner;
                PaperSizeOrientation = paperSize.Orientation;
                break;

            case PreviewSelectionArea preview:
                ScanAreaKind = Scanner.Data.ScanAreaKind.PreviewSelection;
                PreviewSelectionX = preview.SelectedRegion.X;
                PreviewSelectionY = preview.SelectedRegion.Y;
                PreviewSelectionWidth = preview.SelectedRegion.Width;
                PreviewSelectionHeight = preview.SelectedRegion.Height;
                break;

            case null:
                ScanAreaKind = null;
                break;
        }
    }

    /// <summary>
    /// Attempts to restore <paramref name="options"/> from the snapshot. Silently skips any field
    /// whose saved value is no longer valid for <paramref name="scanner"/> (e.g. a resolution that
    /// the device no longer reports).
    /// </summary>
    public void TryRestoreOptions(IScanningDevice scanner, ScanOptions options)
    {
        if (SourceMode.HasValue && IsSourceModeAllowed(scanner, SourceMode.Value))
            options.SourceMode = SourceMode.Value;

        ScannerSource effectiveSource = options.SourceMode;

        if (TargetFormat.HasValue)
            options.TargetFormat = TargetFormat.Value;

        if (ColorMode.HasValue && IsColorModeAllowed(scanner, effectiveSource, ColorMode.Value))
            options.ColorMode = ColorMode.Value;

        if (Duplex.HasValue)
        {
            if (!Duplex.Value)
                options.Duplex = false;
            else if (effectiveSource == ScannerSource.Feeder && scanner.IsFeederDuplexSupported)
                options.Duplex = true;
        }

        if (ScanMultiplePages.HasValue)
            options.ScanMultiplePages = ScanMultiplePages.Value;

        if (Brightness.HasValue)
            options.Brightness = Brightness.Value;

        if (Contrast.HasValue)
            options.Contrast = Contrast.Value;

        // resolution: match saved DPI against the device's current resolution list
        if (ResolutionDpiX.HasValue && ResolutionDpiY.HasValue)
        {
            List<ScanResolution> available = effectiveSource switch
            {
                ScannerSource.Flatbed => scanner.FlatbedResolutions,
                ScannerSource.Feeder => scanner.FeederResolutions,
                _ => []
            };

            ScanResolution? match = available.FirstOrDefault(r =>
                Math.Abs(r.Resolution.DpiX - ResolutionDpiX.Value) < 0.5f &&
                Math.Abs(r.Resolution.DpiY - ResolutionDpiY.Value) < 0.5f);

            if (match is not null)
                options.Resolution = match;
        }

        // ScanArea: the Auto source has no selectable area, and an auto-crop area is only valid
        // when the device supports that crop mode for the effective source.
        options.ScanArea = effectiveSource == ScannerSource.Auto ? null : ScanAreaKind switch
        {
            Scanner.Data.ScanAreaKind.AutoCrop when AutoCropMode.HasValue
                                                && IsAutoCropModeAllowed(scanner, effectiveSource, AutoCropMode.Value)
                => new AutoCropArea { AutoCropMode = AutoCropMode.Value },

            Scanner.Data.ScanAreaKind.PaperSize when PaperSize.HasValue
                                                 && PaperSizeCorner.HasValue
                                                 && PaperSizeOrientation.HasValue
                => new PaperSizeArea
                {
                    PaperSize = PaperSize.Value,
                    Corner = PaperSizeCorner.Value,
                    Orientation = PaperSizeOrientation.Value
                },

            Scanner.Data.ScanAreaKind.PreviewSelection when PreviewSelectionX.HasValue
                                                         && PreviewSelectionY.HasValue
                                                         && PreviewSelectionWidth.HasValue
                                                         && PreviewSelectionHeight.HasValue
                => new PreviewSelectionArea(new Rect(
                    PreviewSelectionX.Value,
                    PreviewSelectionY.Value,
                    PreviewSelectionWidth.Value,
                    PreviewSelectionHeight.Value)),

            // null or any incomplete/unsupported kind ~> leave ScanArea as null
            _ => null
        };
    }

    private static bool IsSourceModeAllowed(IScanningDevice scanner, ScannerSource source)
    {
        return source switch
        {
            ScannerSource.Auto => scanner.IsAutoAllowed,
            ScannerSource.Flatbed => scanner.IsFlatbedAllowed,
            ScannerSource.Feeder => scanner.IsFeederAllowed,
            _ => false
        };
    }

    private static bool IsColorModeAllowed(IScanningDevice scanner, ScannerSource source, ScannerColorMode mode)
    {
        return source switch
        {
            ScannerSource.Flatbed => mode switch
            {
                ScannerColorMode.Color => scanner.IsFlatbedColorAllowed,
                ScannerColorMode.Grayscale => scanner.IsFlatbedGrayscaleAllowed,
                ScannerColorMode.Monochrome => scanner.IsFlatbedMonochromeAllowed,
                ScannerColorMode.Automatic => scanner.IsFlatbedAutoColorAllowed,
                ScannerColorMode.None => true,
                _ => false
            },
            ScannerSource.Feeder => mode switch
            {
                ScannerColorMode.Color => scanner.IsFeederColorAllowed,
                ScannerColorMode.Grayscale => scanner.IsFeederGrayscaleAllowed,
                ScannerColorMode.Monochrome => scanner.IsFeederMonochromeAllowed,
                ScannerColorMode.Automatic => scanner.IsFeederAutoColorAllowed,
                ScannerColorMode.None => true,
                _ => false
            },
            // the Auto source carries no explicit color mode
            _ => mode == ScannerColorMode.None
        };
    }

    private static bool IsAutoCropModeAllowed(IScanningDevice scanner, ScannerSource source, ScannerAutoCropMode mode)
    {
        bool single, multi;
        switch (source)
        {
            case ScannerSource.Flatbed:
                single = scanner.IsFlatbedAutoCropSingleRegionAllowed;
                multi = scanner.IsFlatbedAutoCropMultiRegionAllowed;
                break;
            case ScannerSource.Feeder:
                single = scanner.IsFeederAutoCropSingleRegionAllowed;
                multi = scanner.IsFeederAutoCropMultiRegionAllowed;
                break;
            default:
                return false;
        }

        return mode switch
        {
            ScannerAutoCropMode.SingleRegion => single,
            ScannerAutoCropMode.MultipleRegions => multi,
            ScannerAutoCropMode.Disabled => single || multi,
            _ => false
        };
    }

    /// <summary>
    /// Copies every snapshot column from <paramref name="other"/> onto this instance.
    /// </summary>
    public void CopySnapshotFrom(ScanOptionsSnapshot other)
    {
        SourceMode = other.SourceMode;
        TargetFormat = other.TargetFormat;
        ColorMode = other.ColorMode;
        ResolutionDpiX = other.ResolutionDpiX;
        ResolutionDpiY = other.ResolutionDpiY;
        Duplex = other.Duplex;
        ScanMultiplePages = other.ScanMultiplePages;
        Brightness = other.Brightness;
        Contrast = other.Contrast;
        ScanAreaKind = other.ScanAreaKind;
        AutoCropMode = other.AutoCropMode;
        PaperSize = other.PaperSize;
        PaperSizeCorner = other.PaperSizeCorner;
        PaperSizeOrientation = other.PaperSizeOrientation;
        PreviewSelectionX = other.PreviewSelectionX;
        PreviewSelectionY = other.PreviewSelectionY;
        PreviewSelectionWidth = other.PreviewSelectionWidth;
        PreviewSelectionHeight = other.PreviewSelectionHeight;
    }

    private void ClearScanAreaColumns()
    {
        ScanAreaKind = null;
        AutoCropMode = null;
        PaperSize = null;
        PaperSizeCorner = null;
        PaperSizeOrientation = null;
        PreviewSelectionX = null;
        PreviewSelectionY = null;
        PreviewSelectionWidth = null;
        PreviewSelectionHeight = null;
    }

    /// <summary>
    /// Appends setters that null every snapshot column. Intended to be used inside
    /// <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{TSource}(System.Linq.IQueryable{TSource}, System.Action{UpdateSettersBuilder{TSource}}, System.Threading.CancellationToken)"/>.
    /// </summary>
    public static void AddClearSnapshotSetters<T>(UpdateSettersBuilder<T> setters)
        where T : ScanOptionsSnapshot
    {
        setters
            .SetProperty(e => e.SourceMode, (ScannerSource?)null)
            .SetProperty(e => e.TargetFormat, (TargetFormat?)null)
            .SetProperty(e => e.ColorMode, (ScannerColorMode?)null)
            .SetProperty(e => e.ResolutionDpiX, (float?)null)
            .SetProperty(e => e.ResolutionDpiY, (float?)null)
            .SetProperty(e => e.Duplex, (bool?)null)
            .SetProperty(e => e.ScanMultiplePages, (bool?)null)
            .SetProperty(e => e.Brightness, (int?)null)
            .SetProperty(e => e.Contrast, (int?)null)
            .SetProperty(e => e.ScanAreaKind, (ScanAreaKind?)null)
            .SetProperty(e => e.AutoCropMode, (ScannerAutoCropMode?)null)
            .SetProperty(e => e.PaperSize, (PaperSize?)null)
            .SetProperty(e => e.PaperSizeCorner, (ScanCorner?)null)
            .SetProperty(e => e.PaperSizeOrientation, (ScanOrientation?)null)
            .SetProperty(e => e.PreviewSelectionX, (double?)null)
            .SetProperty(e => e.PreviewSelectionY, (double?)null)
            .SetProperty(e => e.PreviewSelectionWidth, (double?)null)
            .SetProperty(e => e.PreviewSelectionHeight, (double?)null);
    }

    private string GetFriendlySourceMode()
    {
        return SourceMode switch
        {
            ScannerSource.Auto => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceAuto),
            ScannerSource.Flatbed => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceFlatbed),
            ScannerSource.Feeder => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceFeeder),
            _ => "",
        };
    }

    private string GetFriendlyColorMode()
    {
        return ColorMode switch
        {
            ScannerColorMode.Color => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ColorModeColor),
            ScannerColorMode.Grayscale => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ColorModeGrayscale),
            ScannerColorMode.Monochrome => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ColorModeMonochrome),
            ScannerColorMode.Automatic => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ColorModeAuto),
            ScannerColorMode.None => "",
            _ => "",
        };
    }

    private string GetFriendlyResolution()
    {
        return string.Format(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ResolutionValue), ResolutionDpiX);
    }
}
