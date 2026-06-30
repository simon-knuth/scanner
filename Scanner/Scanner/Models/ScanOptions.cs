using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Scanner.Models.Interfaces;
using static Scanner.Helpers.Helpers;
using Scanner.Services.Interfaces;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Tests;
using Scanner.Data;

namespace Scanner.Models;

public partial class ScanOptions : ObservableObject
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////                
    [ObservableProperty]
    private IScanningDevice? scanner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortFriendlySourceMode))]
    private ScannerSource sourceMode;

    public string ShortFriendlySourceMode => GetShortFriendlySourceMode();

    [ObservableProperty]
    private TargetFormat targetFormat;

    [ObservableProperty]
    private ScannerColorMode colorMode;

    [ObservableProperty]
    private ScanResolution resolution;

    [ObservableProperty]
    private ScanArea? scanArea;

    [ObservableProperty]
    private bool duplex;

    [ObservableProperty]
    private bool scanMultiplePages;

    [ObservableProperty]
    private int brightness = AppConfig.DefaultBrightness;

    [ObservableProperty]
    private int contrast = AppConfig.DefaultContrast;

    public ScanMergeConfig? ScanMergeConfig { get; set; }

    public DateTime ScanTime { get; set; } = DateTime.MinValue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanOptions(IScanningDevice? scanner, ScannerSource? forceSourceMode = null)
    {
        SetScanOptionsForScanner(scanner, forceSourceMode);
    }

    public static async Task<ScanOptions> CreateAndRestoreFromDatabaseAsync(IScanningDevice? scanner, ScannerSource? forceSourceMode = null)
    {
        ScanOptions scanOptions = new(scanner, forceSourceMode);

        KnownScannerEntry? entry = null;
        if (scanner != null)
        {
            IKnownScannersService knownScannersService = Ioc.Default.GetRequiredService<IKnownScannersService>();
            entry = await knownScannersService.GetEntryAsync(scanner.Id);

            if (entry != null)
                entry.TryRestoreOptions(scanner, scanOptions);
        }

        return scanOptions;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ImageScannerScanSource GetSourceModeForScanning()
    {
        switch (SourceMode)
        {
            case ScannerSource.Auto:
                return ImageScannerScanSource.AutoConfigured;
            case ScannerSource.Flatbed:
                return ImageScannerScanSource.Flatbed;
            case ScannerSource.Feeder:
                return ImageScannerScanSource.Feeder;
            case ScannerSource.None:
            default:
                throw new ArgumentException(String.Format("Can't convert {0} to ImageScannerScanSource.", SourceMode));
        }
    }

    public ImageScannerColorMode GetColorModeForScanning()
    {
        switch (ColorMode)
        {
            case ScannerColorMode.Color:
                switch (SourceMode)
                {
                    case ScannerSource.Flatbed:
                        if (Scanner.IsFlatbedColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Flatbed doesn't support color");
                    case ScannerSource.Feeder:
                        if (Scanner.IsFeederColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Feeder doesn't support color");
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        throw new ArgumentException("Can't get color mode for source mode " + SourceMode);
                }
            case ScannerColorMode.Grayscale:
                switch (SourceMode)
                {
                    case ScannerSource.Flatbed:
                        if (Scanner.IsFlatbedGrayscaleAllowed)
                            return ImageScannerColorMode.Grayscale;
                        else if (Scanner.IsFlatbedColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Flatbed doesn't support grayscale or fallback");
                    case ScannerSource.Feeder:
                        if (Scanner.IsFeederGrayscaleAllowed)
                            return ImageScannerColorMode.Grayscale;
                        else if (Scanner.IsFeederColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Feeder doesn't support grayscale or fallback");
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        throw new ArgumentException("Can't get color mode for source mode " + SourceMode);
                }
            case ScannerColorMode.Monochrome:
                switch (SourceMode)
                {
                    case ScannerSource.Flatbed:
                        if (Scanner.IsFlatbedMonochromeAllowed)
                            return ImageScannerColorMode.Monochrome;
                        else if (Scanner.IsFlatbedGrayscaleAllowed)
                            return ImageScannerColorMode.Grayscale;
                        else if (Scanner.IsFlatbedColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Flatbed doesn't support monochrome or fallback");
                    case ScannerSource.Feeder:
                        if (Scanner.IsFeederMonochromeAllowed)
                            return ImageScannerColorMode.Monochrome;
                        else if (Scanner.IsFeederGrayscaleAllowed)
                            return ImageScannerColorMode.Grayscale;
                        else if (Scanner.IsFeederColorAllowed)
                            return ImageScannerColorMode.Color;
                        else
                            throw new ArgumentException("Feeder doesn't support monochrome or fallback");
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        throw new ArgumentException("Can't get color mode for source mode " + SourceMode);
                }
            case ScannerColorMode.Automatic:
                switch (SourceMode)
                {
                    case ScannerSource.Flatbed:
                        if (Scanner.IsFlatbedAutoColorAllowed)
                            return ImageScannerColorMode.AutoColor;
                        else
                            throw new ArgumentException("Flatbed doesn't support auto color");
                    case ScannerSource.Feeder:
                        if (Scanner.IsFeederAutoColorAllowed)
                            return ImageScannerColorMode.AutoColor;
                        else
                            throw new ArgumentException("Feeder doesn't support auto color");
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        throw new ArgumentException("Can't get color mode for source mode " + SourceMode);
                }
            case ScannerColorMode.None:
            default:
                throw new ArgumentException(String.Format("Can't convert {0} to ImageScannerColorMode.", ColorMode));
        }
    }

    public ImageScannerAutoCroppingMode GetAutoCropModeForScanner()
    {
        if (ScanArea is AutoCropArea autoCropRegion)
        {
            switch (autoCropRegion.AutoCropMode)
            {
                case ScannerAutoCropMode.Disabled:
                    return ImageScannerAutoCroppingMode.Disabled;
                case ScannerAutoCropMode.SingleRegion:
                    return ImageScannerAutoCroppingMode.SingleRegion;
                case ScannerAutoCropMode.MultipleRegions:
                    return ImageScannerAutoCroppingMode.MultipleRegion;
                case ScannerAutoCropMode.None:
                default:
                    throw new ArgumentException(String.Format("Can't convert {0} to ImageScannerAutoCroppingMode.", autoCropRegion.AutoCropMode));
            }
        }
        else
        {
            return ImageScannerAutoCroppingMode.Disabled;
        }
    }

    private void SetScanOptionsForScanner(IScanningDevice? scanner, ScannerSource? forcedSourceMode)
    {
        Scanner = scanner;
        if (Scanner == null) return;

        // source mode
        if (Scanner.IsAutoAllowed && forcedSourceMode is null or ScannerSource.Auto)
        {
            SourceMode = ScannerSource.Auto;
        }
        else if (Scanner.IsFlatbedAllowed && forcedSourceMode is null or ScannerSource.Flatbed)
        {
            SourceMode = ScannerSource.Flatbed;

            // color mode
            if (Scanner.IsFlatbedColorAllowed)
            {
                ColorMode = ScannerColorMode.Color;
            }
            else if (Scanner.IsFlatbedGrayscaleAllowed)
            {
                ColorMode = ScannerColorMode.Grayscale;
            }
            else if (Scanner.IsFlatbedMonochromeAllowed)
            {
                ColorMode = ScannerColorMode.Monochrome;
            }
            else if (Scanner.IsFlatbedAutoColorAllowed)
            {
                ColorMode = ScannerColorMode.Automatic;
            }

            // resolution
            SetDefaultResolution(Scanner.FlatbedResolutions);

            // scan area
            ScanArea = null;
        }
        else if (Scanner.IsFeederAllowed && forcedSourceMode is null or ScannerSource.Feeder)
        {
            SourceMode = ScannerSource.Feeder;

            // color mode
            if (Scanner.IsFeederColorAllowed)
            {
                ColorMode = ScannerColorMode.Color;
            }
            else if (Scanner.IsFeederGrayscaleAllowed)
            {
                ColorMode = ScannerColorMode.Grayscale;
            }
            else if (Scanner.IsFeederMonochromeAllowed)
            {
                ColorMode = ScannerColorMode.Monochrome;
            }
            else if (Scanner.IsFeederAutoColorAllowed)
            {
                ColorMode = ScannerColorMode.Automatic;
            }

            // resolution
            SetDefaultResolution(Scanner.FeederResolutions);

            // scan area
            ScanArea = null;

            // duplex
            ScanMultiplePages = true;
            Duplex = false;
        }

        TargetFormat = TargetFormat.PDF;
    }

    public void SetDefaultResolution(List<ScanResolution> resolutions)
    {
        ScanResolution? resolution = resolutions.FirstOrDefault((x) => x.Annotation == ResolutionAnnotation.Default);
        if (resolution == null)
        {
            // fall back to documents resolution
            resolution = resolutions.FirstOrDefault((x) => x.Annotation == ResolutionAnnotation.Documents);

            if (resolution == null)
            {
                // fall back to photos resolution
                resolution = resolutions.FirstOrDefault((x) => x.Annotation == ResolutionAnnotation.Photos);

                if (resolution == null)
                {
                    // fall back to first resolution
                    resolution = resolutions[0];
                }
            }
        }
        Resolution = resolution;
    }

    private string GetShortFriendlySourceMode()
    {
        return SourceMode switch
        {
            ScannerSource.Auto => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceAutoShort),
            ScannerSource.Flatbed => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceFlatbedShort),
            ScannerSource.Feeder => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.SourceFeederShort),
            _ => "",
        };
    }
}


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// <summary>
/// The possible scanner sources.
/// </summary>
public enum ScannerSource
{
    None = 0,
    Auto = 1,
    Flatbed = 2,
    Feeder = 3
}

/// <summary>
/// The possible target formats.
/// </summary>
public enum TargetFormat
{
    None = 0,
    PDF = 1,
    JPG = 2,
    PNG = 3,
    BMP = 4,
    SinglePagePDF = 5,
    TIFF = 6,

    [Obsolete("Not exposed to user")]
    RAW = 7
}

/// <summary>
/// The possible scanner color modes.
/// </summary>
public enum ScannerColorMode
{
    None = 0,
    Color = 1,
    Grayscale = 2,
    Monochrome = 3,
    Automatic = 4
}

/// <summary>
/// The possible scanner auto crop modes.
/// </summary>
public enum ScannerAutoCropMode
{
    None = 0,
    Disabled = 1,
    SingleRegion = 2,
    MultipleRegions = 3
}
