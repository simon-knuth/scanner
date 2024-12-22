using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.WebUI;

namespace Scanner.ViewModels
{
    partial class ScanOptionsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        #endregion

        #region Commands
        public AsyncRelayCommand DebugAddScannerCommand => new AsyncRelayCommand(AddDebugScannerAsync);
        public RelayCommand<DispatcherQueue> LoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        private ScanOptions scanOptions = new();

        private IScanningDevice selectedScanner;
        public IScanningDevice SelectedScanner
        {
            get => selectedScanner;
            set
            {
                SetProperty(ref selectedScanner, value);
                ScanOptions = new ScanOptions(value);
                OnPropertyChanged(nameof(AreScanOptionsAvailable));
            }
        }

        public bool AreScanOptionsAvailable => SelectedScanner != null;

        public ObservableCollection<IScanningDevice> Scanners = new();
        private SemaphoreSlim semaphoreScanners = new SemaphoreSlim(1, 1);

        public DebugScannerSetupProperties DebugScannerSetupProperties = new();

        private TaskCompletionSource viewLoading = new();
        private DispatcherQueue viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsViewModel()
        {
            ScannerDiscoveryService.ScanningDeviceFound += ScannerDiscoveryService_ScanningDeviceFound;
            ScannerDiscoveryService.ScanningDeviceLost += ScannerDiscoveryService_ScanningDeviceLost;
            ScannerDiscoveryService.InitialCrawlCompleted += ScannerDiscoveryService_InitialCrawlCompleted;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void ViewLoading(DispatcherQueue? dispatcherQueue)
        {
            if (dispatcherQueue != null)
            {
                viewDispatcherQueue = dispatcherQueue;
            }
            viewLoading.TrySetResult();
        }

        private async void ScannerDiscoveryService_ScanningDeviceFound(object? sender, IScanningDevice e)
        {
            await semaphoreScanners.WaitAsync();
            Scanners.Add(e);
            semaphoreScanners.Release();

            if (SelectedScanner == null && ScannerDiscoveryService.InitialCrawlCompletion.Task.IsCompletedSuccessfully)
            {
                await SelectBestAvailableScannerAsync();
            }
        }

        private async void ScannerDiscoveryService_ScanningDeviceLost(object? sender, IScanningDevice e)
        {
            await semaphoreScanners.WaitAsync();
            Scanners.Remove(e);
            semaphoreScanners.Release();
        }

        private async void ScannerDiscoveryService_InitialCrawlCompleted(object? sender, EventArgs e)
        {
            await SelectBestAvailableScannerAsync();
        }

        private async Task SelectBestAvailableScannerAsync()
        {
            await semaphoreScanners.WaitAsync();
            if (Scanners.Count > 0)
            {
                await viewLoading.Task;
                viewDispatcherQueue.RunOnThread(DispatcherQueuePriority.High, () =>
                {
                    SelectedScanner = Scanners[0];
                });
            }
            semaphoreScanners.Release();
        }

        private async Task AddDebugScannerAsync()
        {
            DebugScanner debugScanner = new(DebugScannerSetupProperties);
            await ScannerDiscoveryService.AddDebugScannerAsync(debugScanner);
        }
    }
}
