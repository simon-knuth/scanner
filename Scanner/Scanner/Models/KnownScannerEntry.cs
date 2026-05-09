using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace Scanner.Data;

/// <summary>
/// <see cref="IScanningDevice"/> that's been used at least once.
/// </summary>
public class KnownScannerEntry
{
    /// <summary>Maps to <see cref="IScanningDevice.Id"/>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last successful scan. Used to rank preferred scanners.</summary>
    public DateTime LastUsed { get; set; }

    public ScannerSource? LastSourceMode { get; set; }
    public TargetFormat? LastTargetFormat { get; set; }
    public ScannerColorMode? LastColorMode { get; set; }

    public float? LastResolutionDpiX { get; set; }
    public float? LastResolutionDpiY { get; set; }

    public bool? LastDuplex { get; set; }
    public bool? LastScanMultiplePages { get; set; }
    public int? LastBrightness { get; set; }
    public int? LastContrast { get; set; }

    public ScanAreaKind? LastScanAreaKind { get; set; }

    // AutoCropArea
    public ScannerAutoCropMode? LastAutoCropMode { get; set; }

    // PaperSizeArea
    public PaperSize? LastPaperSize { get; set; }
    public ScanCorner? LastPaperSizeCorner { get; set; }
    public ScanOrientation? LastPaperSizeOrientation { get; set; }

    // PreviewSelectionArea
    public double? LastPreviewSelectionX { get; set; }
    public double? LastPreviewSelectionY { get; set; }
    public double? LastPreviewSelectionWidth { get; set; }
    public double? LastPreviewSelectionHeight { get; set; }

    /// <summary>
    /// Attempts to restore <paramref name="options"/> from a <see cref="KnownScannerEntry"/>. Silently skips any field
    /// whose saved value is no longer valid for <paramref name="scanner"/> (e.g. a resolution that the device no longer
    /// reports).
    /// </summary>
    public void TryRestoreOptions(IScanningDevice scanner, ScanOptions options)
    {
        if (LastSourceMode.HasValue)
            options.SourceMode = LastSourceMode.Value;

        if (LastTargetFormat.HasValue)
            options.TargetFormat = LastTargetFormat.Value;

        if (LastColorMode.HasValue)
            options.ColorMode = LastColorMode.Value;

        if (LastDuplex.HasValue)
            options.Duplex = LastDuplex.Value;

        if (LastScanMultiplePages.HasValue)
            options.ScanMultiplePages = LastScanMultiplePages.Value;

        if (LastBrightness.HasValue)
            options.Brightness = LastBrightness.Value;

        if (LastContrast.HasValue)
            options.Contrast = LastContrast.Value;

        // resolution: match saved DPI against the device's current resolution list
        if (LastResolutionDpiX.HasValue && LastResolutionDpiY.HasValue)
        {
            List<ScanResolution> available = options.SourceMode switch
            {
                ScannerSource.Flatbed => scanner.FlatbedResolutions,
                ScannerSource.Feeder => scanner.FeederResolutions,
                _ => []
            };

            ScanResolution? match = available.FirstOrDefault(r =>
                Math.Abs(r.Resolution.DpiX - LastResolutionDpiX.Value) < 0.5f &&
                Math.Abs(r.Resolution.DpiY - LastResolutionDpiY.Value) < 0.5f);

            if (match is not null)
                options.Resolution = match;
        }

        // ScanArea
        options.ScanArea = LastScanAreaKind switch
        {
            ScanAreaKind.AutoCrop when LastAutoCropMode.HasValue
                => new AutoCropArea { AutoCropMode = LastAutoCropMode.Value },

            ScanAreaKind.PaperSize when LastPaperSize.HasValue
                                     && LastPaperSizeCorner.HasValue
                                     && LastPaperSizeOrientation.HasValue
                => new PaperSizeArea
                {
                    PaperSize = LastPaperSize.Value,
                    Corner = LastPaperSizeCorner.Value,
                    Orientation = LastPaperSizeOrientation.Value
                },

            ScanAreaKind.PreviewSelection when LastPreviewSelectionX.HasValue
                                           && LastPreviewSelectionY.HasValue
                                           && LastPreviewSelectionWidth.HasValue
                                           && LastPreviewSelectionHeight.HasValue
                => new PreviewSelectionArea(new Rect(
                    LastPreviewSelectionX.Value,
                    LastPreviewSelectionY.Value,
                    LastPreviewSelectionWidth.Value,
                    LastPreviewSelectionHeight.Value)),

            // null or any incomplete/unknown kind ~> leave ScanArea as null
            _ => null
        };
    }
}

public enum ScanAreaKind
{
    None = 0,
    AutoCrop = 1,
    PaperSize = 2,
    PreviewSelection = 3
}