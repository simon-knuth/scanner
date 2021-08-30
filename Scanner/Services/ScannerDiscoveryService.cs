using Scanner.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Services
{
    class ScannerDiscoveryService : IScannerDiscoveryService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

            TaskCompletionSource<bool> clearList = new TaskCompletionSource<bool>();
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DiscoveredScanners.Clear();
                clearList.SetResult(true);
            });
            await clearList.Task;
            Watcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            Watcher.Added += Watcher_ScannerFound;
            Watcher.Removed += Watcher_ScannerLost;
            Watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

            Watcher.Start();
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
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                // check for duplicate
                foreach (DiscoveredScanner scanner in DiscoveredScanners)
                {
                    if (scanner.Id.ToLower() == args.Id.ToLower())
                    {
                        // duplicate detected ~> ignore
                        return;
                    }
                }

                // add scanner
                try
                {
                    ImageScanner imageScanner = await ImageScanner.FromIdAsync(args.Id);
                    DiscoveredScanner newScanner = new DiscoveredScanner(imageScanner, args.Name);
                    DiscoveredScanners.Add(newScanner);
                }
                catch (Exception) { }
            });
        }

        /// <summary>
        ///     Deletes a lost scanner from <see cref="DiscoveredScanners"/>.
        /// </summary>
        private async void Watcher_ScannerLost(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // find and delete scanner from list
                foreach (DiscoveredScanner scanner in DiscoveredScanners)
                {
                    if (scanner.Id.ToLower() == args.Id.ToLower())
                    {
                        DiscoveredScanners.Remove(scanner);
                        return;
                    }
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
    }
}
