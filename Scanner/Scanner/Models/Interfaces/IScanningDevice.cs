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
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.Foundation;
using Microsoft.UI.Dispatching;

namespace Scanner.Models.Interfaces
{
    public interface IScanningDevice
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        string Id { get; }
        
        string Name { get; }

        #region Automatic configuration
        public bool IsAutoAllowed { get; }
        public bool IsAutoPreviewAllowed { get; }

        public List<ImageScannerFormat> AutoFormats { get; }
        #endregion

        #region Flatbed
        public bool IsFlatbedAllowed { get; }
        public bool IsFlatbedPreviewAllowed { get; }

        public List<ImageScannerFormat> FlatbedFormats { get; }

        public bool IsFlatbedColorAllowed { get; }
        public bool IsFlatbedGrayscaleAllowed { get; }
        public bool IsFlatbedMonochromeAllowed { get; }
        public bool IsFlatbedAutoColorAllowed { get; }

        public bool IsFlatbedAutoCropSingleRegionAllowed { get; }
        public bool IsFlatbedAutoCropMultiRegionAllowed { get; }
        public bool IsFlatbedAutoCropSupported => IsFlatbedAutoCropSingleRegionAllowed || IsFlatbedAutoCropMultiRegionAllowed;

        public List<ScanResolution> FlatbedResolutions { get; }

        public Size FlatbedMinScanArea { get; }
        public Size FlatbedMaxScanArea { get; }
        #endregion

        #region Feeder
        public bool IsFeederAllowed { get; }
        public bool IsFeederPreviewAllowed { get; }

        public List<ImageScannerFormat> FeederFormats { get; }

        public bool IsFeederColorAllowed { get; }
        public bool IsFeederGrayscaleAllowed { get; }
        public bool IsFeederMonochromeAllowed { get; }
        public bool IsFeederAutoColorAllowed { get; }

        public bool IsFeederAutoCropSingleRegionAllowed { get; }
        public bool IsFeederAutoCropMultiRegionAllowed { get; }
        public bool IsFeederAutoCropSupported => IsFeederAutoCropSingleRegionAllowed || IsFeederAutoCropMultiRegionAllowed;

        public bool IsFeederDuplexSupported { get; }

        public List<ScanResolution> FeederResolutions { get; }

        public Size FeederMinScanArea { get; }
        public Size FeederMaxScanArea { get; }
        #endregion

        #region Scan properties overview
        public bool IsColorAllowedInAnyMode => IsFlatbedColorAllowed || IsFeederColorAllowed;
        public bool IsGrayscaleAllowedInAnyMode => IsFlatbedGrayscaleAllowed || IsFeederGrayscaleAllowed;
        public bool IsMonochromeAllowedInAnyMode => IsFlatbedMonochromeAllowed || IsFeederMonochromeAllowed;
        public bool IsAutoColorAllowedInAnyMode => IsFlatbedAutoColorAllowed || IsFeederAutoColorAllowed;
        #endregion


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task<StorageFile?> GetPreviewScanAsync(ScannerSource sourceMode, StorageFolder targetFolder, bool clearTargetFolder, DispatcherQueue uiDispatcherQueue);
        Task<IReadOnlyList<StorageFile>> GetScanAsync(ScanOptions scanOptions, StorageFolder targetFolder);
        void CancelPreview();
        void CancelScan();
        bool IsPreviewSupported(ScannerSource source);
    }
}
