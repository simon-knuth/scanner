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
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppDataService AppDataService = Ioc.Default.GetService<IAppDataService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly IHelperService HelperService = Ioc.Default.GetService<IHelperService>();
        private readonly IScanService ScanService = Ioc.Default.GetService<IScanService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();

        public RelayCommand ViewLoadedCommand => new RelayCommand(ViewLoaded);
        public RelayCommand ClosedCommand => new RelayCommand(Closed);
        public RelayCommand<Rect> AspectRatioFlipCommand;

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
                    InchesPerPixel = 0.2;
                }

                PreviewSizeInches = new Rect
                {
                    Width = value.PixelWidth * InchesPerPixel,
                    Height = value.PixelHeight * InchesPerPixel
                };
            }
        }

        private bool _HasPreviewSucceeded;
        public bool HasPreviewSucceeded
        {
            get => _HasPreviewSucceeded;
            set => SetProperty(ref _HasPreviewSucceeded, value);
        }

        private bool _HasPreviewFailed;
        public bool HasPreviewFailed
        {
            get => _HasPreviewFailed;
            set => SetProperty(ref _HasPreviewFailed, value);
        }

        private bool _IsCustomRegionSelected;
        public bool IsCustomRegionSelected
        {
            get => _IsCustomRegionSelected;
            set => SetProperty(ref _IsCustomRegionSelected, value);
        }

        private StorageFile _PreviewFile;
        public StorageFile PreviewFile
        {
            get => _PreviewFile;
            set => SetProperty(ref _PreviewFile, value);
        }

        private double _PreviewWidthInches;
        public double PreviewWidthInches
        {
            get => _PreviewWidthInches;
            set => SetProperty(ref _PreviewWidthInches, value);
        }

        private double _PreviewHeightInches;
        public double PreviewHeightInches
        {
            get => _PreviewHeightInches;
            set => SetProperty(ref _PreviewHeightInches, value);
        }

        private double _SelectedRegionWidthDisplay;
        public double SelectedRegionWidthDisplay
        {
            get => _SelectedRegionWidthDisplay;
            set
            {
                SetProperty(ref _SelectedRegionWidthDisplay, value);

                Rect newRegion = (Rect)SelectedRegion;
                newRegion.Width = ConvertMeasurement(
                    value / InchesPerPixel,
                    (SettingMeasurementUnit)SettingsService.GetSetting(AppSetting.SettingMeasurementUnits),
                    SettingMeasurementUnit.ImperialUS);
                SelectedRegion = newRegion;
            }
        }

        private double _SelectedRegionHeightDisplay;
        public double SelectedRegionHeightDisplay
        {
            get => _SelectedRegionHeightDisplay;
            set
            {
                SetProperty(ref _SelectedRegionHeightDisplay, value);

                Rect newRegion = (Rect)SelectedRegion;
                newRegion.Height = ConvertMeasurement(
                    value / InchesPerPixel,
                    (SettingMeasurementUnit)SettingsService.GetSetting(AppSetting.SettingMeasurementUnits),
                    SettingMeasurementUnit.ImperialUS);
                SelectedRegion = newRegion;
            }
        }

        private Rect? _SelectedRegion;
        public Rect? SelectedRegion
        {
            get => _SelectedRegion;
            set
            {
                if (value == SelectedRegion) return;
                
                SetProperty(ref _SelectedRegion, value);
                if (value != null)
                {
                    SelectedRegionWidthDisplay = ConvertMeasurement(
                        value.Value.Width * InchesPerPixel,
                        SettingMeasurementUnit.ImperialUS,
                        (SettingMeasurementUnit)SettingsService.GetSetting(AppSetting.SettingMeasurementUnits));
                    SelectedRegionHeightDisplay = ConvertMeasurement(
                        value.Value.Height * InchesPerPixel,
                        SettingMeasurementUnit.ImperialUS,
                        (SettingMeasurementUnit)SettingsService.GetSetting(AppSetting.SettingMeasurementUnits));
                }
            }
        }

        private AspectRatioOption _SelectedAspectRatio;
        public AspectRatioOption SelectedAspectRatio
        {
            get => _SelectedAspectRatio;
            set
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
        }

        private double? _SelectedAspectRatioValue;
        public double? SelectedAspectRatioValue
        {
            get => _SelectedAspectRatioValue;
            set => SetProperty(ref _SelectedAspectRatioValue, value);
        }

        private bool _IsFixedAspectRatioSelected;
        public bool IsFixedAspectRatioSelected
        {
            get => _IsFixedAspectRatioSelected;
            set => SetProperty(ref _IsFixedAspectRatioSelected, value);
        }

        private Rect _PreviewSizeInches;
        public Rect PreviewSizeInches
        {
            get => _PreviewSizeInches;
            set
            {
                SetProperty(ref _PreviewSizeInches, value);
                PreviewWidthInches = value.Width;
                PreviewHeightInches = value.Height;
            }
        }

        private double _InchesPerPixel;
        public double InchesPerPixel
        {
            get => _InchesPerPixel;
            set => SetProperty(ref _InchesPerPixel, value);
        }

        private DiscoveredScanner _scanner;
        private ScanOptions _scanOptions;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PreviewDialogViewModel()
        {
            AspectRatioFlipCommand = new RelayCommand<Rect>((x) => FlipSelectedAspectRatio(x));

            Tuple<DiscoveredScanner, ScanOptions> parameters = Messenger.Send(new PreviewParametersRequestMessage());
            _scanner = parameters.Item1;
            _scanOptions = parameters.Item2;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewLoaded()
        {
            await PreviewScanAsync();
        }

        private void Closed()
        {
            ScanService.CancelPreview();

            Size? size = null;
            if (SelectedRegion != null)
            {
                size = new Size
                {
                    Width = SelectedRegion.Value.Width * InchesPerPixel,
                    Height = SelectedRegion.Value.Height * InchesPerPixel
                };
            }
            Messenger.Send(new PreviewSelectedRegionChangedMessage(size));
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
                    await Task.Delay(2000);
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
                    HasPreviewFailed = true;
                    IsPreviewRunning = false;
                    HasPreviewSucceeded = false;
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
            }
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
                PreviewFile = file;
            });
        }

        private void FlipSelectedAspectRatio(Rect currentRect)
        {
            SelectedAspectRatioValue = currentRect.Height / currentRect.Width;
        }
    }
}
