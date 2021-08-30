using System;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;

namespace Scanner.Models
{
    public class ScannerFileFormat
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Required(ErrorMessage = "TargetFormat is required")]
        public readonly ImageScannerFormat TargetFormat;

        public readonly ImageScannerFormat? OriginalFormat;

        public readonly string FriendlyName;

        public bool RequiresConversion => TargetFormat == OriginalFormat;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScannerFileFormat(ImageScannerFormat targetFormat, ImageScannerFormat originalFormat)
        {
            TargetFormat = targetFormat;
            OriginalFormat = originalFormat;
            FriendlyName = GenerateFriendlyName(TargetFormat);
        }

        public ScannerFileFormat(ImageScannerFormat targetFormat)
        {
            TargetFormat = targetFormat;
            FriendlyName = GenerateFriendlyName(TargetFormat);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private string GenerateFriendlyName(ImageScannerFormat targetFormat)
        {
            switch (targetFormat)
            {
                case ImageScannerFormat.Jpeg:
                    return "JPG";
                case ImageScannerFormat.Png:
                    return "PNG";
                case ImageScannerFormat.DeviceIndependentBitmap:
                    return "BMP";
                case ImageScannerFormat.Tiff:
                    return "TIF";
                case ImageScannerFormat.Xps:
                    return "XPS";
                case ImageScannerFormat.OpenXps:
                    return "OpenXPS";
                case ImageScannerFormat.Pdf:
                    return "PDF";
                default:
                    throw new ArgumentException("Unable to generate FriendlyName from format " + targetFormat + ".");
            }
        }
    }
}