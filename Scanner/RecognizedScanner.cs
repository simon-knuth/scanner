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

        public bool autoAllowed;
        public bool feederAllowed;
        public bool flatbedAllowed;
        public bool autoPreviewAllowed;

        public bool? feederColorAllowed;
        public bool? feederGrayscaleAllowed;
        public bool? feederMonochromeAllowed;
        public bool? feederDuplexAllowed;
        public bool? feederPreviewAllowed;

        public bool? flatbedColorAllowed;
        public bool? flatbedGrayscaleAllowed;
        public bool? flatbedMonochromeAllowed;
        public bool? flatbedPreviewAllowed;

        public bool fake = false;



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private RecognizedScanner(ImageScanner device, string name)
        {
            scanner = device;

            scannerName = name;

            autoAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
            feederAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder);
            flatbedAllowed = scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed);

            autoPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.AutoConfigured);

            if (feederAllowed)
            {
                feederColorAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                feederGrayscaleAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                feederMonochromeAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                feederDuplexAllowed = scanner.FeederConfiguration.CanScanDuplex;
                feederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);
            }

            if (flatbedAllowed)
            {
                flatbedColorAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                flatbedGrayscaleAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                flatbedMonochromeAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                flatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);
            }
        }


        public RecognizedScanner(string scannerName, bool auto, bool autoPreview, bool flatbed, bool flatbedPreview, bool flatbedColor, bool flatbedGrayscale,
            bool flatbedMonochrome, bool feeder, bool feederPreview, bool feederColor, bool feederGrayscale, bool feederMonochrome, bool feederDuplex)
        {
            // add debugging scanner
            this.scannerName = scannerName;
            autoAllowed = auto;
            autoPreviewAllowed = autoPreview;
            flatbedAllowed = flatbed;
            flatbedPreviewAllowed = flatbedPreview;
            flatbedColorAllowed = flatbedColor;
            flatbedGrayscaleAllowed = flatbedGrayscale;
            flatbedMonochromeAllowed = flatbedMonochrome;
            feederAllowed = feeder;
            feederPreviewAllowed = feederPreview;
            feederColorAllowed = feederColor;
            feederGrayscaleAllowed = feederGrayscale;
            feederMonochromeAllowed = feederMonochrome;
            feederDuplexAllowed = feederDuplex;

            fake = true;
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