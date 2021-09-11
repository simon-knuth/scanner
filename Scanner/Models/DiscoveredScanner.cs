﻿using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Models
{
    public class DiscoveredScanner
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public ImageScanner Device;
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
        public bool IsFlatbedAutoColorAllowed;
        public bool IsFlatbedPreviewAllowed;
        public bool IsFlatbedAutoCropSingleRegionAllowed;
        public bool IsFlatbedAutoCropMultiRegionAllowed;
        public bool IsFlatbedAutoCropPossible =>
            IsFlatbedAutoCropSingleRegionAllowed || IsFlatbedAutoCropMultiRegionAllowed;
        public ObservableCollection<ScanResolution> FlatbedResolutions;
        public ObservableCollection<ScannerFileFormat> FlatbedFormats;

        [Required(ErrorMessage = "IsFeederAllowed is required")]
        public bool IsFeederAllowed;
        public bool IsFeederColorAllowed;
        public bool IsFeederGrayscaleAllowed;
        public bool IsFeederMonochromeAllowed;
        public bool IsFeederAutoColorAllowed;
        public bool IsFeederDuplexAllowed;
        public bool IsFeederPreviewAllowed;
        public bool IsFeederAutoCropSingleRegionAllowed;
        public bool IsFeederAutoCropMultiRegionAllowed;
        public bool IsFeederAutoCropPossible =>
            IsFeederAutoCropSingleRegionAllowed || IsFeederAutoCropMultiRegionAllowed;
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
                IsFlatbedAutoColorAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.AutoColor);

                IsFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);

                IsFlatbedAutoCropSingleRegionAllowed = device.FlatbedConfiguration
                    .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.SingleRegion);
                IsFlatbedAutoCropMultiRegionAllowed = device.FlatbedConfiguration
                    .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.MultipleRegion);

                FlatbedResolutions = GenerateResolutions(device.FlatbedConfiguration);

                FlatbedFormats = GenerateFormats(device.FlatbedConfiguration);
            }

            if (IsFeederAllowed)
            {
                IsFeederColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFeederGrayscaleAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFeederMonochromeAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                IsFeederAutoColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.AutoColor);

                IsFeederDuplexAllowed = device.FeederConfiguration.CanScanDuplex;
                IsFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);

                IsFeederAutoCropSingleRegionAllowed = device.FeederConfiguration
                    .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.SingleRegion);
                IsFeederAutoCropMultiRegionAllowed = device.FeederConfiguration
                    .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.MultipleRegion);

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

        /// <summary>
        ///     Generates the available file formats for the given <paramref name="config"/>, including
        ///     formats available using conversion. The formats are generated in the following order
        ///     used in the formatArray.
        /// </summary>
        private ObservableCollection<ScannerFileFormat> GenerateFormats(IImageScannerFormatConfiguration config)
        {
            List<ScannerFileFormat> unsortedResult = new List<ScannerFileFormat>();
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
                unsortedResult.Add(new ScannerFileFormat(ImageScannerFormat.Jpeg, ImageScannerFormat.Jpeg));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Jpeg;
                formatList.Remove(ImageScannerFormat.Jpeg);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Png))
            {
                unsortedResult.Add(new ScannerFileFormat(ImageScannerFormat.Png, ImageScannerFormat.Png));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Png;
                formatList.Remove(ImageScannerFormat.Png);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Tiff))
            {
                unsortedResult.Add(new ScannerFileFormat(ImageScannerFormat.Tiff, ImageScannerFormat.Tiff));
                if (baseFormat == null) baseFormat = ImageScannerFormat.Tiff;
                formatList.Remove(ImageScannerFormat.Tiff);
            }

            if (config.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap))
            {
                unsortedResult.Add(new ScannerFileFormat(ImageScannerFormat.DeviceIndependentBitmap, ImageScannerFormat.DeviceIndependentBitmap));
                if (baseFormat == null) baseFormat = ImageScannerFormat.DeviceIndependentBitmap;
                formatList.Remove(ImageScannerFormat.DeviceIndependentBitmap);
            }

            // generate formats reachable using conversion
            if (baseFormat != null)
            {
                foreach (ImageScannerFormat targetFormat in formatList)
                {
                    unsortedResult.Add(new ScannerFileFormat(targetFormat, (ImageScannerFormat)baseFormat));
                }
            }

            // apply desired order
            ObservableCollection<ScannerFileFormat> result = new ObservableCollection<ScannerFileFormat>();
            foreach (ImageScannerFormat format in formatArray)
            {
                ScannerFileFormat found = unsortedResult.FirstOrDefault(x => x.TargetFormat == format);
                if (found != null) result.Add(found);
            }

            return result;
        }

        /// <summary>
        ///     Returns a scan preview as <see cref="BitmapImage"/> using <paramref name="config"/>.
        /// </summary>
        /// <exception cref="Exception">
        ///     Occurs when the scanner sends an error message.
        /// </exception>
        public async Task<BitmapImage> GetPreviewAsync(ImageScannerScanSource config)
        {
            using (IRandomAccessStream previewStream = new InMemoryRandomAccessStream())
            {
                var previewResult = await Device.ScanPreviewToStreamAsync(config, previewStream);

                if (previewResult.Succeeded)
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(previewStream);
                    return bitmapImage;
                }
                else
                {
                    throw new ApplicationException("Preview unsuccessful.");
                }
            }
        }

        /// <summary>
        ///     Prepares the scanner for a scan with the options given in <paramref name="options"/>.
        /// </summary>
        public void ConfigureForScanOptions(ScanOptions options)
        {
            switch (options.Source)
            {
                case Enums.ScannerSource.Auto:
                    // file format
                    if (options.Format.OriginalFormat == null)
                    {
                        Device.AutoConfiguration.Format = options.Format.TargetFormat;
                    }
                    else
                    {
                        Device.AutoConfiguration.Format = (ImageScannerFormat)options.Format.OriginalFormat;
                    }
                    break;

                case Enums.ScannerSource.Flatbed:
                    // file format
                    if (options.Format.OriginalFormat == null)
                    {
                        Device.FlatbedConfiguration.Format = options.Format.TargetFormat;
                    }
                    else
                    {
                        Device.FlatbedConfiguration.Format = (ImageScannerFormat)options.Format.OriginalFormat;
                    }

                    // color mode
                    Device.FlatbedConfiguration.ColorMode = options.GetColorModeForScanning();

                    // resolution
                    Device.FlatbedConfiguration.DesiredResolution = new ImageScannerResolution
                    {
                        DpiX = options.Resolution,
                        DpiY = options.Resolution
                    };

                    // auto crop mode
                    if (IsFlatbedAutoCropPossible)
                    {
                        // prevents exception when only "Disabled" is available
                        Device.FlatbedConfiguration.AutoCroppingMode = options.GetAutoCropModeForScanner();
                    }
                    break;

                case Enums.ScannerSource.Feeder:
                    // file format
                    if (options.Format.OriginalFormat == null)
                    {
                        Device.FeederConfiguration.Format = options.Format.TargetFormat;
                    }
                    else
                    {
                        Device.FeederConfiguration.Format = (ImageScannerFormat)options.Format.OriginalFormat;
                    }

                    // color mode
                    Device.FeederConfiguration.ColorMode = options.GetColorModeForScanning();

                    // resolution
                    Device.FeederConfiguration.DesiredResolution = new ImageScannerResolution
                    {
                        DpiX = options.Resolution,
                        DpiY = options.Resolution
                    };

                    // auto crop mode
                    if (IsFeederAutoCropPossible)
                    {
                        // prevents exception when only "Disabled" is available
                        Device.FeederConfiguration.AutoCroppingMode = options.GetAutoCropModeForScanner();
                    }

                    // max number of pages
                    if (options.FeederMultiplePages) Device.FeederConfiguration.MaxNumberOfPages = 10;
                    else Device.FeederConfiguration.MaxNumberOfPages = 1;

                    // duplex
                    Device.FeederConfiguration.Duplex = options.FeederDuplex;
                    break;

                case Enums.ScannerSource.None:
                default:
                    LogService?.Log.Error("Unable to get scan for unknown {source}.", options.Source);
                    throw new ArgumentException("No source selected.");
            }
        }
    }
}