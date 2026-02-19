using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Models.ScanningDevices;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.WebUI;

namespace Scanner.ViewModels;

partial class ScanOptionsViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
    public readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
    private readonly IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
    public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Commands
    public AsyncRelayCommand DebugAddScannerCommand => new AsyncRelayCommand(AddDebugScannerAsync);
    public AsyncRelayCommand DebugRemoveScannerCommand => new AsyncRelayCommand(RemoveDebugScannerAsync);
    public RelayCommand ResetBrightnessCommand => new RelayCommand(ResetBrightness);
    public RelayCommand ResetContrastCommand => new RelayCommand(ResetContrast);
    public AsyncRelayCommand UpdateScanAreaAlignmentBitmapAsyncCommand => new AsyncRelayCommand(UpdateScanAreaAlignmentBitmapAsync);
    public AsyncRelayCommand<DispatcherQueue> ViewLoadingAsyncCommand => new AsyncRelayCommand<DispatcherQueue>(ViewLoading);
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    private ScanOptions scanOptions = new(null, false);
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

    private IScanningDevice? selectedScanner;
    public IScanningDevice? SelectedScanner
    {
        get => selectedScanner;
        set
        {
            if (SetProperty(ref selectedScanner, value))
            {
                ScanOptions = new ScanOptions(value, true);
                OnPropertyChanged(nameof(AreScanOptionsAvailable));
                _ = CleanUpScanAreaAlignmentBitmapAsync();
                Messenger.Send(new SelectedScannerChangedMessage(value));
            }
        }
    }

    public bool AreScanOptionsAvailable => SelectedScanner != null && !ProjectService.IsScanProcessRunning;

    public ObservableCollection<IScanningDevice> Scanners = new();
    public SemaphoreSlim SemaphoreScanners = new SemaphoreSlim(1, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanAreaAlignmentBitmapUri))]
    private StorageFile? scanAreaAlignmentBitmap;

    public Uri? ScanAreaAlignmentBitmapUri => ScanAreaAlignmentBitmap != null ? new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.PreviewScanFolder, ScanAreaAlignmentBitmap.Name)) : null;

    public DebugScannerSetupProperties DebugScannerSetupProperties = new();

    private TaskCompletionSource viewLoading = new();
    private DispatcherQueue? viewDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanOptionsViewModel()
    {
        ScannerDiscoveryService.ScanningDeviceLost += ScannerDiscoveryService_ScanningDeviceLost;

        ProjectService.PropertyChanged += ProjectService_PropertyChanged;

        Messenger.Register<SelectedScannerRequestMessage>(this, (r, m) =>
        {
            m.Reply(SelectedScanner);
        });
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    private async Task ViewLoading(DispatcherQueue? dispatcherQueue)
    {
        if (dispatcherQueue != null)
        {
            viewDispatcherQueue = dispatcherQueue;
        }
        viewLoading.TrySetResult();

        await ScannerDiscoveryService.InitialCrawlCompletion.Task;
        await SemaphoreScanners.WaitAsync();
        ScannerDiscoveryService.ScanningDeviceFound += ScannerDiscoveryService_ScanningDeviceFound;
        foreach (IScanningDevice device in await ScannerDiscoveryService.GetScanningDevicesAsync())
        {
            Scanners.Add(device);
        }
        SemaphoreScanners.Release();
        await SelectBestAvailableScannerAsync();
    }

    private async void ScannerDiscoveryService_ScanningDeviceFound(object? sender, IScanningDevice e)
    {
        await SemaphoreScanners.WaitAsync();

        if (!Scanners.Contains(e))
            Scanners.Add(e);

        SemaphoreScanners.Release();

        if (SelectedScanner == null)
            await SelectBestAvailableScannerAsync();
    }

    private async void ScannerDiscoveryService_ScanningDeviceLost(object? sender, IScanningDevice e)
    {
        await SemaphoreScanners.WaitAsync();
        Scanners.Remove(e);
        SemaphoreScanners.Release();
    }

    private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IProjectService.IsScanProcessRunning))
            OnPropertyChanged(nameof(AreScanOptionsAvailable));
    }

    private async Task SelectBestAvailableScannerAsync()
    {
        await SemaphoreScanners.WaitAsync();
        if (Scanners.Count > 0)
        {
            await viewLoading.Task;
            viewDispatcherQueue!.RunOnThread(DispatcherQueuePriority.High, () =>
            {
                SelectedScanner = Scanners[0];
            });
        }
        SemaphoreScanners.Release();
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

    private async void ScanOptions_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScanOptions.SourceMode):
                UpdateScanOptionsForSourceMode();
                await CleanUpScanAreaAlignmentBitmapAsync();
                break;
        }
    }

    private async Task CleanUpScanAreaAlignmentBitmapAsync()
    {
        if (ScanAreaAlignmentBitmap == null)
            return;

        StorageFile file = ScanAreaAlignmentBitmap;
        await Task.Run(async () => await file.DeleteAsync(StorageDeleteOption.PermanentDelete));

        ScanAreaAlignmentBitmap = null;
    }

    private void UpdateScanOptionsForSourceMode()
    {
        switch (ScanOptions?.SourceMode)
        {
            case ScannerSource.Auto:
                ScanOptions.ColorMode = ScannerColorMode.None;
                ScanOptions.ScanArea = null;
                ScanOptions.Brightness = 0;
                ScanOptions.Contrast = 0;
                break;
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

                // region selection
                if (ScanOptions.ScanArea is AutoCropArea autoCropRegionFlatbed)
                {
                    switch (autoCropRegionFlatbed.AutoCropMode)
                    {
                        case ScannerAutoCropMode.None:
                        case ScannerAutoCropMode.Disabled:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.SingleRegion:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropSingleRegionAllowed)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropMultiRegionAllowed)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.MultipleRegions:
                            if (ScanOptions.Scanner.IsFlatbedAutoCropMultiRegionAllowed)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSingleRegionAllowed)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFlatbedAutoCropSupported)
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFlatbed.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                    }
                }
                else if (ScanOptions.ScanArea is PreviewSelectionArea previewSelectionRegion)
                {
                    ScanOptions.ScanArea = null;
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

                // feeder options
                ScanOptions.ScanMultiplePages = true;
                ScanOptions.Duplex = false;

                // region selection
                if (ScanOptions.ScanArea is AutoCropArea autoCropRegionFeeder)
                {
                    switch (autoCropRegionFeeder.AutoCropMode)
                    {
                        case ScannerAutoCropMode.None:
                        case ScannerAutoCropMode.Disabled:
                            if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.SingleRegion:
                            if (ScanOptions.Scanner.IsFeederAutoCropSingleRegionAllowed)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropMultiRegionAllowed)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                        case ScannerAutoCropMode.MultipleRegions:
                            if (ScanOptions.Scanner.IsFeederAutoCropMultiRegionAllowed)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSingleRegionAllowed)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.SingleRegion;
                            }
                            else if (ScanOptions.Scanner.IsFeederAutoCropSupported)
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.Disabled;
                            }
                            else
                            {
                                autoCropRegionFeeder.AutoCropMode = ScannerAutoCropMode.None;
                            }
                            break;
                    }
                }
                break;
        }
    }

    private void ResetBrightness()
    {
        if (ScanOptions != null)
            ScanOptions.Brightness = 0;
    }

    private void ResetContrast()
    {
        if (ScanOptions != null)
            ScanOptions.Contrast = 0;
    }

    private async Task UpdateScanAreaAlignmentBitmapAsync()
    {
        await CleanUpScanAreaAlignmentBitmapAsync();

        if (SelectedScanner == null)
            return;

        ScanAreaAlignmentBitmap = await SelectedScanner.GetPreviewScanAsync(ScanOptions.SourceMode, AppDataService.PreviewScanFolder, true, viewDispatcherQueue);
    }
}
