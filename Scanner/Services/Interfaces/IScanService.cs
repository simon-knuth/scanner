using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    public interface IScanService
    {
        event EventHandler ScanStarted;
        event EventHandler ScanCompleted;

        bool IsScanInProgress
        {
            get;
        }
        
        Task<BitmapImage> GetPreviewAsync(DiscoveredScanner scanner, ImageScannerScanSource config);
    }
}
