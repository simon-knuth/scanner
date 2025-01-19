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

namespace Scanner.Models
{
    public partial class DebugScanner : IScanningDevice
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
        public DebugScanner(DebugScannerSetupProperties setupProperties)
        {
            Id = Guid.NewGuid().ToString();
            Name = setupProperties.Name;

            IsAutoAllowed = setupProperties.IsAutoAllowed;
            IsAutoPreviewAllowed = setupProperties.IsAutoPreviewAllowed;
            AutoFormats = new List<ImageScannerFormat> { ImageScannerFormat.Jpeg, ImageScannerFormat.DeviceIndependentBitmap };

            IsFlatbedAllowed = setupProperties.IsFlatbedAllowed;
            IsFlatbedPreviewAllowed = setupProperties.IsFlatbedPreviewAllowed;
            IsFlatbedColorAllowed = setupProperties.IsFlatbedColorAllowed;
            IsFlatbedGrayscaleAllowed = setupProperties.IsFlatbedGrayscaleAllowed;
            IsFlatbedMonochromeAllowed = setupProperties.IsFlatbedMonochromeAllowed;
            IsFlatbedAutoColorAllowed = setupProperties.IsFlatbedAutoColorAllowed;
            IsFlatbedAutoCropSingleRegionAllowed = setupProperties.IsFlatbedAutoCropSingleRegionAllowed;
            IsFlatbedAutoCropMultiRegionAllowed = setupProperties.IsFlatbedAutoCropMultiRegionAllowed;
            FlatbedFormats = new List<ImageScannerFormat> { ImageScannerFormat.Png, ImageScannerFormat.DeviceIndependentBitmap };
            FlatbedResolutions = new List<ScanResolution>
            {
                new ScanResolution(100, ResolutionAnnotation.None),
                new ScanResolution(300, ResolutionAnnotation.Documents),
                new ScanResolution(500, ResolutionAnnotation.None),
                new ScanResolution(700, ResolutionAnnotation.Photos),
            };

            IsFeederAllowed = setupProperties.IsFeederAllowed;
            IsFeederPreviewAllowed = setupProperties.IsFeederPreviewAllowed;
            IsFeederColorAllowed = setupProperties.IsFeederColorAllowed;
            IsFeederGrayscaleAllowed = setupProperties.IsFeederGrayscaleAllowed;
            IsFeederMonochromeAllowed = setupProperties.IsFeederMonochromeAllowed;
            IsFeederAutoColorAllowed = setupProperties.IsFeederAutoColorAllowed;
            IsFeederAutoCropSingleRegionAllowed = setupProperties.IsFeederAutoCropSingleRegionAllowed;
            IsFeederAutoCropMultiRegionAllowed = setupProperties.IsFeederAutoCropMultiRegionAllowed;
            IsFeederDuplexSupported = setupProperties.IsFeederDuplexSupported;
            FeederFormats = new List<ImageScannerFormat> { ImageScannerFormat.Jpeg, ImageScannerFormat.Pdf, ImageScannerFormat.Xps };
            FeederResolutions = new List<ScanResolution>
            {
                new ScanResolution(100, ResolutionAnnotation.None),
                new ScanResolution(150, ResolutionAnnotation.None),
                new ScanResolution(200, ResolutionAnnotation.None),
                new ScanResolution(250, ResolutionAnnotation.None),
                new ScanResolution(300, ResolutionAnnotation.Documents),
                new ScanResolution(400, ResolutionAnnotation.Default),
                new ScanResolution(500, ResolutionAnnotation.None),
                new ScanResolution(600, ResolutionAnnotation.Photos),
                new ScanResolution(700, ResolutionAnnotation.None),
                new ScanResolution(800, ResolutionAnnotation.None),
            };
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

        public Task<ImageScannerScanResult> GetScanAsync()
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
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public struct DebugScannerSetupProperties
    {
        public DebugScannerSetupProperties()
        {

        }

        public string Name = "Debug scanner";

        public bool IsAutoAllowed = true;
        public bool IsAutoPreviewAllowed;

        public bool IsFlatbedAllowed = true;
        public bool IsFlatbedPreviewAllowed = true;
        public bool IsFlatbedColorAllowed = true;
        public bool IsFlatbedGrayscaleAllowed = true;
        public bool IsFlatbedMonochromeAllowed;
        public bool IsFlatbedAutoColorAllowed = true;
        public bool IsFlatbedAutoCropSingleRegionAllowed;
        public bool IsFlatbedAutoCropMultiRegionAllowed;

        public bool IsFeederAllowed = true;
        public bool IsFeederPreviewAllowed;
        public bool IsFeederColorAllowed = true;
        public bool IsFeederGrayscaleAllowed;
        public bool IsFeederMonochromeAllowed = true;
        public bool IsFeederAutoColorAllowed;
        public bool IsFeederAutoCropSingleRegionAllowed = true;
        public bool IsFeederAutoCropMultiRegionAllowed = true;
        public bool IsFeederDuplexSupported = true;
    }
}
