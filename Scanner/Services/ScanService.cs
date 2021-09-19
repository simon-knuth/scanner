using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    /// <summary>
    ///     Interfaces with <see cref="DiscoveredScanner"/>s to request scans and previews.
    /// </summary>
    internal class ScanService : ObservableObject, IScanService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        private bool _IsScanInProgress;
        public bool IsScanInProgress
        {
            get => _IsScanInProgress;
            set
            {
                bool oldValue = _IsScanInProgress;
                SetProperty(ref _IsScanInProgress, value);

                if (!oldValue && value == true)
                {
                    ScanStarted?.Invoke(this, EventArgs.Empty);
                }
                else if (oldValue && value == false)
                {
                    ScanEnded?.Invoke(this, EventArgs.Empty);
                }
            }

        }

        private Progress<uint> _ScanProgress;
        public Progress<uint> ScanProgress
        {
            get => ScanProgress;
            set => ScanProgress = value;
        }

        private CancellationTokenSource ScanCancellationToken;

        public event EventHandler ScanStarted;
        public event EventHandler ScanEnded;
        public event EventHandler<uint> PageScanned;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanService()
        {
            
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<BitmapImage> GetPreviewAsync(DiscoveredScanner scanner, ImageScannerScanSource config)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.Preview,
                new Dictionary<string, string>
                {
                        { "Source", config.ToString() },
                });

            return await scanner.GetPreviewAsync(config);
        }

        public async Task<ImageScannerScanResult> GetScanAsync(DiscoveredScanner scanner,
            ScanOptions options, StorageFolder targetFolder)
        {
            IsScanInProgress = true;
            ImageScannerScanResult result = null;

            try
            {
                if (!scanner.Debug)
                {
                    // real scanner ~> configure scanner and commence scan
                    scanner.ConfigureForScanOptions(options);

                    ScanCancellationToken = new CancellationTokenSource();
                    _ScanProgress = new Progress<uint>();
                    _ScanProgress.ProgressChanged += (x, y) => PageScanned?.Invoke(this, y);
                    result = await scanner.Device.ScanFilesToFolderAsync
                        ((ImageScannerScanSource)((int)options.Source - 1), targetFolder)
                        .AsTask(ScanCancellationToken.Token, _ScanProgress);
                }
                else
                {
                    // debug scanner ~> throw exception
                    await Task.Delay(2000);
                    throw new ArgumentException("Can't scan with a debug scanner, duh");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                IsScanInProgress = false;
            }

            return result;
        }

        public void CancelScan()
        {
            ScanEnded?.Invoke(this, EventArgs.Empty);
            try
            {
                if (ScanCancellationToken != null) ScanCancellationToken.Cancel();
            }
            catch (Exception) { }
            ScanCancellationToken = null;
        }
    }
}
