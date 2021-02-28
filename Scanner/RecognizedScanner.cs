using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;

using static Enums;
using static Globals;
using static Utilities;


namespace Scanner
{
    class RecognizedScanner
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ImageScanner scanner;
        public string scannerName;

        public bool isAutoAllowed;
        public bool isFeederAllowed;
        public bool isFlatbedAllowed;
        public bool isAutoPreviewAllowed;

        public bool? isFeederColorAllowed;
        public bool? isFeederGrayscaleAllowed;
        public bool? isFeederMonochromeAllowed;
        public bool? isFeederDuplexAllowed;
        public bool? isFeederPreviewAllowed;

        public bool? isFlatbedColorAllowed;
        public bool? isFlatbedGrayscaleAllowed;
        public bool? isFlatbedMonochromeAllowed;
        public bool? isFlatbedPreviewAllowed;

        public bool isFake = false;



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private RecognizedScanner(ImageScanner device, string name)
        {
            scanner = device;

            scannerName = name;

            isAutoAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
            isFeederAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder);
            isFlatbedAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed);

            isAutoPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.AutoConfigured);

            if (isFeederAllowed)
            {
                isFeederColorAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                isFeederGrayscaleAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                isFeederMonochromeAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                isFeederDuplexAllowed = scanner.FeederConfiguration.CanScanDuplex;
                isFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);
            }

            if (isFlatbedAllowed)
            {
                isFlatbedColorAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                isFlatbedGrayscaleAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                isFlatbedMonochromeAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                isFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);
            }
        }


        public RecognizedScanner(string scannerName, bool hasAuto, bool hasAutoPreview, bool hasFlatbed, bool hasFlatbedPreview, bool hasFlatbedColor, bool hasFlatbedGrayscale,
            bool hasFlatbedMonochrome, bool hasFeeder, bool hasFeederPreview, bool hasFeederColor, bool hasFeederGrayscale, bool hasFeederMonochrome, bool hasFeederDuplex)
        {
            // add debugging scanner
            this.scannerName = scannerName;
            isAutoAllowed = hasAuto;
            isAutoPreviewAllowed = hasAutoPreview;
            isFlatbedAllowed = hasFlatbed;
            isFlatbedPreviewAllowed = hasFlatbedPreview;
            isFlatbedColorAllowed = hasFlatbedColor;
            isFlatbedGrayscaleAllowed = hasFlatbedGrayscale;
            isFlatbedMonochromeAllowed = hasFlatbedMonochrome;
            isFeederAllowed = hasFeeder;
            isFeederPreviewAllowed = hasFeederPreview;
            isFeederColorAllowed = hasFeederColor;
            isFeederGrayscaleAllowed = hasFeederGrayscale;
            isFeederMonochromeAllowed = hasFeederMonochrome;
            isFeederDuplexAllowed = hasFeederDuplex;

            isFake = true;
        }


        public async static Task<RecognizedScanner> CreateFromDeviceInformationAsync(DeviceInformation device)
        {
            return new RecognizedScanner(await ImageScanner.FromIdAsync(device.Id), device.Name);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    }
}