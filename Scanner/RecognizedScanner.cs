using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;

using static Enums;
using static Globals;


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
        public bool isAutoPreviewAllowed;

        public bool isFlatbedAllowed;
        public bool? isFlatbedColorAllowed;
        public bool? isFlatbedGrayscaleAllowed;
        public bool? isFlatbedMonochromeAllowed;
        public bool? isFlatbedPreviewAllowed;
        public List<ValueTuple<float, ResolutionProperty>> flatbedResolutions;

        public bool isFeederAllowed;
        public bool? isFeederColorAllowed;
        public bool? isFeederGrayscaleAllowed;
        public bool? isFeederMonochromeAllowed;
        public bool? isFeederDuplexAllowed;
        public bool? isFeederPreviewAllowed;
        public List<ValueTuple<float, ResolutionProperty>> feederResolutions;

        public bool isFake = false;


        private const float DocumentsResolution = 300;      // the recommended resolution for documents
        private const float PhotosResolution = 500;         // the recommended resolution for photos



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

            if (isFlatbedAllowed)
            {
                isFlatbedColorAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                isFlatbedGrayscaleAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                isFlatbedMonochromeAllowed = scanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                isFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);

                flatbedResolutions = GenerateResolutions(scanner.FlatbedConfiguration);
            }

            if (isFeederAllowed)
            {
                isFeederColorAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                isFeederGrayscaleAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                isFeederMonochromeAllowed = scanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                isFeederDuplexAllowed = scanner.FeederConfiguration.CanScanDuplex;
                isFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);

                feederResolutions = GenerateResolutions(scanner.FeederConfiguration);
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
            flatbedResolutions = GenerateFakeResolutions();

            isFeederAllowed = hasFeeder;
            isFeederPreviewAllowed = hasFeederPreview;
            isFeederColorAllowed = hasFeederColor;
            isFeederGrayscaleAllowed = hasFeederGrayscale;
            isFeederMonochromeAllowed = hasFeederMonochrome;
            isFeederDuplexAllowed = hasFeederDuplex;
            feederResolutions = GenerateFakeResolutions();

            isFake = true;
        }


        public async static Task<RecognizedScanner> CreateFromDeviceInformationAsync(DeviceInformation device)
        {
            return new RecognizedScanner(await ImageScanner.FromIdAsync(device.Id), device.Name);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Generates the true available resolution values for a flatbed/feeder configuration. Also enriches the resolution
        ///     values with the related <see cref="ResolutionProperty"/>.
        ///     Assumption: DpiX = DpiY
        /// </summary>
        /// <param name="config">The configuration for which resolution values are to be determined.</param>
        /// <returns>The generates resolutions, each with their <see cref="ResolutionProperty"/>.</returns>
        private List<ValueTuple<float, ResolutionProperty>> GenerateResolutions(IImageScannerSourceConfiguration config)
        {
            float currentValue = config.MinResolution.DpiX;
            float lastValue = -1;
            List<ValueTuple<float, ResolutionProperty>> result = new List<ValueTuple<float, ResolutionProperty>>();
            int bestDocumentsResolution = -1, bestPhotosResolution = -1;

            while (currentValue <= config.MaxResolution.DpiX)
            {
                config.DesiredResolution = new ImageScannerResolution { DpiX = currentValue, DpiY = currentValue };

                if (config.ActualResolution.DpiX != lastValue)
                {
                    ValueTuple<float, ResolutionProperty> newRes = new ValueTuple<float,
                        ResolutionProperty>(config.ActualResolution.DpiX, ResolutionProperty.None);
                    result.Add(newRes);
                    lastValue = config.ActualResolution.DpiX;

                    // check how suitable these resolutions are for scanning documents and photos
                    if (bestDocumentsResolution == -1
                        || Math.Abs(DocumentsResolution - newRes.Item1) < Math.Abs(DocumentsResolution - result[bestDocumentsResolution].Item1))
                    {
                        bestDocumentsResolution = result.Count - 1;
                    }
                    if (bestPhotosResolution == -1
                        || Math.Abs(PhotosResolution - newRes.Item1) < Math.Abs(PhotosResolution - result[bestPhotosResolution].Item1))
                    {
                        bestPhotosResolution = result.Count - 1;
                    }
                }

                if (lastValue <= currentValue) currentValue += 1;
                else currentValue = config.ActualResolution.DpiX + 1;
            }

            if (result.Count == 0)
            {
                log.Error("Generating resolutions for {@Config} failed.", config);
                throw new ApplicationException("Unable to generate any resolutions for given scanner.");
            }

            // determine the final properties
            if (bestDocumentsResolution == bestPhotosResolution)
            {
                result[bestDocumentsResolution] = new ValueTuple<float, ResolutionProperty>(result[bestDocumentsResolution].Item1, ResolutionProperty.Default);
            }
            else
            {
                result[bestDocumentsResolution] = new ValueTuple<float, ResolutionProperty>(result[bestDocumentsResolution].Item1, ResolutionProperty.Documents);
                result[bestPhotosResolution] = new ValueTuple<float, ResolutionProperty>(result[bestPhotosResolution].Item1, ResolutionProperty.Photos);
            }

            log.Information("Generated {@Resolutions} for scanner.", result);

            return result;
        }


        private List<ValueTuple<float, ResolutionProperty>> GenerateFakeResolutions()
        {
            return new List<(float, ResolutionProperty)>
            {
                new ValueTuple<float,ResolutionProperty>(200, ResolutionProperty.None),
                new ValueTuple<float, ResolutionProperty>(300, ResolutionProperty.Documents),
                new ValueTuple<float, ResolutionProperty>(550, ResolutionProperty.Photos),
                new ValueTuple<float, ResolutionProperty>(800, ResolutionProperty.None),
                new ValueTuple<float, ResolutionProperty>(1200, ResolutionProperty.None)
            };
        }
    }
}