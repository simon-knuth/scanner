using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using Windows.Foundation.Metadata;

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
        public ObservableCollection<ScannerFileFormat> AutoFormats;

        [Required(ErrorMessage = "IsFlatbedAllowed is required")]
        public bool IsFlatbedAllowed;
        public bool IsFlatbedColorAllowed;
        public bool IsFlatbedGrayscaleAllowed;
        public bool IsFlatbedMonochromeAllowed;
        public bool IsFlatbedPreviewAllowed;
        public ObservableCollection<ScanResolution> FlatbedResolutions;
        public ObservableCollection<ScannerFileFormat> FlatbedFormats;

        [Required(ErrorMessage = "IsFeederAllowed is required")]
        public bool IsFeederAllowed;
        public bool IsFeederColorAllowed;
        public bool IsFeederGrayscaleAllowed;
        public bool IsFeederMonochromeAllowed;
        public bool IsFeederDuplexAllowed;
        public bool IsFeederPreviewAllowed;
        public ObservableCollection<ScanResolution> FeederResolutions;
        public ObservableCollection<ScannerFileFormat> FeederFormats;

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

            if (IsAutoAllowed)
            {
                IsAutoPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.AutoConfigured);

                AutoFormats = GenerateFormats(device.AutoConfiguration);
            }

            if (IsFlatbedAllowed)
            {
                IsFlatbedColorAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFlatbedGrayscaleAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFlatbedMonochromeAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                IsFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);

                FlatbedResolutions = GenerateResolutions(device.FlatbedConfiguration);

                FlatbedFormats = GenerateFormats(device.FlatbedConfiguration);
            }

            if (IsFeederAllowed)
            {
                IsFeederColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFeederGrayscaleAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFeederMonochromeAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                IsFeederDuplexAllowed = device.FeederConfiguration.CanScanDuplex;
                IsFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);

                FeederResolutions = GenerateResolutions(device.FeederConfiguration);

                FeederFormats = GenerateFormats(device.FeederConfiguration);
            }
        }

        public DiscoveredScanner(string name)
        {
            Name = name;
            Debug = true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Generates the true available resolution values for a flatbed/feeder configuration. Also enriches the resolution
        ///     values with the related <see cref="ResolutionAnnotation"/> and a friendly string.
        ///     Assumption: DpiX = DpiY
        /// </summary>
        /// <param name="config">The configuration for which resolution values are to be determined.</param>
        private ObservableCollection<ScanResolution> GenerateResolutions(IImageScannerSourceConfiguration config)
        {
            float currentValue = config.MinResolution.DpiX;
            float lastValue = -1;
            ObservableCollection<ScanResolution> result = new ObservableCollection<ScanResolution>();
            int bestDocumentsResolution = -1, bestPhotosResolution = -1;

            while (currentValue <= config.MaxResolution.DpiX)
            {
                config.DesiredResolution = new ImageScannerResolution { DpiX = currentValue, DpiY = currentValue };

                if (config.ActualResolution.DpiX != lastValue)
                {
                    ScanResolution newRes = new ScanResolution(config.ActualResolution.DpiX, ResolutionAnnotation.None);
                    result.Add(newRes);
                    lastValue = config.ActualResolution.DpiX;

                    // check how suitable these resolutions are for scanning documents and photos
                    if (bestDocumentsResolution == -1
                        || Math.Abs(ScanResolution.DocumentsResolution - newRes.Resolution.DpiX) < Math.Abs(ScanResolution.DocumentsResolution - result[bestDocumentsResolution].Resolution.DpiX))
                    {
                        bestDocumentsResolution = result.Count - 1;
                    }
                    if (bestPhotosResolution == -1
                        || Math.Abs(ScanResolution.PhotosResolution - newRes.Resolution.DpiX) < Math.Abs(ScanResolution.PhotosResolution - result[bestPhotosResolution].Resolution.DpiX))
                    {
                        bestPhotosResolution = result.Count - 1;
                    }
                }

                if (lastValue <= currentValue) currentValue += 1;
                else currentValue = config.ActualResolution.DpiX + 1;
            }

            if (result.Count == 0)
            {
                //log.Error("Generating resolutions for {@Config} failed.", config);
                throw new ApplicationException("Unable to generate any resolutions for given scanner.");
            }

            // determine the final properties
            if (bestDocumentsResolution == bestPhotosResolution)
            {
                result[bestDocumentsResolution] = new ScanResolution(result[bestDocumentsResolution].Resolution.DpiX, ResolutionAnnotation.Default);
            }
            else
            {
                result[bestDocumentsResolution] = new ScanResolution(result[bestDocumentsResolution].Resolution.DpiX, ResolutionAnnotation.Documents);
                result[bestPhotosResolution] = new ScanResolution(result[bestPhotosResolution].Resolution.DpiX, ResolutionAnnotation.Photos);
            }

            //log.Information("Generated {@Resolutions} for scanner.", result);

            return result;
        }

        private ObservableCollection<ScannerFileFormat> GenerateFormats(IImageScannerFormatConfiguration config)
        {
            ObservableCollection<ScannerFileFormat> result = new ObservableCollection<ScannerFileFormat>();
            ImageScannerFormat[] formatArray =
            {
                ImageScannerFormat.Jpeg,
                ImageScannerFormat.Png,
                ImageScannerFormat.Pdf,
                ImageScannerFormat.Tiff,
                ImageScannerFormat.DeviceIndependentBitmap
            };
            List<ImageScannerFormat> formatList = new List<ImageScannerFormat>(formatArray);

            // check whether the PDF component is available
            if (!ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                formatList.Remove(ImageScannerFormat.Pdf);
            }

            // generate native image formats first
            ImageScannerFormat? baseFormat = null;
            if (config.IsFormatSupported(ImageScannerFormat.Jpeg))
            {
                result.Add(new ScannerFileFormat(ImageScannerFormat.Jpeg, ImageScannerFormat.Jpeg));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Jpeg;
                formatList.Remove(ImageScannerFormat.Jpeg);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Png))
            {
                result.Add(new ScannerFileFormat(ImageScannerFormat.Png, ImageScannerFormat.Png));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Png;
                formatList.Remove(ImageScannerFormat.Png);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Tiff))
            {
                result.Add(new ScannerFileFormat(ImageScannerFormat.Tiff, ImageScannerFormat.Tiff));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Tiff;
                formatList.Remove(ImageScannerFormat.Tiff);
            }

            if (config.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap))
            {
                result.Add(new ScannerFileFormat(ImageScannerFormat.DeviceIndependentBitmap, ImageScannerFormat.DeviceIndependentBitmap));
                if (baseFormat == null) baseFormat = ImageScannerFormat.DeviceIndependentBitmap;
                formatList.Remove(ImageScannerFormat.DeviceIndependentBitmap);
            }

            // generate formats reachable using conversion
            if (baseFormat != null)
            {
                foreach (ImageScannerFormat targetFormat in formatList)
                {
                    result.Add(new ScannerFileFormat(targetFormat, (ImageScannerFormat)baseFormat));
                }
            }

            return result;
        }
    }
}
