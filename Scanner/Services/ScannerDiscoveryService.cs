using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Services
{
    /// <summary>
    ///     Searches for and lists discovered wired/wireless scanners.
    /// </summary>
    internal class ScannerDiscoveryService : IScannerDiscoveryService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        private ObservableCollection<DiscoveredScanner> _DiscoveredScanners = new ObservableCollection<DiscoveredScanner>();
        public ObservableCollection<DiscoveredScanner> DiscoveredScanners
        {
            get => _DiscoveredScanners;
        }

        /// <summary>
        ///     Invoked when the initial crawl after restarting is complete.
        ///     The search still continues after this.
        /// </summary>
        public event EventHandler InitialCrawlCompleted;

        private DeviceWatcher Watcher;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task RestartSearchAsync()
        {
            Watcher?.Stop();

            await RunOnUIThreadAndWaitAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DiscoveredScanners.Clear();
            });
            Watcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            Watcher.Added += Watcher_ScannerFound;
            Watcher.Removed += Watcher_ScannerLost;
            Watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

            Watcher.Start();
            LogService?.Log.Information("Restarted ScannerDiscoveryService.");
        }

        /// <summary>
        ///     Raises <see cref="InitialCrawlCompleted"/> when the initial crawl after restarting
        ///     the <see cref="Watcher"/> is complete. The <see cref="Watcher"/> still continues
        ///     searching after this.
        /// </summary>
        private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            InitialCrawlCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Adds a newly dicovered scanner to <see cref="DiscoveredScanners"/> if it isn't a
        ///     duplicate.
        /// </summary>
        private async void Watcher_ScannerFound(DeviceWatcher sender, DeviceInformation args)
        {
            DiscoveredScanner newScanner = null;
            await RunOnUIThreadAndWaitAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                // check for duplicate
                foreach (DiscoveredScanner scanner in DiscoveredScanners)
                {
                    if (!scanner.Debug && scanner.Id.ToLower() == args.Id.ToLower())
                    {
                        // duplicate detected ~> ignore
                        LogService?.Log.Information("Wanted to add scanner {@Device}, but it's a duplicate.", args);
                        return;
                    }
                }

                // add scanner
                try
                {
                    ImageScanner imageScanner = await ImageScanner.FromIdAsync(args.Id);
                    newScanner = new DiscoveredScanner(imageScanner, args.Name);
                    DiscoveredScanners.Add(newScanner);
                    LogService?.Log.Information("Added scanner {@Device}.", args);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to add scanner {@Device} to existing {ScannerList}.", args, DiscoveredScanners);
                    return;
                }
            });

            // send analytics
            if (newScanner != null) SendScannerAnalytics(newScanner);
        }

        /// <summary>
        ///     Deletes a lost scanner from <see cref="DiscoveredScanners"/>.
        /// </summary>
        private async void Watcher_ScannerLost(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // find and delete scanner from list
                try
                {
                    foreach (DiscoveredScanner scanner in DiscoveredScanners)
                    {
                        if (scanner.Id.ToLower() == args.Id.ToLower())
                        {
                            DiscoveredScanners.Remove(scanner);
                            LogService?.Log.Information("Removed scanner {@Device}.", args);
                            return;
                        }
                    }
                    LogService?.Log.Warning("Attempted to remove scanner {@Device} but couldn't find it in the list.", args);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Warning(exc, "Removing the scanner {@Device} failed.", args);
                }
            });
        }

        public async Task AddDebugScannerAsync(DiscoveredScanner scanner)
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // add debug scanner
                DiscoveredScanners.Add(scanner);
            });
        }

        /// <summary>
        ///     Send information on a new scanner to App Center
        /// </summary>
        public void SendScannerAnalytics(DiscoveredScanner scanner)
        {
            string formatCombination = "";
            bool jpgSupported, pngSupported, pdfSupported, xpsSupported, oxpsSupported, tifSupported, bmpSupported;
            jpgSupported = pngSupported = pdfSupported = xpsSupported = oxpsSupported = tifSupported = bmpSupported = false;

            try
            {
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.Jpeg) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|JPG");
                    jpgSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.Png) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|PNG");
                    pngSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.Pdf) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|PDF");
                    pdfSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.Xps) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|XPS");
                    xpsSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.OpenXps) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|OXPS");
                    oxpsSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.Tiff) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|TIF");
                    tifSupported = true;
                }
                if (scanner.AutoFormats.FirstOrDefault(format => format.TargetFormat == ImageScannerFormat.DeviceIndependentBitmap) != null)
                {
                    formatCombination = formatCombination.Insert(formatCombination.Length, "|BMP");
                    bmpSupported = true;
                }

                formatCombination = formatCombination.Insert(formatCombination.Length, "|");


                AppCenterService?.TrackEvent(AppCenterEvent.ScannerAdded, new Dictionary<string, string> {
                            { "formatCombination", formatCombination },
                            { "jpgSupported", jpgSupported.ToString() },
                            { "pngSupported", pngSupported.ToString() },
                            { "pdfSupported", pdfSupported.ToString() },
                            { "xpsSupported", xpsSupported.ToString() },
                            { "oxpsSupported", oxpsSupported.ToString() },
                            { "tifSupported", tifSupported.ToString() },
                            { "bmpSupported", bmpSupported.ToString() },
                            { "hasAuto", scanner.IsAutoAllowed.ToString() },
                            { "hasFlatbed", scanner.IsFlatbedAllowed.ToString() },
                            { "hasFeeder", scanner.IsFeederAllowed.ToString() },
                            { "autoPreviewSupported", scanner.IsAutoPreviewAllowed.ToString() },
                            { "flatbedPreviewSupported", scanner.IsFlatbedPreviewAllowed.ToString() },
                            { "feederPreviewSupported", scanner.IsFeederPreviewAllowed.ToString() },
                            { "feederAutoCropPossible", scanner.IsFeederAutoCropPossible.ToString() },
                            { "feederAutoCropSingleSupported", scanner.IsFeederAutoCropSingleRegionAllowed.ToString() },
                            { "feederAutoCropMultiSupported", scanner.IsFeederAutoCropMultiRegionAllowed.ToString() },
                        });
            }
            catch (Exception) { }
        }
    }
}
