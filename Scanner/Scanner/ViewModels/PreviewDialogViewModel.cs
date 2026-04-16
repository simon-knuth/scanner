using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Models.ScanningDevices;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using static Scanner.Helpers.Helpers;

namespace Scanner.ViewModels;

public partial class PreviewDialogViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
    public readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
    public readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    public readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Events
    public event EventHandler CloseRequested;
    #endregion

    #region Commands
    public RelayCommand ApplyAndCloseCommand => new RelayCommand(() => Close(true));
    public RelayCommand CloseCommand => new RelayCommand(() => Close(false));
    public RelayCommand<Rect> AspectRatioFlipCommand;
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    [ObservableProperty]
    private bool isPreviewRunning = true;

    [ObservableProperty]
    private BitmapImage previewImage;

    [ObservableProperty]
    private bool hasPreviewSucceeded;

    [ObservableProperty]
    private bool hasPreviewFailed;

    private bool isCustomRegionSelected;
    public bool IsCustomRegionSelected
    {
        get => isCustomRegionSelected;
        set
        {
            LogService.Log.Information($"PreviewDialogViewModel: Setting IsCustomRegionSelected to {value}");

            if (value == true && previewFileBuffer != null)
            {
                PreviewFile = previewFileBuffer;
                previewFileBuffer = null;
            }

            SetProperty(ref isCustomRegionSelected, value);
        }
    }

    [ObservableProperty]
    private StorageFile previewFile;

    [ObservableProperty]
    private double inchesPerPixel;

    [ObservableProperty]
    private MeasurementValue minLength;

    [ObservableProperty]
    private MeasurementValue minWidthForAspectRatio;

    [ObservableProperty]
    private MeasurementValue minHeightForAspectRatio;

    [ObservableProperty]
    private MeasurementValue maxWidth;

    [ObservableProperty]
    private MeasurementValue maxWidthForAspectRatio;

    [ObservableProperty]
    private MeasurementValue maxHeightForAspectRatio;

    [ObservableProperty]
    private MeasurementValue maxHeight;

    [ObservableProperty]
    private MeasurementValue selectedX;

    [ObservableProperty]
    private MeasurementValue selectedY;

    private MeasurementValue selectedWidth;
    public MeasurementValue SelectedWidth
    {
        get => selectedWidth;
        set
        {
            try
            {
                if (value.Pixels > MaxWidthForAspectRatio.Pixels)
                {
                    // exceeds max width
                    SelectedWidth = MaxWidthForAspectRatio;
                    return;
                }

                if (value != null && SelectedX != null && value.Pixels + SelectedX.Pixels > MaxWidth.Pixels)
                {
                    // exceeds bounds, check whether moving the selection to the left would help
                    if (value.Pixels <= MaxWidth.Pixels)
                    {
                        // move selection to the left to allow new width
                        SelectedX = new MeasurementValue(MeasurementType.Pixels, MaxWidth.Pixels - value.Pixels, InchesPerPixel);
                    }
                }

                if (SelectedWidth != null && SelectedHeight != null && SelectedAspectRatio != AspectRatio.Custom
                    && Math.Abs(SelectedWidth.Pixels - value.Pixels) > 0.1)
                {
                    selectedWidth = value;
                    OnPropertyChanged(nameof(SelectedWidth));

                    // change height according to aspect ratio
                    double newHeightPixels = value.Pixels / (double)SelectedAspectRatioValue;

                    if (Math.Abs(newHeightPixels - SelectedHeight.Pixels) > 0.1)
                    {
                        SelectedHeight = new MeasurementValue(MeasurementType.Pixels, newHeightPixels, InchesPerPixel);
                    }
                }
                else
                {
                    selectedWidth = value;
                    OnPropertyChanged(nameof(SelectedWidth));
                }
            }
            catch (Exception exc)
            {
                Messenger.Send(new ShowInAppNotificationMessage(new()
                {
                    Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageHeading),
                    Message = $"{GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageBody)}\n{exc.Message}",
                    Severity = InfoBarSeverity.Warning,
                }));
                LogService.Log.Error(exc, $"Setting SelectedWidth to {value} failed");
                SentryService.TrackError(exc);
                Close(false);
            }
        }
    }

    private MeasurementValue selectedHeight;
    public MeasurementValue SelectedHeight
    {
        get => selectedHeight;
        set
        {
            try
            {
                if (value.Pixels > MaxHeightForAspectRatio.Pixels)
                {
                    // exceeds max height
                    SelectedWidth = MaxWidthForAspectRatio;
                    return;
                }


                if (value != null && SelectedY != null && value.Pixels + SelectedY.Pixels > MaxHeight.Pixels)
                {
                    // exceeds bounds, check whether moving the selection to the left would help
                    if (value.Pixels <= MaxHeight.Pixels)
                    {
                        // move selection to the left to allow new height
                        SelectedY = new MeasurementValue(MeasurementType.Pixels, MaxHeight.Pixels - value.Pixels, InchesPerPixel);
                    }
                }

                if (SelectedHeight != null && SelectedWidth != null && SelectedAspectRatio != AspectRatio.Custom
                    && Math.Abs(SelectedHeight.Pixels - value.Pixels) > 0.1)
                {
                    selectedHeight = value;
                    OnPropertyChanged(nameof(SelectedHeight));

                    // change width according to aspect ratio
                    double newWidthPixels = value.Pixels * (double)SelectedAspectRatioValue;

                    if (Math.Abs(newWidthPixels - SelectedWidth.Pixels) > 0.1)
                    {
                        SelectedWidth = new MeasurementValue(MeasurementType.Pixels, newWidthPixels, InchesPerPixel);
                    }
                }
                else
                {
                    selectedHeight = value;
                    OnPropertyChanged(nameof(SelectedHeight));
                }
            }
            catch (Exception exc)
            {
                Messenger.Send(new ShowInAppNotificationMessage(new()
                {
                    Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageHeading),
                    Message = $"{GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageBody)}\n{exc.Message}",
                    Severity = InfoBarSeverity.Warning,
                }));
                LogService?.Log.Error(exc, $"Setting SelectedHeight to {value} failed");
                SentryService?.TrackError(exc);
                Close(false);
            }
        }
    }

    private AspectRatio selectedAspectRatio;
    public AspectRatio SelectedAspectRatio
    {
        get => selectedAspectRatio;
        set
        {
            try
            {
                SetProperty(ref selectedAspectRatio, value);
                SelectedAspectRatioValue = value.ToValue();

                if (value == AspectRatio.Custom)
                {
                    IsFixedAspectRatioSelected = false;
                }
                else
                {
                    IsFixedAspectRatioSelected = true;
                }

                SettingsService.LastUsedCropAspectRatio = value;
            }
            catch (Exception exc)
            {
                Messenger.Send(new ShowInAppNotificationMessage(new()
                {
                    Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageHeading),
                    Message = $"{GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageBody)}\n{exc.Message}",
                    Severity = InfoBarSeverity.Warning,
                }));
                LogService.Log.Error(exc, $"Setting SelectedAspectRatio to {value} failed");
                SentryService.TrackError(exc);
                Close(false);
            }
        }
    }

    private double? selectedAspectRatioValue;
    public double? SelectedAspectRatioValue
    {
        get => selectedAspectRatioValue;
        set
        {
            SetProperty(ref selectedAspectRatioValue, value);

            // refresh max/min width/height
            if (value == null)
            {
                MinWidthForAspectRatio = MinHeightForAspectRatio = MinLength;
                MaxWidthForAspectRatio = MaxWidth;
                MaxHeightForAspectRatio = MaxHeight;
            }
            else
            {
                if (value == 1)
                {
                    // same width as height
                    if (MaxWidth.Pixels > MaxHeight.Pixels) MaxWidthForAspectRatio = MaxHeightForAspectRatio = MaxHeight;
                    else MaxWidthForAspectRatio = MaxHeightForAspectRatio = MaxWidth;

                    MinWidthForAspectRatio = MinHeightForAspectRatio = MinLength;
                }
                else if (value > 1)
                {
                    // longer width than height
                    MaxWidthForAspectRatio = MaxWidth;
                    MaxHeightForAspectRatio = new MeasurementValue(MeasurementType.Pixels, MaxWidth.Pixels / (double)value, InchesPerPixel);

                    MinHeightForAspectRatio = MinLength;
                    MinWidthForAspectRatio = new MeasurementValue(MeasurementType.Pixels, MinLength.Pixels * (double)value, InchesPerPixel);
                }
                else
                {
                    // longer height than width
                    MaxHeightForAspectRatio = MaxHeight;
                    MaxWidthForAspectRatio = new MeasurementValue(MeasurementType.Pixels, MaxHeight.Pixels * (double)value, InchesPerPixel);

                    MinWidthForAspectRatio = MinLength;
                    MinHeightForAspectRatio = new MeasurementValue(MeasurementType.Pixels, MinLength.Pixels * (double)value, InchesPerPixel);
                }
            }
        }
    }

    [ObservableProperty]
    private bool isFixedAspectRatioSelected;

    private StorageFile previewFileBuffer;
    private ScanOptions scanOptions;

    private double previewImagePixelWidth;
    private double previewImagePixelHeight;

    private DispatcherQueue viewDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public PreviewDialogViewModel(ScanOptions scanOptions)
    {
        LogService?.Log.Information("Opening preview dialog");
        this.scanOptions = scanOptions;
        AspectRatioFlipCommand = new RelayCommand<Rect>((x) => FlipSelectedAspectRatio(x));
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    public async void ViewLoaded(DispatcherQueue dispatcherQueue)
    {
        viewDispatcherQueue = dispatcherQueue;
        await PreviewScanAsync();
    }

    private void InitializeRegionSelectionParameters()
    {
        double minWidthInches, minHeightInches, maxWidthInches, maxHeightInches;
        if (scanOptions.Scanner is DebugScanner debugScanner)
        {
            // adapt values to preview file for debug scanners
            InchesPerPixel = 1.0 / 100;
            minWidthInches = 100 * InchesPerPixel;
            minHeightInches = 100 * InchesPerPixel;
            maxWidthInches = previewImagePixelWidth * InchesPerPixel;
            maxHeightInches = previewImagePixelHeight * InchesPerPixel;
        }
        else
        {
            // use actual values
            switch (scanOptions.SourceMode)
            {
                case ScannerSource.None:
                case ScannerSource.Auto:
                default:
                    throw new ArgumentException("Can't get min/max scan area for source auto or none.");
                case ScannerSource.Flatbed:
                    minWidthInches = scanOptions.Scanner.FlatbedMinScanArea.Width;
                    minHeightInches = scanOptions.Scanner.FlatbedMinScanArea.Height;
                    maxWidthInches = scanOptions.Scanner.FlatbedMaxScanArea.Width;
                    maxHeightInches = scanOptions.Scanner.FlatbedMaxScanArea.Height;
                    break;
                case ScannerSource.Feeder:
                    minWidthInches = scanOptions.Scanner.FeederMinScanArea.Width;
                    minHeightInches = scanOptions.Scanner.FeederMinScanArea.Height;
                    maxWidthInches = scanOptions.Scanner.FeederMaxScanArea.Width;
                    maxHeightInches = scanOptions.Scanner.FeederMaxScanArea.Height;
                    break;
            }
        }

        // determine baseline for min
        if (minWidthInches > minHeightInches)
        {
            MinLength = new MeasurementValue(MeasurementType.Inches, minWidthInches, InchesPerPixel);
        }
        else
        {
            MinLength = new MeasurementValue(MeasurementType.Inches, minHeightInches, InchesPerPixel);
        }

        // determine max
        MaxWidth = new MeasurementValue(MeasurementType.Inches, maxWidthInches, InchesPerPixel);
        MaxHeight = new MeasurementValue(MeasurementType.Inches, maxHeightInches, InchesPerPixel);

        SelectedAspectRatio = SettingsService.LastUsedCropAspectRatio;

        // log result
        LogService?.Log.Information("Datermined {@MinLength}, {@MaxWidth} and {@MaxHeight}", MinLength, MaxWidth, MaxHeight);
    }

    private void Close(bool applySelection)
    {
        scanOptions.Scanner.CancelPreview();

        if (applySelection && IsCustomRegionSelected)
        {
            Rect rect = new(SelectedX.Inches, SelectedY.Inches, SelectedWidth.Inches, SelectedHeight.Inches);

            LogService?.Log.Information($"Closing preview dialog and returning region {rect}");
            scanOptions.ScanArea = new PreviewSelectionArea(rect);
        }
        else
        {
            LogService?.Log.Information($"Closing preview dialog without returning region");
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Requests a preview scan for the <see cref="scanner"/>, updates <see cref="PreviewImage"/> and
    ///     <see cref="HasPreviewFailed"/>.
    /// </summary>
    private async Task PreviewScanAsync()
    {
        try
        {
            LogService?.Log.Information("PreviewScanAsync");

            StorageFile? file = await scanOptions.Scanner.GetPreviewScanAsync(scanOptions.SourceMode, AppDataService.PreviewScanFolder, true, viewDispatcherQueue!);
            if (file == null)
            {
                HasPreviewFailed = true;
                IsPreviewRunning = false;
                HasPreviewSucceeded = false;
                return;
            }

            await viewDispatcherQueue!.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
            {
                double scanAreaWidthInches = scanOptions.SourceMode is ScannerSource.Flatbed ? scanOptions.Scanner.FlatbedMaxScanArea.Width : scanOptions.Scanner.FeederMaxScanArea.Width;

                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder bitmapDecoder = await BitmapDecoder.CreateAsync(stream);
                    InchesPerPixel = scanAreaWidthInches / bitmapDecoder.PixelWidth;
                    previewImagePixelWidth = bitmapDecoder.PixelWidth;
                    previewImagePixelHeight = bitmapDecoder.PixelHeight;
                }
            });

            previewFileBuffer = file;
            PreviewImage = new(new(AppDataService.GetUriForAppDataFolder(AppDataService.PreviewScanFolder, file.Name)));
            IsPreviewRunning = false;
            HasPreviewSucceeded = true;
        }
        catch (Exception)
        {
            HasPreviewFailed = true;
            IsPreviewRunning = false;
            HasPreviewSucceeded = false;
            return;
        }
        
        InitializeRegionSelectionParameters();
    }

    private void FlipSelectedAspectRatio(Rect currentRect)
    {
        try
        {
            SelectedAspectRatioValue = currentRect.Height / currentRect.Width;
            if (SelectedAspectRatio == AspectRatio.Custom)
            {
                SelectedAspectRatio = AspectRatio.Custom;
            }
        }
        catch (Exception exc)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new()
            {
                Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageHeading),
                Message = $"{GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageBody)}\n{exc.Message}",
                Severity = InfoBarSeverity.Warning,
            }));
            LogService?.Log.Error(exc, $"Flipping aspect ratio rect {currentRect} to failed");
            SentryService?.TrackError(exc);
            Close(false);
        }
    }
}

public class MeasurementValue
{
    public double Inches
    {
        get;
        private set;
    }

    public double Pixels
    {
        get;
        private set;
    }

    public double Display
    {
        get;
        private set;
    }

    public MeasurementValue(MeasurementType unit, double value, double InchesPerPixel)
    {
        ISettingsService settingsService = Ioc.Default.GetRequiredService<ISettingsService>();

        switch (unit)
        {
            case MeasurementType.Inches:
                Inches = value;

                Pixels = Inches / InchesPerPixel;

                Display = ConvertMeasurement(value, SettingMeasurementUnits.ImperialUS, settingsService.SettingMeasurementUnits);
                break;
            case MeasurementType.Pixels:
                Pixels = value;

                Inches = Pixels * InchesPerPixel;

                Display = ConvertMeasurement(Inches, SettingMeasurementUnits.ImperialUS, settingsService.SettingMeasurementUnits);
                break;
            case MeasurementType.Display:
                Display = value;

                Inches = ConvertMeasurement(value, settingsService.SettingMeasurementUnits, SettingMeasurementUnits.ImperialUS);

                Pixels = Inches / InchesPerPixel;
                break;
            default:
                break;
        }
    }
}

public enum MeasurementType
{
    Inches,
    Pixels,
    Display
}