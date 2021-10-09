using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/>.
    /// </summary>
    public interface IScanResultService
    {
        event EventHandler<ScanResult> ScanResultCreated;
        event EventHandler ScanResultDismissed;
        event EventHandler ScanResultChanging;
        event EventHandler ScanResultChanged;

        bool IsScanResultChanging
        {
            get;
        }

        ScanResult Result
        {
            get;
        }

        Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            bool fixedFolder);
        Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat targetFormat,
            StorageFolder targetFolder);

        Task<bool> CropScanAsync(int index, ImageCropper imageCropper);
        Task<bool> CropScanAsCopyAsync(int index, ImageCropper imageCropper);
        Task<bool> RotatePagesAsync(IList<Tuple<int, BitmapRotation>> instructions);
        Task<bool> DrawOnScanAsync(int index, InkCanvas inkCanvas);
        Task<bool> DrawOnScanAsCopyAsync(int index, InkCanvas inkCanvas);
        Task<bool> RenameAsync(int index, string newName);
        Task<bool> RenameAsync(string newName);
        Task<bool> DeleteScanAsync(int index);
        Task<bool> DeleteScanAsync(int index, StorageDeleteOption deleteOption);
        Task<bool> DeleteScansAsync(List<int> indices);
        Task<bool> DeleteScansAsync(List<int> indices, StorageDeleteOption deleteOption);
        Task<bool> CopyAsync();
        Task<bool> CopyImageAsync(int index);
        Task<bool> CopyImagesAsync();
        Task<bool> CopyImagesAsync(IList<int> indices);
        Task OpenWithAsync();
        Task OpenWithAsync(AppInfo appInfo);
        Task OpenImageWithAsync(int index);
        Task OpenImageWithAsync(int index, AppInfo appInfo);
    }
}
