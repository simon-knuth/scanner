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
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using CommunityToolkit.Mvvm.DependencyInjection;
using Windows.Devices.Scanners;
using Scanner.Models;
using Scanner.Models.ScanningDevices;

namespace Scanner.Services
{
    internal partial class ScannerDiscoveryService : IScannerDiscoveryService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Events
        public event EventHandler InitialCrawlCompleted;
        public event EventHandler<IScanningDevice> ScanningDeviceFound;
        public event EventHandler<IScanningDevice> ScanningDeviceLost;
        #endregion

        public TaskCompletionSource InitialCrawlCompletion
        {
            get;
        } = new();

        private readonly List<IScanningDevice> Devices = new();
        private readonly SemaphoreSlim semaphoreDevices = new SemaphoreSlim(1, 1);

        private DeviceWatcher watcher;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScannerDiscoveryService()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<List<IScanningDevice>> GetScanningDevicesAsync()
        {
            await semaphoreDevices.WaitAsync();

            List<IScanningDevice> result = Devices.ToList();

            semaphoreDevices.Release();

            return result;
        }

        public async Task InitializeSearchAsync()
        {
            watcher?.Stop();

            await semaphoreDevices.WaitAsync();

            Devices.Clear();

            semaphoreDevices.Release();

            // trigger search (effectiveness unclear)
            await DeviceInformation.FindAllAsync(DeviceClass.ImageScanner);

            // set up device watcher
            watcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            watcher.Added += Watcher_ScannerFound;
            watcher.Removed += Watcher_ScannerLost;
            watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

            watcher.Start();
            LogService?.Log.Information("ScannerDiscoverService - Initialized");
        }

        private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            InitialCrawlCompleted?.Invoke(this, EventArgs.Empty);
            InitialCrawlCompletion.TrySetResult();
        }

        private async void Watcher_ScannerFound(DeviceWatcher sender, DeviceInformation args)
        {
            int attempt = 1;
            while (attempt <= 2)
            {
                try
                {
                    // construct scanner
                    ImageScanner imageScanner = await ImageScanner.FromIdAsync(args.Id);
                    HardwareScanner scanner = new HardwareScanner(imageScanner, args.Name);

                    await TryAddScannerAsync(scanner);
                    LogService?.Log.Information("ScannerDiscoveryScanner - Found and added {@Scanner}", scanner);

                    // analytics
                    if (scanner != null) SendScannerAnalytics(scanner);

                    return;
                }
                catch (Exception exc)
                {
                    LogService?.Log.Warning(exc, "ScannerDiscoveryService - Failed to add scanner ({Attempt})", attempt);
                    if (attempt < 2)
                    {
                        // scanner may just be blocked by another app, try again
                        await Task.Delay(5000);
                    }
                    attempt++;
                }
            }
        }

        private async void Watcher_ScannerLost(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await TryRemoveScannerByIdAsync(args.Id);
        }

        private async Task TryRemoveScannerAsync(IScanningDevice scanner)
        {
            try
            {
                await semaphoreDevices.WaitAsync();
                Devices.Remove(scanner);
                ScanningDeviceLost?.Invoke(this, scanner);
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "ScannerDiscoveryService - Failed to remove {@Device}", scanner);
                return;
            }
            finally
            {
                semaphoreDevices.Release();
            }
        }

        private async Task TryRemoveScannerByIdAsync(string scannerId)
        {
            try
            {
                // search for scanner
                await semaphoreDevices.WaitAsync();
                IScanningDevice? device = Devices.FirstOrDefault((x) => x.Id == scannerId);

                // remove scanner
                if (device != null)
                {
                    Devices.Remove(device);
                    ScanningDeviceLost?.Invoke(this, device);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "ScannerDiscoveryService - Failed to remove device by {Id}", scannerId);
                return;
            }
            finally
            {
                semaphoreDevices.Release();
            }
        }

        public async Task AddDebugScannerAsync(DebugScanner scanner)
        {
            await TryAddScannerAsync(scanner);
        }

        public async Task RemoveDebugScannerAsync(DebugScanner scanner)
        {
            await TryRemoveScannerAsync(scanner);
        }

        private async Task TryAddScannerAsync(IScanningDevice scanner)
        {
            try
            {
                // check for duplicate
                await semaphoreDevices.WaitAsync();
                if (Devices.Exists((x) => x.Id == scanner.Id))
                {
                    // duplicate detected ~> ignore
                    LogService?.Log.Information("ScannerDiscoveryService - Found duplicate {@Scanner}", scanner);
                    return;
                }

                // add scanner
                Devices.Add(scanner);
                ScanningDeviceFound?.Invoke(this, scanner);
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "ScannerDiscoveryService - Failed to add discovered {@Scanner}", scanner);
                return;
            }
            finally
            {
                semaphoreDevices.Release();
            }
        }

        public void SendScannerAnalytics(HardwareScanner scanner)
        {
            //string formatCombination = "";
            //bool jpgSupported, pngSupported, pdfSupported, xpsSupported, oxpsSupported, tifSupported, bmpSupported;
            //jpgSupported = pngSupported = pdfSupported = xpsSupported = oxpsSupported = tifSupported = bmpSupported = false;

            //try
            //{
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.Jpeg))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|JPG");
            //        jpgSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.Png))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|PNG");
            //        pngSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.Pdf))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|PDF");
            //        pdfSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.Xps))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|XPS");
            //        xpsSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.OpenXps))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|OXPS");
            //        oxpsSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.Tiff))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|TIF");
            //        tifSupported = true;
            //    }
            //    if (scanner.AutoFormats.Contains(ImageScannerFormat.DeviceIndependentBitmap))
            //    {
            //        formatCombination = formatCombination.Insert(formatCombination.Length, "|BMP");
            //        bmpSupported = true;
            //    }

            //    formatCombination = formatCombination.Insert(formatCombination.Length, "|");


            //    AppCenterService?.TrackEvent(AppCenterEvent.ScannerAdded, new Dictionary<string, string> {
            //                { "formatCombination", formatCombination },
            //                { "jpgSupported", jpgSupported.ToString() },
            //                { "pngSupported", pngSupported.ToString() },
            //                { "pdfSupported", pdfSupported.ToString() },
            //                { "xpsSupported", xpsSupported.ToString() },
            //                { "oxpsSupported", oxpsSupported.ToString() },
            //                { "tifSupported", tifSupported.ToString() },
            //                { "bmpSupported", bmpSupported.ToString() },
            //                { "hasAuto", scanner.IsAutoAllowed.ToString() },
            //                { "hasFlatbed", scanner.IsFlatbedAllowed.ToString() },
            //                { "hasFeeder", scanner.IsFeederAllowed.ToString() },
            //                { "autoPreviewSupported", scanner.IsAutoPreviewAllowed.ToString() },
            //                { "flatbedPreviewSupported", scanner.IsFlatbedPreviewAllowed.ToString() },
            //                { "feederPreviewSupported", scanner.IsFeederPreviewAllowed.ToString() },
            //                { "feederAutoCropPossible", scanner.IsFeederAutoCropPossible.ToString() },
            //                { "feederAutoCropSingleSupported", scanner.IsFeederAutoCropSingleRegionAllowed.ToString() },
            //                { "feederAutoCropMultiSupported", scanner.IsFeederAutoCropMultiRegionAllowed.ToString() },
            //            });
            //}
            //catch (Exception) { }
        }
    }
}
