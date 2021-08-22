using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;

namespace Scanner.Models
{
    public class DiscoveredScanner
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImageScanner Device;
        public string Id;
        public bool Debug;

        [Required(ErrorMessage = "Name is required")]
        public string Name;

        [Required(ErrorMessage = "IsAutoAllowed is required")]
        public bool IsAutoAllowed;
        public bool IsAutoPreviewAllowed;

        [Required(ErrorMessage = "IsFlatbedAllowed is required")]
        public bool IsFlatbedAllowed;
        public bool? IsFlatbedColorAllowed;
        public bool? IsFlatbedGrayscaleAllowed;
        public bool? IsFlatbedMonochromeAllowed;
        public bool? IsFlatbedPreviewAllowed;

        [Required(ErrorMessage = "IsFeederAllowed is required")]
        public bool IsFeederAllowed;
        public bool? IsFeederColorAllowed;
        public bool? IsFeederGrayscaleAllowed;
        public bool? IsFeederMonochromeAllowed;
        public bool? IsFeederDuplexAllowed;
        public bool? IsFeederPreviewAllowed;


        private const float DocumentsResolution = 300;      // the recommended resolution for documents
        private const float PhotosResolution = 500;         // the recommended resolution for photos


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public DiscoveredScanner(ImageScanner device, string name)
        {
            Device = device;
            Id = Device.DeviceId;

            Name = name;

            IsAutoAllowed = device.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
            IsFeederAllowed = device.IsScanSourceSupported(ImageScannerScanSource.Feeder);
            IsFlatbedAllowed = device.IsScanSourceSupported(ImageScannerScanSource.Flatbed);

            IsAutoPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.AutoConfigured);

            IsFlatbedColorAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
            IsFlatbedGrayscaleAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
            IsFlatbedMonochromeAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

            IsFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);

            if (IsFeederAllowed)
            {
                IsFeederColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFeederGrayscaleAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFeederMonochromeAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                IsFeederDuplexAllowed = device.FeederConfiguration.CanScanDuplex;
                IsFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);
            }
        }

        public DiscoveredScanner(string name)
        {
            Name = name;
            Debug = true;
        }
    }
}
