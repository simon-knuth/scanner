using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    /// <summary>
    ///     Interfaces with <see cref="DiscoveredScanner"/>s to request scans and previews.
    /// </summary>
    public interface IScanService
    {
        event EventHandler ScanStarted;
        event EventHandler ScanEnded;

        bool IsScanInProgress
        {
            get;
        }

        Progress<uint> ScanProgress
        {
            get;
        }

        int CompletedScans
        {
            get;
        }

        Task<BitmapImage> GetPreviewAsync(DiscoveredScanner scanner, ImageScannerScanSource config);
        Task<ImageScannerScanResult> GetScanAsync(DiscoveredScanner scanner, ScanOptions options, StorageFolder targetFolder);
        void CancelScan();
    }
}
