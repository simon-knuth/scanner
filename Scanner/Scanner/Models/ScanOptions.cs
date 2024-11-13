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

namespace Scanner.Models
{
    public partial class ScanOptions : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////                
        [ObservableProperty]
        private ScannerSource sourceMode;

        [ObservableProperty]
        private TargetFormat targetFormat;

        [ObservableProperty]
        private ScannerColorMode colorMode;

        [ObservableProperty]
        private float resolution;

        public ScannerAutoCropMode AutoCropMode;
        public bool FeederDuplex;

        public int? Brightness;
        public int? Contrast;

        public Rect? SelectedRegion;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptions()
        {
            
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
        PDF = 0,
        JPG = 1,
        PNG = 2,
        BMP = 3,
        TIFF = 4,
        RAW = 5
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
