using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Models;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using static Enums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class PreviewDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly IAppDataService AppDataService = Ioc.Default.GetService<IAppDataService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly IHelperService HelperService = Ioc.Default.GetService<IHelperService>();
        private readonly IScanService ScanService = Ioc.Default.GetService<IScanService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        #endregion

        #region Commands
        public RelayCommand ViewLoadedCommand => new RelayCommand(ViewLoaded);
        public RelayCommand ApplyAndCloseCommand => new RelayCommand(() => Close(true));
        public RelayCommand CloseCommand => new RelayCommand(() => Close(false));
        public RelayCommand<Rect> AspectRatioFlipCommand;
        #endregion

        #region Events
        public event EventHandler CloseRequested;
        #endregion

        private bool _IsPreviewRunning = true;
        public bool IsPreviewRunning
        {
            get => _IsPreviewRunning;
            set => SetProperty(ref _IsPreviewRunning, value);
        }

        private BitmapImage _PreviewImage;
        public BitmapImage PreviewImage
        {
            get => _PreviewImage;
            set
            {
                SetProperty(ref _PreviewImage, value);

                if (!_scanner.Debug)
                {
                    switch (_scanOptions.Source)
                    {
                        case ScannerSource.Flatbed:
                            InchesPerPixel = _scanner.Device.FlatbedConfiguration.MaxScanArea.Width / PreviewImage.PixelWidth;
                            break;
                        case ScannerSource.Feeder:
                            InchesPerPixel = _scanner.Device.FeederConfiguration.MaxScanArea.Width / PreviewImage.PixelWidth;
                            break;
                    }
                }
                else
                {
                    InchesPerPixel = 0.005;
                }
            }
        }

        private bool _HasPreviewSucceeded;
        public bool HasPreviewSucceeded
        {
            get => _HasPreviewSucceeded;
            set
            {
                SetProperty(ref _HasPreviewSucceeded, value);

                if (value == true)
                {
                    CanSelectCustomRegion = _scanOptions.Source == ScannerSource.Flatbed;
                }
            }
        }

        private bool _HasPreviewFailed;
        public bool HasPreviewFailed
        {
            get => _HasPreviewFailed;
            set => SetProperty(ref _HasPreviewFailed, value);
        }

        private bool _CanSelectCustomRegion;
        public bool CanSelectCustomRegion
        {
            get => _CanSelectCustomRegion;
            set => SetProperty(ref _CanSelectCustomRegion, value);
        }

        private bool _IsCustomRegionSelected;
        public bool IsCustomRegionSelected
        {
            get => _IsCustomRegionSelected;
            set
            {
                LogService.Log.Information($"PreviewDialogViewModel: Setting IsCustomRegionSelected to {value}");
                
                if (value == true && _previewFileBuffer != null)
                {
                    PreviewFile = _previewFileBuffer;
                    _previewFileBuffer = null;
                }

                SetProperty(ref _IsCustomRegionSelected, value);
            }
        }

        private StorageFile _PreviewFile;
        public StorageFile PreviewFile
        {
            get => _PreviewFile;
            set => SetProperty(ref _PreviewFile, value);
        }

        private double _InchesPerPixel;
        public double InchesPerPixel
        {
            get => _InchesPerPixel;
            set => SetProperty(ref _InchesPerPixel, value);
        }

        private MeasurementValue _MinLength;
        public MeasurementValue MinLength
        {
            get => _MinLength;
            set => SetProperty(ref _MinLength, value);
        }

        private MeasurementValue _MinWidthForAspectRatio;
        public MeasurementValue MinWidthForAspectRatio
        {
            get => _MinWidthForAspectRatio;
            set => SetProperty(ref _MinWidthForAspectRatio, value);
        }

        private MeasurementValue _MinHeightForAspectRatio;
        public MeasurementValue MinHeightForAspectRatio
        {
            get => _MinHeightForAspectRatio;
            set => SetProperty(ref _MinHeightForAspectRatio, value);
        }

        private MeasurementValue _MaxWidth;
        public MeasurementValue MaxWidth
        {
            get => _MaxWidth;
            set => SetProperty(ref _MaxWidth, value);
        }

        private MeasurementValue _MaxWidthForAspectRatio;
        public MeasurementValue MaxWidthForAspectRatio
        {
            get => _MaxWidthForAspectRatio;
            set => SetProperty(ref _MaxWidthForAspectRatio, value);
        }

        private MeasurementValue _MaxHeightForAspectRatio;
        public MeasurementValue MaxHeightForAspectRatio
        {
            get => _MaxHeightForAspectRatio;
            set => SetProperty(ref _MaxHeightForAspectRatio, value);
        }

        private MeasurementValue _MaxHeight;
        public MeasurementValue MaxHeight
        {
            get => _MaxHeight;
            set => SetProperty(ref _MaxHeight, value);
        }

        private MeasurementValue _SelectedX;
        public MeasurementValue SelectedX
        {
            get => _SelectedX;
            set => SetProperty(ref _SelectedX, value);
        }

        private MeasurementValue _SelectedY;
        public MeasurementValue SelectedY
        {
            get => _SelectedY;
            set => SetProperty(ref _SelectedY, value);
        }

        private MeasurementValue _SelectedWidth;
        public MeasurementValue SelectedWidth
        {
            get => _SelectedWidth;
            set
            {
                try
                {
                    if (value != null && SelectedX != null && value.Pixels + SelectedX.Pixels > MaxWidth.Pixels)
                    {
                        // too wide, check whether moving the selection to the left would help
                        if (value.Pixels <= MaxWidth.Pixels)
                        {
                            // move selection to the left to allow new width
                            SelectedX = new MeasurementValue(MeasurementType.Pixels, MaxWidth.Pixels - value.Pixels, InchesPerPixel);
                        }
                    }

                    if (SelectedWidth != null && SelectedHeight != null && SelectedAspectRatio != AspectRatioOption.Custom
                        && Math.Abs(SelectedWidth.Pixels - value.Pixels) > 0.1)
                    {
                        SetProperty(ref _SelectedWidth, value);

                        // change height according to aspect ratio
                        double newHeightPixels = value.Pixels / (double)SelectedAspectRatioValue;

                        if (Math.Abs(newHeightPixels - SelectedHeight.Pixels) > 0.1)
                        {
                            SelectedHeight = new MeasurementValue(MeasurementType.Pixels, newHeightPixels, InchesPerPixel);
                        }
                    }
                    else
                    {
                        SetProperty(ref _SelectedWidth, value);
                    }
                }
                catch (Exception exc)
                {
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageHeader"),
                        MessageText = LocalizedString("ErrorMessageBody"),
                        Severity = AppWideStatusMessageSeverity.Warning,
                        AdditionalText = exc.Message
                    });
                    LogService.Log.Error(exc, $"Setting SelectedWidth to {value} failed");
                    AppCenterService.TrackError(exc);
                    Close(false);
                }
            }
        }

        private MeasurementValue _SelectedHeight;
        public MeasurementValue SelectedHeight
        {
            get => _SelectedHeight;
            set
            {
                try
                {
                    if (value != null && SelectedY != null && value.Pixels + SelectedY.Pixels > MaxHeight.Pixels)
                    {
                        // too wide, check whether moving the selection to the left would help
                        if (value.Pixels <= MaxHeight.Pixels)
                        {
                            // move selection to the left to allow new height
                            SelectedY = new MeasurementValue(MeasurementType.Pixels, MaxHeight.Pixels - value.Pixels, InchesPerPixel);
                        }
                    }

                    if (SelectedHeight != null && SelectedWidth != null && SelectedAspectRatio != AspectRatioOption.Custom
                        && Math.Abs(SelectedHeight.Pixels - value.Pixels) > 0.1)
                    {
                        SetProperty(ref _SelectedHeight, value);

                        // change width according to aspect ratio
                        double newWidthPixels = value.Pixels * (double)SelectedAspectRatioValue;

                        if (Math.Abs(newWidthPixels - SelectedWidth.Pixels) > 0.1)
                        {
                            SelectedWidth = new MeasurementValue(MeasurementType.Pixels, newWidthPixels, InchesPerPixel);
                        }
                    }
                    else
                    {
                        SetProperty(ref _SelectedHeight, value);
                    }
                }
                catch (Exception exc)
                {
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageHeader"),
                        MessageText = LocalizedString("ErrorMessageBody"),
                        Severity = AppWideStatusMessageSeverity.Warning,
                        AdditionalText = exc.Message
                    });
                    LogService.Log.Error(exc, $"Setting SelectedHeight to {value} failed");
                    AppCenterService.TrackError(exc);
                    Close(false);
                }
            }
        }

        private AspectRatioOption _SelectedAspectRatio;
        public AspectRatioOption SelectedAspectRatio
        {
            get => _SelectedAspectRatio;
            set
            {
                try
                {
                    SetProperty(ref _SelectedAspectRatio, value);
                    SelectedAspectRatioValue = ConvertAspectRatioOptionToValue(value);

                    if (value == AspectRatioOption.Custom)
                    {
                        IsFixedAspectRatioSelected = false;
                    }
                    else
                    {
                        IsFixedAspectRatioSelected = true;
                    }

                    SettingsService.SetSetting(AppSetting.LastUsedCropAspectRatio, value);
                }
                catch (Exception exc)
                {
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageHeader"),
                        MessageText = LocalizedString("ErrorMessageBody"),
                        Severity = AppWideStatusMessageSeverity.Warning,
                        AdditionalText = exc.Message
                    });
                    LogService.Log.Error(exc, $"Setting SelectedAspectRatio to {value} failed");
                    AppCenterService.TrackError(exc);
                    Close(false);
                }
            }
        }

        private double? _SelectedAspectRatioValue;
        public double? SelectedAspectRatioValue
        {
            get => _SelectedAspectRatioValue;
            set
            {
                SetProperty(ref _SelectedAspectRatioValue, value);

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

        private bool _IsFixedAspectRatioSelected;
        public bool IsFixedAspectRatioSelected
        {
            get => _IsFixedAspectRatioSelected;
            set => SetProperty(ref _IsFixedAspectRatioSelected, value);
        }

        private StorageFile _previewFileBuffer;
        private DiscoveredScanner _scanner;
        private ScanOptions _scanOptions;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PreviewDialogViewModel()
        {
            LogService.Log.Information("Opening preview dialog");
            AspectRatioFlipCommand = new RelayCommand<Rect>((x) => FlipSelectedAspectRatio(x));

            Tuple<DiscoveredScanner, ScanOptions> parameters = Messenger.Send(new PreviewParametersRequestMessage());
            _scanner = parameters.Item1;

            parameters.Item2.SelectedRegion = null;
            _scanOptions = parameters.Item2;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewLoaded()
        {
            await PreviewScanAsync();
        }

        private void InitializeRegionSelectionParameters()
        {
            double minWidth, minHeight, maxWidth, maxHeight;

            if (_scanner.Debug)
            {
                maxWidth = PreviewImage.PixelWidth * InchesPerPixel;
                maxHeight = PreviewImage.PixelHeight * InchesPerPixel;

                minWidth = maxWidth / 4;
                minHeight = maxHeight / 4;
            }
            else
            {
                switch (_scanOptions.Source)
                {
                    case ScannerSource.None:
                    case ScannerSource.Auto:
                    default:
                        throw new ArgumentException("Can't get min/max scan area for source auto or none.");
                    case ScannerSource.Flatbed:
                        minWidth = _scanner.Device.FlatbedConfiguration.MinScanArea.Width;
                        minHeight = _scanner.Device.FlatbedConfiguration.MinScanArea.Height;
                        maxWidth = _scanner.Device.FlatbedConfiguration.MaxScanArea.Width;
                        maxHeight = _scanner.Device.FlatbedConfiguration.MaxScanArea.Height;
                        break;
                    case ScannerSource.Feeder:
                        minWidth = _scanner.Device.FeederConfiguration.MinScanArea.Width;
                        minHeight = _scanner.Device.FeederConfiguration.MinScanArea.Height;
                        maxWidth = _scanner.Device.FeederConfiguration.MaxScanArea.Width;
                        maxHeight = _scanner.Device.FeederConfiguration.MaxScanArea.Height;
                        break;
                }
            }

            // determine baseline for min
            if (minWidth > minHeight)
            {
                MinLength = new MeasurementValue(MeasurementType.Inches, minWidth, InchesPerPixel);
            }
            else
            {
                MinLength = new MeasurementValue(MeasurementType.Inches, minHeight, InchesPerPixel);
            }

            // determine max
            MaxWidth = new MeasurementValue(MeasurementType.Inches, maxWidth, InchesPerPixel);
            MaxHeight = new MeasurementValue(MeasurementType.Inches, maxHeight, InchesPerPixel);

            SelectedAspectRatio = (AspectRatioOption)SettingsService.GetSetting(AppSetting.LastUsedCropAspectRatio);

            // log result
            LogService?.Log.Information("Datermined {@MinLength}, {@MaxWidth} and {@MaxHeight}", MinLength, MaxWidth, MaxHeight);
        }

        private void Close(bool applySelection)
        {
            ScanService.CancelPreview();

            if (applySelection && CanSelectCustomRegion && IsCustomRegionSelected)
            {
                Rect? rect = new Rect
                {
                    X = SelectedX.Inches,
                    Y = SelectedY.Inches,
                    Width = SelectedWidth.Inches,
                    Height = SelectedHeight.Inches
                };

                LogService.Log.Information($"Closing preview dialog and returning region {rect}");
                Messenger.Send(new PreviewSelectedRegionChangedMessage(rect));
            }
            else
            {
                LogService.Log.Information($"Closing preview dialog without returning region");
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Requests a preview scan for the <see cref="_scanner"/>, updates <see cref="PreviewImage"/> and
        ///     <see cref="HasPreviewFailed"/>.
        /// </summary>
        private async Task PreviewScanAsync()
        {
            try
            {
                LogService?.Log.Information("PreviewScanAsync");
                BitmapImage debugImage = null;
                StorageFile file = null;
                if (_scanner.Debug)
                {
                    // debug preview with file
                    var picker = new FileOpenPicker();
                    picker.ViewMode = PickerViewMode.Thumbnail;
                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.FileTypeFilter.Add(".jpg");
                    picker.FileTypeFilter.Add(".png");
                    picker.FileTypeFilter.Add(".tif");
                    picker.FileTypeFilter.Add(".tiff");
                    picker.FileTypeFilter.Add(".bmp");

                    file = await picker.PickSingleFileAsync();
                    if (file != null)
                    {
                        debugImage = await HelperService.GenerateBitmapFromFileAsync(file);
                    }
                }

                // get preview
                if (debugImage != null)
                {
                    // show debug image as preview
                    await Task.Delay(1000);
                    PreviewImage = debugImage;
                    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                    await CreatePreviewFile(stream);
                    IsPreviewRunning = false;
                    HasPreviewSucceeded = true;
                }
                else if (_scanner.Debug && debugImage == null)
                {
                    // fail debug preview
                    await Task.Delay(2000);
                    throw new ApplicationException("Debug preview failed on purpose");
                }
                else
                {
                    // real preview
                    var previewResult = await ScanService.GetPreviewWithStreamAsync(_scanner, _scanOptions);
                    PreviewImage = previewResult.Item1;
                    await CreatePreviewFile(previewResult.Item2);
                    IsPreviewRunning = false;
                    HasPreviewSucceeded = true;
                }
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

        /// <summary>
        ///     Saves the preview image from <paramref name="stream"/> to a new temporary file and assigns that
        ///     to <see cref="PreviewFile"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private async Task CreatePreviewFile(IRandomAccessStream stream)
        {
            await RunOnUIThreadAndWaitAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                StorageFile file = await AppDataService.FolderPreview.CreateFileAsync("preview.jpg", CreationCollisionOption.ReplaceExisting);
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await HelperService.CreateOptimizedBitmapEncoderAsync(ImageScannerFormat.Jpeg, fileStream);
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                }
                _previewFileBuffer = file;
            });
        }

        private void FlipSelectedAspectRatio(Rect currentRect)
        {
            try
            {
                SelectedAspectRatioValue = currentRect.Height / currentRect.Width;
                if (SelectedAspectRatio == AspectRatioOption.Custom)
                {
                    SelectedAspectRatio = AspectRatioOption.Custom;
                }
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageHeader"),
                    MessageText = LocalizedString("ErrorMessageBody"),
                    Severity = AppWideStatusMessageSeverity.Warning,
                    AdditionalText = exc.Message
                });
                LogService.Log.Error(exc, $"Flipping aspect ratio rect {currentRect} to failed");
                AppCenterService.TrackError(exc);
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
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();

            switch (unit)
            {
                case MeasurementType.Inches:
                    Inches = value;

                    Pixels = Inches / InchesPerPixel;

                    Display = ConvertMeasurement(
                        value,
                        SettingMeasurementUnit.ImperialUS,
                        (SettingMeasurementUnit)settingsService.GetSetting(AppSetting.SettingMeasurementUnits));
                    break;
                case MeasurementType.Pixels:
                    Pixels = value;

                    Inches = Pixels * InchesPerPixel;

                    Display = ConvertMeasurement(
                        Inches,
                        SettingMeasurementUnit.ImperialUS,
                        (SettingMeasurementUnit)settingsService.GetSetting(AppSetting.SettingMeasurementUnits));
                    break;
                case MeasurementType.Display:
                    Display = value;

                    Inches = ConvertMeasurement(
                        value,
                        (SettingMeasurementUnit)settingsService.GetSetting(AppSetting.SettingMeasurementUnits),
                        SettingMeasurementUnit.ImperialUS);

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
}
