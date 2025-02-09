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
using Scanner.Models.Interfaces;
using Windows.Devices.Scanners;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Enumeration;
using Scanner.Services;
using Scanner.Services.Interfaces;
using CommunityToolkit.Mvvm.DependencyInjection;
using System.Collections.ObjectModel;
using Windows.Foundation.Metadata;
using Windows.Storage;

namespace Scanner.Models
{
    internal partial class HardwareScanner : IScanningDevice
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        public string Id { get; private set; }
        public string Name { get; private set; }
        private ImageScanner imageScanner;

        #region Automatic configuration
        public bool IsAutoAllowed { get; private set; }
        public bool IsAutoPreviewAllowed { get; private set; }

        public List<ImageScannerFormat> AutoFormats { get; private set; }
        #endregion

        #region Flatbed
        public bool IsFlatbedAllowed { get; private set; }
        public bool IsFlatbedPreviewAllowed { get; private set; }

        public List<ImageScannerFormat> FlatbedFormats { get; private set; }

        public bool IsFlatbedColorAllowed { get; private set; }
        public bool IsFlatbedGrayscaleAllowed { get; private set; }
        public bool IsFlatbedMonochromeAllowed { get; private set; }
        public bool IsFlatbedAutoColorAllowed { get; private set; }

        public bool IsFlatbedAutoCropSingleRegionAllowed { get; private set; }
        public bool IsFlatbedAutoCropMultiRegionAllowed { get; private set; }

        public List<ScanResolution> FlatbedResolutions { get; private set; }
        #endregion

        #region Feeder
        public bool IsFeederAllowed { get; private set; }
        public bool IsFeederPreviewAllowed { get; private set; }

        public List<ImageScannerFormat> FeederFormats { get; private set; }

        public bool IsFeederColorAllowed { get; private set; }
        public bool IsFeederGrayscaleAllowed { get; private set; }
        public bool IsFeederMonochromeAllowed { get; private set; }
        public bool IsFeederAutoColorAllowed { get; private set; }

        public bool IsFeederAutoCropSingleRegionAllowed { get; private set; }
        public bool IsFeederAutoCropMultiRegionAllowed { get; private set; }

        public bool IsFeederDuplexSupported { get; private set; }

        public List<ScanResolution> FeederResolutions { get; private set; }
        #endregion


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public HardwareScanner(ImageScanner device, string name)
        {
            Id = device.DeviceId;
            Name = name;
            imageScanner = device;

            try
            {
                IsAutoAllowed = device.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
                IsFeederAllowed = device.IsScanSourceSupported(ImageScannerScanSource.Feeder);
                IsFlatbedAllowed = device.IsScanSourceSupported(ImageScannerScanSource.Flatbed);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "HardwareScanner - Failed to determine supported scan sources");
                throw;
            }

            // auto mode
            if (IsAutoAllowed)
            {
                IsAutoPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.AutoConfigured);

                AutoFormats = GenerateFormats(device.AutoConfiguration);
            }

            // flatbed mode
            if (IsFlatbedAllowed)
            {
                IsFlatbedColorAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFlatbedGrayscaleAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFlatbedMonochromeAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                IsFlatbedAutoColorAllowed = device.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.AutoColor);

                if (!IsFlatbedColorAllowed && !IsFlatbedGrayscaleAllowed && !IsFlatbedMonochromeAllowed && !IsFlatbedAutoColorAllowed)
                {
                    // no color mode allowed, source mode is invalid
                    IsFlatbedAllowed = false;
                    LogService?.Log.Warning("HardwareScanner - No color mode for flatbed allowed, invalid source mode");
                }
                else
                {
                    try
                    {
                        IsFlatbedPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Flatbed);
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Warning(exc, "HardwareScanner - Failed to determine preview support for flatbed");
                        IsFlatbedPreviewAllowed = false;
                    }

                    try
                    {
                        IsFlatbedAutoCropSingleRegionAllowed = device.FlatbedConfiguration
                            .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.SingleRegion);
                        IsFlatbedAutoCropMultiRegionAllowed = device.FlatbedConfiguration
                            .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.MultipleRegion);
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Warning(exc, "DiscoveredScanner - Failed to determine auto crop support for flatbed");
                        IsFlatbedAutoCropSingleRegionAllowed = IsFlatbedAutoCropMultiRegionAllowed = false;
                    }

                    FlatbedFormats = GenerateFormats(device.FlatbedConfiguration);
                    if (FlatbedFormats.Count == 0)
                    {
                        LogService?.Log.Warning("HardwareScanner - No formats generated for flatbed, invalid source mode");
                        IsFeederAllowed = false;
                    }
                    else
                    {
                        LogService?.Log.Information("HardwareScanner - Generated {@Formats} for flatbed", FlatbedFormats);
                    }

                    FlatbedResolutions = GenerateResolutions(device.FlatbedConfiguration);
                    if (FlatbedResolutions.Count == 0)
                    {
                        LogService?.Log.Warning("HardwareScanner - No resolutions generated for flatbed, invalid source mode");
                        IsFeederAllowed = false;
                    }
                    else
                    {
                        LogService?.Log.Information("HardwareScanner - Generated {@Resolutions} for flatbed", FlatbedResolutions);
                    }
                }
            }

            // feeder mode
            if (IsFeederAllowed)
            {
                IsFeederColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                IsFeederGrayscaleAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                IsFeederMonochromeAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                IsFeederAutoColorAllowed = device.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.AutoColor);

                if (!IsFeederColorAllowed && !IsFeederGrayscaleAllowed && !IsFeederMonochromeAllowed && !IsFeederAutoColorAllowed)
                {
                    // no color mode allowed, source mode is invalid
                    IsFeederAllowed = false;
                    LogService?.Log.Warning("HardwareScanner - No color mode for feeder allowed, invalid source mode");
                }
                else
                {
                    try
                    {
                        IsFeederDuplexSupported = device.FeederConfiguration.CanScanDuplex;
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Warning(exc, "HardwareScanner - Failed to determine duplex support for feeder");
                        IsFeederDuplexSupported = false;
                    }

                    try
                    {
                        IsFeederPreviewAllowed = device.IsPreviewSupported(ImageScannerScanSource.Feeder);
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Warning(exc, "HardwareScanner - Failed to determine preview support for feeder");
                        IsFeederPreviewAllowed = false;
                    }

                    try
                    {
                        IsFeederAutoCropSingleRegionAllowed = device.FeederConfiguration
                            .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.SingleRegion);
                        IsFeederAutoCropMultiRegionAllowed = device.FeederConfiguration
                            .IsAutoCroppingModeSupported(ImageScannerAutoCroppingMode.MultipleRegion);
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Warning(exc, "HardwareScanner - Failed to determine auto crop support for feeder");
                        IsFeederAutoCropSingleRegionAllowed = IsFeederAutoCropMultiRegionAllowed = false;
                    }

                    FeederFormats = GenerateFormats(device.FeederConfiguration);
                    if (FeederFormats.Count == 0)
                    {
                        LogService?.Log.Warning("HardwareScanner - No formats generated for feeder, invalid source mode");
                        IsFeederAllowed = false;
                    }
                    else
                    {
                        LogService?.Log.Information("HardwareScanner - Generated {@Formats} for feeder", FeederFormats);
                    }

                    FeederResolutions = GenerateResolutions(device.FeederConfiguration);
                    if (FeederResolutions.Count == 0)
                    {
                        LogService?.Log.Warning("HardwareScanner - No resolutions generated for feeder, invalid source mode");
                        IsFeederAllowed = false;
                    }
                    else
                    {
                        LogService?.Log.Information("HardwareScanner - Generated {@Resolutions} for feeder", FeederResolutions);
                    }
                }
            }

            if (!IsAutoAllowed && !IsFlatbedAllowed && !IsFeederAllowed)
            {
                // no source mode allowed, scanner is invalid and useless
                throw new ArgumentException("Scanner doesn't support any source mode and can't be used");
            }

            LogService?.Log.Information("HardwareScanner - Created {@Scanner}", this);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void CancelPreview()
        {
            throw new NotImplementedException();
        }

        public void CancelScan()
        {
            throw new NotImplementedException();
        }

        public Task<BitmapImage> GetPreviewAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IList<StorageFile>> GetScanAsync(StorageFolder targetFolder)
        {
            throw new NotImplementedException();
        }

        private List<ImageScannerFormat> GenerateFormats(IImageScannerFormatConfiguration config)
        {
            List<ImageScannerFormat> result = new();

            if (config.IsFormatSupported(ImageScannerFormat.Jpeg))
            {
                result.Add(ImageScannerFormat.Jpeg);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Png))
            {
                result.Add(ImageScannerFormat.Png);
            }

            if (config.IsFormatSupported(ImageScannerFormat.Tiff))
            {
                result.Add(ImageScannerFormat.Tiff);
            }

            if (config.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap))
            {
                result.Add(ImageScannerFormat.DeviceIndependentBitmap);
            }

            return result;
        }

        /// <summary>
        ///     Generates the true available resolution values for a flatbed/feeder configuration. Also enriches the resolution
        ///     values with the related <see cref="ResolutionAnnotation"/> and a friendly string.
        ///     Assumption: DpiX = DpiY
        /// </summary>
        /// <param name="config">The configuration for which resolution values are to be determined.</param>
        private List<ScanResolution> GenerateResolutions(IImageScannerSourceConfiguration config)
        {
            float currentValue = config.MinResolution.DpiX;
            float lastValue = -1;
            List<ScanResolution> result = new();
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
                        || Math.Abs(AppConfig.DocumentsResolution - newRes.Resolution.DpiX) < Math.Abs(AppConfig.DocumentsResolution - result[bestDocumentsResolution].Resolution.DpiX))
                    {
                        bestDocumentsResolution = result.Count - 1;
                    }
                    if (bestPhotosResolution == -1
                        || Math.Abs(AppConfig.PhotosResolution - newRes.Resolution.DpiX) < Math.Abs(AppConfig.PhotosResolution - result[bestPhotosResolution].Resolution.DpiX))
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

            return result;
        }

        public bool IsPreviewSupported(ScannerSource source)
        {
            switch (source)
            {
                case ScannerSource.Auto:
                    return IsAutoAllowed && IsAutoPreviewAllowed;
                case ScannerSource.Flatbed:
                    return IsFlatbedAllowed && IsFlatbedPreviewAllowed;
                case ScannerSource.Feeder:
                    return IsFeederAllowed && IsFeederPreviewAllowed;
                case ScannerSource.None:
                default:
                    return false;
            }
        }
    }
}
