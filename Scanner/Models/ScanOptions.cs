using System;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Enums;

namespace Scanner.Models
{
    public class ScanOptions
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Required(ErrorMessage = "Source is required")]
        public ScannerSource Source;

        [Required(ErrorMessage = "ColorMode is required")]
        public ScannerColorMode ColorMode;

        public float Resolution;
        public ScannerAutoCropMode AutoCropMode;
        public bool FeederMultiplePages;
        public bool FeederDuplex;

        [Required(ErrorMessage = "Format is required")]
        public ScannerFileFormat Format;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptions()
        {

        }

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
}
