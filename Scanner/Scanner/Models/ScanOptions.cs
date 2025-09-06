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

namespace Scanner.Models
{
    public partial class ScanOptions : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////                
        [ObservableProperty]
        private IScanningDevice scanner;

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
        private ScannerAutoCropMode autoCropMode;

        [ObservableProperty]
        private bool duplex;

        [ObservableProperty]
        private bool scanMultiplePages;

        [ObservableProperty]
        private int brightness;

        [ObservableProperty]
        private int contrast;

        public Rect? SelectedRegion;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptions(IScanningDevice scanner)
        {
            SetScanOptionsForScanner(scanner);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ImageScannerColorMode GetColorModeForScanning()
        {
            switch (ColorMode)
            {
                case ScannerColorMode.Color:
                    return ImageScannerColorMode.Color;
                case ScannerColorMode.Grayscale:
                    return ImageScannerColorMode.Grayscale;
                case ScannerColorMode.Monochrome:
                    return ImageScannerColorMode.Monochrome;
                case ScannerColorMode.Automatic:
                    return ImageScannerColorMode.AutoColor;
                case ScannerColorMode.None:
                default:
                    throw new ArgumentOutOfRangeException(String.Format("Can't convert {0} to ImageScannerColorMode.", ColorMode));
            }
        }

        public ImageScannerAutoCroppingMode GetAutoCropModeForScanner()
        {
            switch (AutoCropMode)
            {
                case ScannerAutoCropMode.Disabled:
                    return ImageScannerAutoCroppingMode.Disabled;
                case ScannerAutoCropMode.SingleRegion:
                    return ImageScannerAutoCroppingMode.SingleRegion;
                case ScannerAutoCropMode.MultipleRegions:
                    return ImageScannerAutoCroppingMode.MultipleRegion;
                case ScannerAutoCropMode.None:
                default:
                    throw new ArgumentOutOfRangeException(String.Format("Can't convert {0} to ImageScannerAutoCroppingMode.", AutoCropMode));
            }
        }

        private void SetScanOptionsForScanner(IScanningDevice? scanner)
        {
            Scanner = scanner;
            if (Scanner == null) return;

            // source mode
            if (Scanner.IsAutoAllowed)
            {
                SourceMode = ScannerSource.Auto;
            }
            else if (Scanner.IsFlatbedAllowed)
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

                // auto crop mode
                if (Scanner.IsFlatbedAutoCropSupported)
                {
                    AutoCropMode = ScannerAutoCropMode.Disabled;
                }
                else
                {
                    AutoCropMode = ScannerAutoCropMode.None;
                }
            }
            else if (Scanner.IsFeederAllowed)
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

                // auto crop mode
                if (Scanner.IsFeederAutoCropSupported)
                {
                    AutoCropMode = ScannerAutoCropMode.Disabled;
                }
                else
                {
                    AutoCropMode = ScannerAutoCropMode.None;
                }

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
    ///     The possible scanner sources.
    /// </summary>
    public enum ScannerSource
    {
        None = 0,
        Auto = 1,
        Flatbed = 2,
        Feeder = 3
    }

    /// <summary>
    ///     The possible target formats.
    /// </summary>
    public enum TargetFormat
    {
        None = 0,
        PDF = 1,
        JPG = 2,
        PNG = 3,
        BMP = 4,
        TIFF = 5,
        RAW = 6
    }

    /// <summary>
    ///     The possible scanner color modes.
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
    ///     The possible scanner auto crop modes.
    /// </summary>
    public enum ScannerAutoCropMode
    {
        None = 0,
        Disabled = 1,
        SingleRegion = 2,
        MultipleRegions = 3
    }
}
