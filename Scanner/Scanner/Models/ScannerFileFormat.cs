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
    public partial class ScannerFileFormat
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly ImageScannerFormat TargetFormat;

        public readonly ImageScannerFormat? OriginalFormat;

        public readonly string FriendlyName;

        public bool RequiresConversion => TargetFormat != OriginalFormat;


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

        public override string ToString()
        {
            return FriendlyName;
        }
    }
}
