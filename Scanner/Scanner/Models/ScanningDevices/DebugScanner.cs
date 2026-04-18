using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Scanner.AppWindows;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.WindowManagement;
using WinRT.Interop;

namespace Scanner.Models.ScanningDevices;

public partial class DebugScanner : IScanningDevice
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
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

    public Size FlatbedMinScanArea { get; private set; } = new Size(1, 1);
    public Size FlatbedMaxScanArea { get; private set; } = new Size(13, 9);
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

    public Size FeederMinScanArea { get; } = new Size(3, 3);
    public Size FeederMaxScanArea { get; } = new Size(9, 13);
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public DebugScanner(DebugScannerSetupProperties setupProperties)
    {
        Id = setupProperties.Id;
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
        
    }

    public void CancelScan()
    {
        
    }

    public async Task<StorageFile?> GetPreviewScanAsync(ScannerSource sourceMode, StorageFolder targetFolder, bool clearTargetFolder, DispatcherQueue uiDispatcherQueue)
    {
        // empty target folder
        if (clearTargetFolder)
            await AppDataService.EmptyFolderAsync(targetFolder);

        IReadOnlyList<PickFileResult> pickerResults = await Helpers.Helpers.PickInputFilesAsync(false, uiDispatcherQueue);
        if (pickerResults.Count == 0 || pickerResults[0] == null)
            return null;

        // get files
        StorageFile[] files = new StorageFile[pickerResults.Count];
        for (int i = 0; i < pickerResults.Count; i++)
        {
            files[i] = await StorageFile.GetFileFromPathAsync(pickerResults[i].Path);
        }

        // copy to target folder
        List<Task<StorageFile>> copytasks = new();
        foreach (StorageFile file in files)
        {
            copytasks.Add(file.CopyAsync(targetFolder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask());
        }
        StorageFile[] results = await Task.WhenAll(copytasks);

        return results[0];
    }

    public async Task<IReadOnlyList<StorageFile>> GetScanAsync(ScanOptions scanOptions, StorageFolder targetFolder, DispatcherQueue uiDispatcherQueue)
    {
        IReadOnlyList<PickFileResult> pickerResults = await Helpers.Helpers.PickInputFilesAsync(scanOptions.SourceMode != ScannerSource.Flatbed, uiDispatcherQueue);

        // get files
        StorageFile[] files = new StorageFile[pickerResults.Count];
        for (int i = 0; i < pickerResults.Count; i++)
        {
            files[i] = await StorageFile.GetFileFromPathAsync(pickerResults[i].Path);
        }

        // copy to target folder
        List<Task<StorageFile>> copyTasks = [];
        foreach (StorageFile file in files)
        {
            copyTasks.Add(file.CopyAsync(targetFolder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask());
        }
        StorageFile[] results = await Task.WhenAll(copyTasks);

        return results;
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


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public struct DebugScannerSetupProperties
{
    public DebugScannerSetupProperties()
    {

    }

    public string Name = "Debug scanner";
    public string Id = "DEBUG";

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
