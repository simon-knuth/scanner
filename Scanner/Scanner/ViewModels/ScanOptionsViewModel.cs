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
using Windows.ApplicationModel.Appointments.AppointmentsProvider;
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
        public AsyncRelayCommand DebugRemoveScannerCommand => new AsyncRelayCommand(RemoveDebugScannerAsync);
        public RelayCommand<DispatcherQueue> LoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private ScanOptions scanOptions;
        public ScanOptions ScanOptions
        {
            get => scanOptions;
            set
            {
                if (scanOptions != null)
                {
                    scanOptions.PropertyChanged -= ScanOptions_PropertyChanged;
                }

                SetProperty(ref scanOptions, value);

                if (scanOptions != null)
                {
                    scanOptions.PropertyChanged += ScanOptions_PropertyChanged;
                }
            }
        }

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

        [ObservableProperty]
        private bool isScanning;

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

        private async Task RemoveDebugScannerAsync()
        {
            if (SelectedScanner is DebugScanner debugScanner)
            {
                await ScannerDiscoveryService.RemoveDebugScannerAsync(debugScanner);
            }
        }

        private void ScanOptions_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScanOptions.SourceMode):
                    UpdateScanOptionsForSourceMode();
                    break;
            }
        }

        private void UpdateScanOptionsForSourceMode()
        {
            switch (ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    // color mode
                    switch (ScanOptions.ColorMode)
                    {
                        case ScannerColorMode.None:
                        case ScannerColorMode.Color:
                            if (ScanOptions.Scanner.IsFlatbedColorAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Color;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            break;
                        case ScannerColorMode.Grayscale:
                            if (ScanOptions.Scanner.IsFlatbedColorAllowed || ScanOptions.Scanner.IsFlatbedGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            break;
                        case ScannerColorMode.Monochrome:
                            if (ScanOptions.Scanner.IsFlatbedColorAllowed || ScanOptions.Scanner.IsFlatbedMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            break;
                    }

                    // resolution
                    ScanOptions.SetDefaultResolution(ScanOptions.Scanner.FlatbedResolutions);

                    // auto crop mode
                    switch (ScanOptions.AutoCropMode)
                    {
                        case ScannerAutoCropMode.None:
                        case ScannerAutoCropMode.Disabled:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.SingleRegion:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropSingleRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropMultiRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.MultipleRegions:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropMultiRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSingleRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                    }
                    break;
                case ScannerSource.Feeder:
                    // color mode
                    switch (ScanOptions.ColorMode)
                    {
                        case ScannerColorMode.None:
                        case ScannerColorMode.Color:
                            if (ScanOptions.Scanner.IsFeederColorAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Color;
                            }
                            else if (ScanOptions.Scanner.IsFeederGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            else if (ScanOptions.Scanner.IsFeederMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            break;
                        case ScannerColorMode.Grayscale:
                            if (ScanOptions.Scanner.IsFeederColorAllowed || ScanOptions.Scanner.IsFeederGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            else if (ScanOptions.Scanner.IsFeederMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            break;
                        case ScannerColorMode.Monochrome:
                            if (ScanOptions.Scanner.IsFeederColorAllowed || ScanOptions.Scanner.IsFeederMonochromeAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                            }
                            else if (ScanOptions.Scanner.IsFeederGrayscaleAllowed)
                            {
                                ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                            }
                            break;
                    }

                    // resolution
                    ScanOptions.SetDefaultResolution(ScanOptions.Scanner.FeederResolutions);

                    // auto crop mode
                    switch (ScanOptions.AutoCropMode)
                    {
                        case ScannerAutoCropMode.None:
                        case ScannerAutoCropMode.Disabled:
                            if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.SingleRegion:
                            if (ScanOptions.Scanner.IsFeederAutoCropSingleRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropMultiRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.MultipleRegions:
                            if (ScanOptions.Scanner.IsFeederAutoCropMultiRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSingleRegionAllowed)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                ScanOptions.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                    }
                    break;
            }
        }
    }
}
