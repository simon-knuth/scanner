﻿using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using WinUI = Microsoft.UI.Xaml.Controls;
using Scanner.Services.Messenger;
using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using static Scanner.Services.Messenger.MessengerEnums;
using Microsoft.Toolkit.Mvvm.Input;

using static Enums;
using Scanner.Models;
using System.Collections.ObjectModel;
using Scanner.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using static Utilities;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
using Windows.Storage;

namespace Scanner.ViewModels
{
    public class ScanOptionsViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        private readonly IScanOptionsDatabaseService ScanOptionsDatabaseService = Ioc.Default.GetService<IScanOptionsDatabaseService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();

        public AsyncRelayCommand ViewLoadedCommand;

        public RelayCommand HelpRequestScannerDiscoveryCommand;
        public RelayCommand HelpRequestChooseResolutionCommand;
        public RelayCommand HelpRequestChooseFileFormatCommand;

        public AsyncRelayCommand<string> PreviewScanCommand;
        public RelayCommand DismissPreviewScanCommand;

        public event EventHandler PreviewRunning;

        private bool _PreviewFailed;
        public bool PreviewFailed
        {
            get => _PreviewFailed;
            set => SetProperty(ref _PreviewFailed, value);
        }

        private ObservableCollection<DiscoveredScanner> _Scanners;
        public ObservableCollection<DiscoveredScanner> Scanners
        {
            get => _Scanners;
            set => SetProperty(ref _Scanners, value);
        }

        private DiscoveredScanner _SelectedScanner;
        public DiscoveredScanner SelectedScanner
        {
            get => _SelectedScanner;
            set
            {
                SetProperty(ref _SelectedScanner, value);

                if (SettingsService != null && ScanOptionsDatabaseService != null)
                {
                    bool useRemembered = (bool) SettingsService.GetSetting
                        (SettingsEnums.AppSetting.SettingRememberScanOptions);

                    if (useRemembered) ApplyInitialScanOptionsForScanner(SelectedScanner);
                    else ApplyDefaultSourceModeForScanner(SelectedScanner);
                }
                else
                {
                    ApplyDefaultSourceModeForScanner(SelectedScanner);
                }
            }
        }

        private ScannerSource _ScannerSource = Enums.ScannerSource.None;
        public ScannerSource? ScannerSource
        {
            get => _ScannerSource;
            set
            {
                // check intermittent value
                if (value == null) return;
                
                // get previously selected scan options
                ScanOptions previousScanOptions = CreateScanOptions();

                SetProperty(ref _ScannerSource, (Enums.ScannerSource)value);

                // enable applicable resolutions and file formats
                switch (value)
                {
                    case Enums.ScannerSource.Auto:
                        ScannerResolutions = null;
                        FileFormats = SelectedScanner?.AutoFormats;
                        break;
                    case Enums.ScannerSource.Flatbed:
                        ScannerResolutions = SelectedScanner?.FlatbedResolutions;
                        FileFormats = SelectedScanner?.FlatbedFormats;
                        break;
                    case Enums.ScannerSource.Feeder:
                        ScannerResolutions = SelectedScanner?.FeederResolutions;
                        FileFormats = SelectedScanner?.FeederFormats;
                        break;
                    case Enums.ScannerSource.None:
                        ScannerResolutions = null;
                        FileFormats = null;
                        break;
                    default:
                        ScannerResolutions = null;
                        break;
                }

                ApplyDefaultScanOptionsForSourceMode((ScannerSource)value, previousScanOptions);
            }
        }

        private ScannerColorMode _ScannerColorMode = ScannerColorMode.None;
        public ScannerColorMode ScannerColorMode
        {
            get => _ScannerColorMode;
            set => SetProperty(ref _ScannerColorMode, value);
        }

        private ObservableCollection<ScanResolution> _ScannerResolutions;
        public ObservableCollection<ScanResolution> ScannerResolutions
        {
            get => _ScannerResolutions;
            set => SetProperty(ref _ScannerResolutions, value);
        }

        private ScanResolution _SelectedResolution;
        public ScanResolution SelectedResolution
        {
            get => _SelectedResolution;
            set => SetProperty(ref _SelectedResolution, value);
        }

        private bool _FeederMultiplePages = false;
        public bool FeederMultiplePages
        {
            get => _FeederMultiplePages;
            set => SetProperty(ref _FeederMultiplePages, value);
        }

        private bool _FeederDuplex = false;
        public bool FeederDuplex
        {
            get => _FeederDuplex;
            set => SetProperty(ref _FeederDuplex, value);
        }

        private ObservableCollection<ScannerFileFormat> _FileFormats;
        public ObservableCollection<ScannerFileFormat> FileFormats
        {
            get => _FileFormats;
            set => SetProperty(ref _FileFormats, value);
        }

        private ScannerFileFormat _SelectedFileFormat;
        public ScannerFileFormat SelectedFileFormat
        {
            get => _SelectedFileFormat;
            set => SetProperty(ref _SelectedFileFormat, value);
        }

        private BitmapImage _PreviewImage;
        public BitmapImage PreviewImage
        {
            get => _PreviewImage;
            set => SetProperty(ref _PreviewImage, value);
        }

        // Debug stuff
        public AsyncRelayCommand DebugAddScannerCommand;
        public RelayCommand DebugRestartScannerDiscoveryCommand;

        private string _DebugScannerName = "Some scanner";
        public string DebugScannerName
        {
            get => _DebugScannerName;
            set => SetProperty(ref _DebugScannerName, value);
        }

        private bool _DebugScannerAutoEnabled = true;
        public bool DebugScannerAutoEnabled
        {
            get => _DebugScannerAutoEnabled;
            set => SetProperty(ref _DebugScannerAutoEnabled, value);
        }

        private bool _DebugScannerFlatbedEnabled = true;
        public bool DebugScannerFlatbedEnabled
        {
            get => _DebugScannerFlatbedEnabled;
            set => SetProperty(ref _DebugScannerFlatbedEnabled, value);
        }

        private bool _DebugScannerFeederEnabled = true;
        public bool DebugScannerFeederEnabled
        {
            get => _DebugScannerFeederEnabled;
            set => SetProperty(ref _DebugScannerFeederEnabled, value);
        }

        private bool _DebugScannerAutoPreviewEnabled = true;
        public bool DebugScannerAutoPreviewEnabled
        {
            get => _DebugScannerAutoPreviewEnabled;
            set => SetProperty(ref _DebugScannerAutoPreviewEnabled, value);
        }

        private bool _DebugScannerFlatbedPreviewEnabled = true;
        public bool DebugScannerFlatbedPreviewEnabled
        {
            get => _DebugScannerFlatbedPreviewEnabled;
            set => SetProperty(ref _DebugScannerFlatbedPreviewEnabled, value);
        }

        private bool _DebugScannerFeederPreviewEnabled = true;
        public bool DebugScannerFeederPreviewEnabled
        {
            get => _DebugScannerFeederPreviewEnabled;
            set => SetProperty(ref _DebugScannerFeederPreviewEnabled, value);
        }

        private bool _DebugScannerFlatbedColorEnabled = true;
        public bool DebugScannerFlatbedColorEnabled
        {
            get => _DebugScannerFlatbedColorEnabled;
            set => SetProperty(ref _DebugScannerFlatbedColorEnabled, value);
        }

        private bool _DebugScannerFlatbedGrayscaleEnabled = true;
        public bool DebugScannerFlatbedGrayscaleEnabled
        {
            get => _DebugScannerFlatbedGrayscaleEnabled;
            set => SetProperty(ref _DebugScannerFlatbedGrayscaleEnabled, value);
        }

        private bool _DebugScannerFlatbedMonochromeEnabled = false;
        public bool DebugScannerFlatbedMonochromeEnabled
        {
            get => _DebugScannerFlatbedMonochromeEnabled;
            set => SetProperty(ref _DebugScannerFlatbedMonochromeEnabled, value);
        }

        private bool _DebugScannerFeederColorEnabled = true;
        public bool DebugScannerFeederColorEnabled
        {
            get => _DebugScannerFeederColorEnabled;
            set => SetProperty(ref _DebugScannerFeederColorEnabled, value);
        }

        private bool _DebugScannerFeederGrayscaleEnabled = true;
        public bool DebugScannerFeederGrayscaleEnabled
        {
            get => _DebugScannerFeederGrayscaleEnabled;
            set => SetProperty(ref _DebugScannerFeederGrayscaleEnabled, value);
        }

        private bool _DebugScannerFeederMonochromeEnabled = false;
        public bool DebugScannerFeederMonochromeEnabled
        {
            get => _DebugScannerFeederMonochromeEnabled;
            set => SetProperty(ref _DebugScannerFeederMonochromeEnabled, value);
        }

        private bool _DebugScannerFeederDuplexEnabled = false;
        public bool DebugScannerFeederDuplexEnabled
        {
            get => _DebugScannerFeederDuplexEnabled;
            set => SetProperty(ref _DebugScannerFeederDuplexEnabled, value);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsViewModel()
        {
            ViewLoadedCommand = new AsyncRelayCommand(ViewLoaded);
            HelpRequestScannerDiscoveryCommand = new RelayCommand(HelpRequestScannerDiscovery);
            HelpRequestChooseResolutionCommand = new RelayCommand(HelpRequestChooseResolution);
            HelpRequestChooseFileFormatCommand = new RelayCommand(HelpRequestChooseFileFormat);
            PreviewScanCommand = new AsyncRelayCommand<string>(PreviewScanAsync);
            DismissPreviewScanCommand = new RelayCommand(DismissPreviewScanAsync);
            DebugAddScannerCommand = new AsyncRelayCommand(DebugAddScannerAsync);
            DebugRestartScannerDiscoveryCommand = new RelayCommand(DebugRestartScannerDiscovery);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async Task ViewLoaded()
        {
            await ScannerDiscoveryService.RestartSearchAsync();
            Scanners = ScannerDiscoveryService.DiscoveredScanners;
            Scanners.CollectionChanged += Scanners_CollectionChangedAsync;
        }
        
        /// <summary>
        ///     Restarts the <see cref="ScannerDiscoveryService"/>.
        /// </summary>
        private void RestartScannerDiscovery()
        {
            ScannerDiscoveryService.RestartSearchAsync();
        }

        /// <summary>
        ///     Selects the first <see cref="DiscoveredScanner"/> added to <see cref="Scanners"/>,
        ///     if none is selected yet.
        /// </summary>
        private async void Scanners_CollectionChangedAsync(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                && e.NewItems.Count > 0
                && SelectedScanner == null)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => SelectedScanner = Scanners[0]);
            }
        }

        /// <summary>
        ///     Applies the initial scan options values for <paramref name="scanner"/> as defined
        ///     by <see cref="ScanOptionsDatabaseService"/>.
        ///     If no initial values are available, default values are applied by
        ///     <see cref="ApplyDefaultSourceModeForScanner(DiscoveredScanner)"/>.
        ///     If values are found but they are not plausible, the database entry is removed and
        ///     default values are applied by <see cref="ApplyDefaultSourceModeForScanner(DiscoveredScanner)"/>.
        /// </summary>
        private void ApplyInitialScanOptionsForScanner(DiscoveredScanner scanner)
        {
            ScannerSource = Enums.ScannerSource.None;
            if (scanner == null) return;

            ScanOptions scanOptions = ScanOptionsDatabaseService.GetScanOptionsForScanner(scanner);

            if (scanOptions != null)
            {
                // scan options found, check integrity and delete from database if necessary
                throw new NotImplementedException();
            }
            else
            {
                // no scan options found, apply default
                ApplyDefaultSourceModeForScanner(scanner);
            }
        }

        /// <summary>
        ///     Applies default (first available) source mode for a scanner.
        /// </summary>
        private void ApplyDefaultSourceModeForScanner(DiscoveredScanner scanner)
        {
            ScannerSource = Enums.ScannerSource.None;
            if (scanner == null) return;

            if (scanner.IsAutoAllowed)
            {
                ScannerSource = Enums.ScannerSource.Auto;
            }
            else if (scanner.IsFlatbedAllowed)
            {
                ScannerSource = Enums.ScannerSource.Flatbed;
            }
            else if (scanner.IsFeederAllowed)
            {
                ScannerSource = Enums.ScannerSource.Feeder;
            }
            else
            {
                AppCenterService.TrackError(new ApplicationException("No default source mode for given scanner."));
            }
        }

        /// <summary>
        ///     Applies default scan options for a <paramref name="sourceMode"/> while taking 
        ///     <paramref name="previousScanOptions"/> into account for a more comprehensible change.
        /// </summary>
        private void ApplyDefaultScanOptionsForSourceMode(ScannerSource sourceMode, ScanOptions previousScanOptions)
        {
            switch (sourceMode)
            {
                case Enums.ScannerSource.Auto:
                    ScannerColorMode = ScannerColorMode.None;
                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.AutoFormats, previousScanOptions.Format);
                    break;
                case Enums.ScannerSource.Flatbed:
                    ScannerColorMode = GetDefaultColorMode
                        (SelectedScanner.IsFlatbedColorAllowed,
                        SelectedScanner.IsFlatbedGrayscaleAllowed,
                        SelectedScanner.IsFlatbedMonochromeAllowed,
                        previousScanOptions.ColorMode);

                    foreach (ScanResolution resolution in SelectedScanner.FlatbedResolutions)
                    {
                        if (resolution.Annotation == ResolutionAnnotation.Default
                            || resolution.Annotation == ResolutionAnnotation.Documents)
                        {
                            SelectedResolution = resolution;
                        }
                    }

                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.FlatbedFormats, previousScanOptions.Format);
                    break;
                case Enums.ScannerSource.Feeder:
                    ScannerColorMode = GetDefaultColorMode
                        (SelectedScanner.IsFeederColorAllowed,
                        SelectedScanner.IsFeederGrayscaleAllowed,
                        SelectedScanner.IsFeederMonochromeAllowed,
                        previousScanOptions.ColorMode);

                    foreach (ScanResolution resolution in SelectedScanner.FeederResolutions)
                    {
                        if (resolution.Annotation == ResolutionAnnotation.Default
                            || resolution.Annotation == ResolutionAnnotation.Documents)
                        {
                            SelectedResolution = resolution;
                        }
                    }

                    FeederMultiplePages = true;

                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.FeederFormats, previousScanOptions.Format);
                    break;
                case Enums.ScannerSource.None:
                default:
                    AppCenterService.TrackError(new ApplicationException
                        ("Unable to apply default scan options for ScannerSource.None."));
                    break;
            }
        }

        /// <summary>
        ///     Gets the default file format from <paramref name="newList"/> while also taking
        ///     <paramref name="previousFormat"/> into consideration.
        /// </summary>
        private ScannerFileFormat GetDefaultFileFormat(ObservableCollection<ScannerFileFormat> newList,
            ScannerFileFormat previousFormat)
        {
            if (previousFormat == null)
            {
                return newList[0];
            }
            else
            {
                foreach (ScannerFileFormat availableFormat in newList)
                {
                    if (availableFormat.TargetFormat == previousFormat.TargetFormat)
                    {
                        return availableFormat;
                    }
                }
                return newList[0];
            }
        }

        /// <summary>
        ///     Gets the default color mode while also taking <paramref name="previousColorMode"/>
        ///     into consideration.
        /// </summary>
        private ScannerColorMode GetDefaultColorMode(bool colorAllowed, bool grayscaleAllowed,
            bool monochromeAllowed, ScannerColorMode previousColorMode)
        {
            switch (previousColorMode)
            {
                
                case ScannerColorMode.Color:
                    if (colorAllowed) return ScannerColorMode.Color;
                    break;
                case ScannerColorMode.Grayscale:
                    if (grayscaleAllowed) return ScannerColorMode.Grayscale;
                    break;
                case ScannerColorMode.Monochrome:
                    if (monochromeAllowed) return ScannerColorMode.Monochrome;
                    break;
                case ScannerColorMode.None:
                default:
                    break;
            }

            if (colorAllowed) return ScannerColorMode.Color;
            if (grayscaleAllowed) return ScannerColorMode.Grayscale;
            if (monochromeAllowed) return ScannerColorMode.Monochrome;

            throw new ArgumentException("Unable to select default color mode when none is available.");
        }

        /// <summary>
        ///     Compiles <see cref="ScanOptions"/> based on the current selections.
        /// </summary>
        private ScanOptions CreateScanOptions()
        {
            ScanOptions result = new ScanOptions()
            {
                Source = _ScannerSource,
                ColorMode = ScannerColorMode,
                FeederMultiplePages = FeederMultiplePages,
                FeederDuplex = FeederDuplex,
                Format = SelectedFileFormat
            };

            if (SelectedResolution != null) result.Resolution = SelectedResolution.Resolution.DpiX;

            return result;
        }

        /// <summary>
        ///     Instructs the <see cref="ScannerDiscoveryService"/> to add a debug scanner.
        /// </summary>
        private async Task DebugAddScannerAsync()
        {
            DiscoveredScanner debugScanner = new DiscoveredScanner(DebugScannerName)
            {
                IsAutoAllowed = DebugScannerAutoEnabled,
                IsAutoPreviewAllowed = DebugScannerAutoPreviewEnabled,

                IsFlatbedAllowed = DebugScannerFlatbedEnabled,
                IsFlatbedPreviewAllowed = DebugScannerFlatbedPreviewEnabled,
                IsFlatbedColorAllowed = DebugScannerFlatbedColorEnabled,
                IsFlatbedGrayscaleAllowed = DebugScannerFlatbedGrayscaleEnabled,
                IsFlatbedMonochromeAllowed = DebugScannerFlatbedMonochromeEnabled,
                
                IsFeederAllowed = DebugScannerFeederEnabled,
                IsFeederPreviewAllowed = DebugScannerFeederPreviewEnabled,
                IsFeederColorAllowed = DebugScannerFeederColorEnabled,
                IsFeederGrayscaleAllowed = DebugScannerFeederGrayscaleEnabled,
                IsFeederMonochromeAllowed = DebugScannerFeederMonochromeEnabled,
                IsFeederDuplexAllowed = DebugScannerFeederDuplexEnabled
            };

            if (debugScanner.IsAutoAllowed)
            {
                debugScanner.AutoFormats = CreateDebugFileFormatList();
            }

            if (debugScanner.IsFlatbedAllowed)
            {
                debugScanner.FlatbedResolutions = CreateDebugResolutionList();
                debugScanner.FlatbedFormats = CreateDebugFileFormatList();
            }

            if (debugScanner.IsFeederAllowed)
            {
                debugScanner.FeederResolutions = CreateDebugResolutionList();
                debugScanner.FeederFormats = CreateDebugFileFormatList();
            }

            await ScannerDiscoveryService.AddDebugScannerAsync(debugScanner);
        }

        /// <summary>
        ///     Creates a debug scanner's resolution list and fills it with some resolutions.
        /// </summary>
        private ObservableCollection<ScanResolution> CreateDebugResolutionList()
        {
            return new ObservableCollection<ScanResolution>()
            {
                new ScanResolution(150, ResolutionAnnotation.None),
                new ScanResolution(350, ResolutionAnnotation.Documents),
                new ScanResolution(600, ResolutionAnnotation.Photos),
                new ScanResolution(800, ResolutionAnnotation.None)
            };
        }

        /// <summary>
        ///     Creates a debug scanner's file format and fills it with some file formats.
        /// </summary>
        private ObservableCollection<ScannerFileFormat> CreateDebugFileFormatList()
        {
            return new ObservableCollection<ScannerFileFormat>()
            {
                new ScannerFileFormat(Windows.Devices.Scanners.ImageScannerFormat.Jpeg),
                new ScannerFileFormat(Windows.Devices.Scanners.ImageScannerFormat.Png),
                new ScannerFileFormat(Windows.Devices.Scanners.ImageScannerFormat.Pdf),
                new ScannerFileFormat(Windows.Devices.Scanners.ImageScannerFormat.DeviceIndependentBitmap)
            };
        }

        /// <summary>
        ///     Restarts the <see cref="ScannerDiscoveryService"/> when instructed to do so while
        ///     debugging.
        /// </summary>
        private void DebugRestartScannerDiscovery()
        {
            RestartScannerDiscovery();
        }

        /// <summary>
        ///     Asks the shell to display the help on scanner discovery.
        /// </summary>
        private void HelpRequestScannerDiscovery()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ScannerDiscovery));
        }

        /// <summary>
        ///     Asks the shell to display the help on choosing a resolution.
        /// </summary>
        private void HelpRequestChooseResolution()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseResolution));
        }

        /// <summary>
        ///     Asks the shell to display the help on choosing a file format.
        /// </summary>
        private void HelpRequestChooseFileFormat()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseFileFormat));
        }

        /// <summary>
        ///     Requests a preview scan for the <see cref="SelectedScanner"/> and
        ///     <see cref="ScannerSource"/> and updates <see cref="PreviewImage"/> and
        ///     <see cref="PreviewFailed"/>.
        /// </summary>
        private async Task PreviewScanAsync(string parameter)
        {
            if (PreviewScanCommand.IsRunning) return;   // preview already running?

            BitmapImage debugImage = null;
            if (parameter == "File")
            {
                // debug preview with file
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".tif");
                picker.FileTypeFilter.Add(".tiff");
                picker.FileTypeFilter.Add(".bmp");

                StorageFile file = await picker.PickSingleFileAsync();
                debugImage = await GenerateBitmapFromFileAsync(file);
            }
            
            PreviewRunning?.Invoke(this, EventArgs.Empty);
            
            // reset properties
            PreviewImage = null;
            PreviewFailed = false;

            // get source mode
            ImageScannerScanSource source;
            switch (_ScannerSource)
            {
                case Enums.ScannerSource.Auto:
                    source = ImageScannerScanSource.AutoConfigured;
                    break;
                case Enums.ScannerSource.Flatbed:
                    source = ImageScannerScanSource.Flatbed;
                    break;
                case Enums.ScannerSource.Feeder:
                    source = ImageScannerScanSource.Feeder;
                    break;
                case Enums.ScannerSource.None:
                default:
                    return;
            }

            // get preview
            if (debugImage != null)
            {
                await Task.Delay(2000);
                PreviewImage = debugImage;
            }
            else if (parameter == "Fail" || SelectedScanner.Debug)
            {
                await Task.Delay(2000);
                PreviewFailed = true;
                PreviewScanCommand?.Cancel();
            }
            else
            {
                try
                {
                    BitmapImage image = await SelectedScanner.GetPreviewAsync(source);
                    PreviewImage = image;
                }
                catch (Exception)
                {
                    PreviewFailed = true;
                    PreviewScanCommand?.Cancel();
                }
            }
        }

        /// <summary>
        ///     Signals that any preview scans are not needed any longer by getting rid of
        ///     <see cref="PreviewImage"/> and canceling any previews in progress.
        /// </summary>
        private void DismissPreviewScanAsync()
        {
            try { PreviewImage = null; }
            catch { }

            PreviewScanCommand?.Cancel();
        }
    }
}
