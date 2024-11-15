﻿using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Helpers;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/> and exposes it to the code.
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
            ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress);
        Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            ImageScannerFormat targetFormat, ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress);
        Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat,
            ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress);
        Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat,
            StorageFolder targetFolder, ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress);
        Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat,
            ScanMergeConfig mergeConfig, ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress);
        
        void DismissScanResult();

        Task<bool> CropScansAsync(List<int> indices, Rect cropRegion);
        Task<bool> CropScanAsCopyAsync(List<int> indices, Rect cropRegion);
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
        Task<bool> OpenWithAsync();
        Task<bool> OpenWithAsync(AppInfo appInfo);
        Task<bool> OpenImageWithAsync(int index);
        Task<bool> OpenImageWithAsync(int index, AppInfo appInfo);
        Task<bool> DuplicatePageAsync(int index);
        Task ExportScansAsync(List<int> indices, StorageFolder targetFolder);
        Task ExportScanAsync(int index, StorageFolder targetFolder, string name);
        Task ExportScanAsync(int index, StorageFile targetFile, string name);
        Task ApplyElementOrderToFilesAsync();
    }
}
