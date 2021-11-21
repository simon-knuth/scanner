using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Models;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using static Enums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class ScanOptionsViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        private readonly IScanOptionsDatabaseService ScanOptionsDatabaseService = Ioc.Default.GetService<IScanOptionsDatabaseService>();
        private readonly IPersistentScanOptionsDatabaseService PersistentScanOptionsDatabaseService = Ioc.Default.GetService<IPersistentScanOptionsDatabaseService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly IHelperService HelperService = Ioc.Default.GetService<IHelperService>();

        public AsyncRelayCommand ViewLoadedCommand;

        public RelayCommand HelpRequestScannerDiscoveryCommand;
        public RelayCommand HelpRequestChooseResolutionCommand;
        public RelayCommand HelpRequestChooseFileFormatCommand;

        public AsyncRelayCommand<string> PreviewScanCommand;
        public RelayCommand DismissPreviewScanCommand;
        public RelayCommand ResetBrightnessCommand;
        public RelayCommand ResetContrastCommand;

        public AsyncRelayCommand ScanCommand;
        public AsyncRelayCommand ScanFreshCommand;
        public RelayCommand CancelScanCommand;

        public event EventHandler PreviewRunning;
        public event EventHandler ScanStarted;
        public event EventHandler ScanEnded;
        public event EventHandler ScannerSearchTipRequested;

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
                    bool useRemembered = (bool)SettingsService.GetSetting
                        (AppSetting.SettingRememberScanOptions);

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
                // check intermittent or old value
                ScannerSource? old = ScannerSource;
                if (value == null || (ScannerSource)value == ScannerSource) return;

                // get previously selected scan options
                ScanOptions previousScanOptions = CreateScanOptions();

                SetProperty(ref _ScannerSource, (ScannerSource)value);

                // enable applicable auto crop mode, resolutions and file formats
                switch (value)
                {
                    case Enums.ScannerSource.Auto:
                        ScannerResolutions = null;
                        SelectedScannerAutoCropMode = ScannerAutoCropMode.None;
                        FileFormats = SelectedScanner?.AutoFormats;
                        ScannerBrightnessConfig = null;
                        ScannerContrastConfig = null;
                        break;
                    case Enums.ScannerSource.Flatbed:
                        ScannerResolutions = SelectedScanner?.FlatbedResolutions;
                        FileFormats = SelectedScanner?.FlatbedFormats;
                        ScannerBrightnessConfig = SelectedScanner?.FlatbedBrightnessConfig;
                        ScannerContrastConfig = SelectedScanner?.FlatbedContrastConfig;
                        break;
                    case Enums.ScannerSource.Feeder:
                        ScannerResolutions = SelectedScanner?.FeederResolutions;
                        FileFormats = SelectedScanner?.FeederFormats;
                        ScannerBrightnessConfig = SelectedScanner?.FeederBrightnessConfig;
                        ScannerContrastConfig = SelectedScanner?.FeederContrastConfig;
                        break;
                    case Enums.ScannerSource.None:
                        ScannerResolutions = null;
                        FileFormats = null;
                        ScannerBrightnessConfig = null;
                        ScannerContrastConfig = null;
                        break;
                    default:
                        ScannerResolutions = null;
                        SelectedScannerAutoCropMode = ScannerAutoCropMode.None;
                        ScannerBrightnessConfig = null;
                        ScannerContrastConfig = null;
                        break;
                }

                if (old == Enums.ScannerSource.None
                    && (bool)SettingsService.GetSetting(AppSetting.SettingRememberScanOptions))
                {
                    ApplyInitialScanOptionsForSourceMode((ScannerSource)value);
                    ApplyInitialPersistentScanOptionsForSourceMode((ScannerSource)value);
                }
                else
                {
                    ApplyDefaultScanOptionsForSourceMode((ScannerSource)value, previousScanOptions);
                    ApplyInitialPersistentScanOptionsForSourceMode((ScannerSource)value);
                }
            }
        }

        private ScannerColorMode _ScannerColorMode = ScannerColorMode.None;
        public ScannerColorMode ScannerColorMode
        {
            get => _ScannerColorMode;
            set => SetProperty(ref _ScannerColorMode, value);
        }

        private ScannerAutoCropMode _SelectedScannerAutoCropMode = ScannerAutoCropMode.None;
        public ScannerAutoCropMode SelectedScannerAutoCropMode
        {
            get => _SelectedScannerAutoCropMode;
            set => SetProperty(ref _SelectedScannerAutoCropMode, value);
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
            set
            {
                SetProperty(ref _SelectedFileFormat, value);
                if (value != null) RefreshCanAddToScanResult();
            }
        }

        private BrightnessConfig _ScannerBrightnessConfig;
        public BrightnessConfig ScannerBrightnessConfig
        {
            get => _ScannerBrightnessConfig;
            set => SetProperty(ref _ScannerBrightnessConfig, value);
        }

        private int _SelectedBrightness;
        public int SelectedBrightness
        {
            get => _SelectedBrightness;
            set
            {
                SetProperty(ref _SelectedBrightness, value);
                if (ScannerBrightnessConfig != null)
                {
                    IsDefaultBrightnessSelected = ScannerBrightnessConfig.DefaultBrightness == value;
                }
            }
        }

        private bool _IsDefaultBrightnessSelected;
        public bool IsDefaultBrightnessSelected
        {
            get => _IsDefaultBrightnessSelected;
            set => SetProperty(ref _IsDefaultBrightnessSelected, value);
        }

        private ContrastConfig _ScannerContrastConfig;
        public ContrastConfig ScannerContrastConfig
        {
            get => _ScannerContrastConfig;
            set => SetProperty(ref _ScannerContrastConfig, value);
        }

        private int _SelectedContrast;
        public int SelectedContrast
        {
            get => _SelectedContrast;
            set
            {
                SetProperty(ref _SelectedContrast, value);
                if (ScannerContrastConfig != null)
                {
                    IsDefaultContrastSelected = ScannerContrastConfig.DefaultContrast == value;
                }
            }
        }

        private bool _IsDefaultContrastSelected;
        public bool IsDefaultContrastSelected
        {
            get => _IsDefaultContrastSelected;
            set => SetProperty(ref _IsDefaultContrastSelected, value);
        }

        private BitmapImage _PreviewImage;
        public BitmapImage PreviewImage
        {
            get => _PreviewImage;
            set => SetProperty(ref _PreviewImage, value);
        }

        private bool _CanAddToScanResult;
        public bool CanAddToScanResult
        {
            get => _CanAddToScanResult;
            set => SetProperty(ref _CanAddToScanResult, value);
        }

        private bool _NextScanMustBeFresh;
        public bool NextScanMustBeFresh
        {
            get => _NextScanMustBeFresh;
            set => SetProperty(ref _NextScanMustBeFresh, value);
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            set => SetProperty(ref _IsScanResultChanging, value);
        }

        private bool _IsEditorEditing;
        public bool IsEditorEditing
        {
            get => _IsEditorEditing;
            set => SetProperty(ref _IsEditorEditing, value);
        }

        private bool _SettingShowAdvancedScanOptions;
        public bool SettingShowAdvancedScanOptions
        {
            get => _SettingShowAdvancedScanOptions;
            set => SetProperty(ref _SettingShowAdvancedScanOptions, value);
        }

        // Debug stuff
        public AsyncRelayCommand DebugAddScannerCommand;
        public RelayCommand DebugRestartScannerDiscoveryCommand;
        public RelayCommand DebugDeleteScanOptionsFromDatabaseCommand;
        public AsyncRelayCommand DebugScanCommand;
        public RelayCommand DebugShowScannerTipCommand;

        private DiscoveredScanner _DebugScanner;
        public DiscoveredScanner DebugScanner
        {
            get => _DebugScanner;
            set => SetProperty(ref _DebugScanner, value);
        }

        public List<ImageScannerFormat> DebugScanFormats = new List<ImageScannerFormat>
        {
            ImageScannerFormat.Jpeg,
            ImageScannerFormat.Png,
            ImageScannerFormat.Pdf,
            ImageScannerFormat.Tiff,
            ImageScannerFormat.DeviceIndependentBitmap
        };

        public ImageScannerFormat DebugSelectedScanFormat;

        public bool DebugScanStartFresh;


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
            DebugDeleteScanOptionsFromDatabaseCommand = new RelayCommand(DebugDeleteScanOptionsFromDatabase);
            ScanCommand = new AsyncRelayCommand(async () => await ScanAsync(false, false));
            ScanFreshCommand = new AsyncRelayCommand(async () => await ScanAsync(false, false));
            DebugScanCommand = new AsyncRelayCommand(async () => await ScanAsync(DebugScanStartFresh == true, true));
            CancelScanCommand = new RelayCommand(CancelScan);
            DebugShowScannerTipCommand = new RelayCommand(DebugShowScannerTip);
            ResetBrightnessCommand = new RelayCommand(ResetBrightness);
            ResetContrastCommand = new RelayCommand(ResetContrast);
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            ScanResultService.ScanResultChanging += (x, y) => IsScanResultChanging = true;
            ScanResultService.ScanResultChanged += (x, y) => IsScanResultChanging = false;
            ScanService.ScanStarted += (x, y) => ScanStarted?.Invoke(this, EventArgs.Empty);
            ScanService.ScanEnded += (x, y) => ScanEnded?.Invoke(this, EventArgs.Empty);

            Messenger.Register<EditorIsEditingChangedMessage>(this, (r, m) => IsEditorEditing = m.Value);
            Messenger.Register<SetupCompletedMessage>(this, (r, m) => ScannerSearchTipRequested?.Invoke(this, EventArgs.Empty));

            SettingsService.SettingChanged += SettingsService_SettingChanged;
            SettingShowAdvancedScanOptions = (bool)SettingsService.GetSetting(AppSetting.SettingShowAdvancedScanOptions);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async Task ViewLoaded()
        {
            await PersistentScanOptionsDatabaseService.InitializeAsync();
            await ScanOptionsDatabaseService.InitializeAsync();
            await ScannerDiscoveryService.RestartSearchAsync();
            Scanners = ScannerDiscoveryService.DiscoveredScanners;
            Scanners.CollectionChanged += Scanners_CollectionChangedAsync;
            PrepareDebugScanner();
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

            ScanOptions scanOptions = ScanOptionsDatabaseService?.GetScanOptionsForScanner(scanner);

            if (scanOptions != null)
            {
                // scan options found, check integrity of source mode and delete from database if necessary
                switch (scanOptions.Source)
                {
                    case Enums.ScannerSource.Auto:
                        if (!scanner.IsAutoAllowed)
                        {
                            ScanOptionsDatabaseService?.DeleteScanOptionsForScanner(scanner);
                            ApplyDefaultSourceModeForScanner(scanner);
                        }
                        ScannerSource = Enums.ScannerSource.Auto;
                        break;
                    case Enums.ScannerSource.Flatbed:
                        if (!scanner.IsFlatbedAllowed)
                        {
                            ScanOptionsDatabaseService?.DeleteScanOptionsForScanner(scanner);
                            ApplyDefaultSourceModeForScanner(scanner);
                        }
                        ScannerSource = Enums.ScannerSource.Flatbed;
                        break;
                    case Enums.ScannerSource.Feeder:
                        if (!scanner.IsFeederAllowed)
                        {
                            ScanOptionsDatabaseService?.DeleteScanOptionsForScanner(scanner);
                            ApplyDefaultSourceModeForScanner(scanner);
                        }
                        ScannerSource = Enums.ScannerSource.Feeder;
                        break;
                    case Enums.ScannerSource.None:
                    default:
                        ApplyDefaultSourceModeForScanner(scanner);
                        break;
                }
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
        ///     Applies initial scan options based on the database for a <paramref name="sourceMode"/>.
        /// </summary>
        private void ApplyInitialScanOptionsForSourceMode(ScannerSource sourceMode)
        {
            ScanOptions scanOptions = ScanOptionsDatabaseService?.GetScanOptionsForScanner(SelectedScanner);

            if (scanOptions != null)
            {
                try
                {
                    switch (sourceMode)
                    {
                        case Enums.ScannerSource.Auto:
                            // file format
                            if (SelectedScanner.SupportsFileFormat(sourceMode, scanOptions.Format.TargetFormat))
                            {
                                SelectedFileFormat = FileFormats.First((x) => x.TargetFormat == scanOptions.Format.TargetFormat);
                            }
                            else
                            {
                                throw new ArgumentException($"Target file format {scanOptions.Format.TargetFormat} not supported.");
                            }
                            break;

                        case Enums.ScannerSource.Flatbed:
                            // color mode
                            if (SelectedScanner.SupportsColorMode(sourceMode, scanOptions.ColorMode))
                            {
                                ScannerColorMode = scanOptions.ColorMode;
                            }
                            else
                            {
                                throw new ArgumentException($"Color mode {scanOptions.ColorMode} not supported.");
                            }

                            // file format
                            if (SelectedScanner.SupportsFileFormat(sourceMode, scanOptions.Format.TargetFormat))
                            {
                                SelectedFileFormat = FileFormats.First((x) => x.TargetFormat == scanOptions.Format.TargetFormat);
                            }
                            else
                            {
                                throw new ArgumentException($"Target file format {scanOptions.Format.TargetFormat} not supported.");
                            }

                            // resolution
                            if (SelectedScanner.SupportsResolution(sourceMode, scanOptions.Resolution))
                            {
                                SelectedResolution = ScannerResolutions.First((x) => x.Resolution.DpiX == scanOptions.Resolution);
                            }
                            else
                            {
                                throw new ArgumentException($"Resolution {scanOptions.Resolution} not supported.");
                            }

                            // auto crop mode
                            if (SelectedScanner.SupportsAutoCropMode(sourceMode, scanOptions.AutoCropMode))
                            {
                                SelectedScannerAutoCropMode = scanOptions.AutoCropMode;
                            }
                            else
                            {
                                throw new ArgumentException($"Auto crop mode {scanOptions.AutoCropMode} not supported.");
                            }

                            break;

                        case Enums.ScannerSource.Feeder:
                            // color mode
                            if (SelectedScanner.SupportsColorMode(sourceMode, scanOptions.ColorMode))
                            {
                                ScannerColorMode = scanOptions.ColorMode;
                            }
                            else
                            {
                                throw new ArgumentException($"Color mode {scanOptions.ColorMode} not supported.");
                            }


                            // file format
                            if (SelectedScanner.SupportsFileFormat(sourceMode, scanOptions.Format.TargetFormat))
                            {
                                SelectedFileFormat = FileFormats.First((x) => x.TargetFormat == scanOptions.Format.TargetFormat);
                            }
                            else
                            {
                                throw new ArgumentException($"Target file format {scanOptions.Format.TargetFormat} not supported.");
                            }

                            // resolution
                            if (SelectedScanner.SupportsResolution(sourceMode, scanOptions.Resolution))
                            {
                                SelectedResolution = ScannerResolutions.First((x) => x.Resolution.DpiX == scanOptions.Resolution);
                            }
                            else
                            {
                                throw new ArgumentException($"Resolution {scanOptions.Resolution} not supported.");
                            }

                            // auto crop mode
                            if (SelectedScanner.SupportsAutoCropMode(sourceMode, scanOptions.AutoCropMode))
                            {
                                SelectedScannerAutoCropMode = scanOptions.AutoCropMode;
                            }
                            else
                            {
                                throw new ArgumentException($"Auto crop mode {scanOptions.AutoCropMode} not supported.");
                            }

                            // feeder options
                            if (scanOptions.FeederDuplex)
                            {
                                if (SelectedScanner.IsFeederDuplexAllowed)
                                {
                                    FeederDuplex = true;
                                }
                                else
                                {
                                    throw new ArgumentException($"Duplex not supported.");
                                }
                            }
                            FeederMultiplePages = scanOptions.FeederMultiplePages;
                            break;

                        case Enums.ScannerSource.None:
                        default:
                            break;
                    }
                }
                catch (Exception exc)
                {
                    AppCenterService?.TrackError(exc);
                    ScanOptionsDatabaseService?.DeleteScanOptionsForScanner(SelectedScanner);
                    ApplyDefaultScanOptionsForSourceMode(sourceMode, null);
                    ApplyInitialPersistentScanOptionsForSourceMode(sourceMode);
                }
            }
            else
            {
                ApplyDefaultScanOptionsForSourceMode(sourceMode, null);
                ApplyInitialPersistentScanOptionsForSourceMode(sourceMode);
            }
        }

        /// <summary>
        ///     Applies initial persistent scan options based on the database for a <paramref name="sourceMode"/>.
        /// </summary>
        private void ApplyInitialPersistentScanOptionsForSourceMode(ScannerSource sourceMode)
        {
            if (SelectedScanner == null) return;

            PersistentScanOptions persistentScanOptions =
                           PersistentScanOptionsDatabaseService.GetPersistentScanOptionsForScanner(SelectedScanner);

            switch (sourceMode)
            {
                case Enums.ScannerSource.Flatbed:
                    if (SelectedScanner.FlatbedBrightnessConfig != null)
                    {
                        if (persistentScanOptions?.FlatbedBrightness != null)
                        {
                            SelectedBrightness = (int)persistentScanOptions.FlatbedBrightness;
                        }
                        else
                        {
                            SelectedBrightness = SelectedScanner.FlatbedBrightnessConfig.DefaultBrightness;
                        }
                    }

                    if (SelectedScanner.FlatbedContrastConfig != null)
                    {
                        if (persistentScanOptions?.FlatbedContrast != null)
                        {
                            SelectedContrast = (int)persistentScanOptions.FlatbedContrast;
                        }
                        else
                        {
                            SelectedContrast = SelectedScanner.FlatbedContrastConfig.DefaultContrast;
                        }
                    }
                    break;

                case Enums.ScannerSource.Feeder:
                    if (SelectedScanner.FeederBrightnessConfig != null)
                    {
                        if (persistentScanOptions?.FeederBrightness != null)
                        {
                            SelectedBrightness = (int)persistentScanOptions.FeederBrightness;
                        }
                        else
                        {
                            SelectedBrightness = SelectedScanner.FeederBrightnessConfig.DefaultBrightness;
                        }
                    }

                    if (SelectedScanner.FeederContrastConfig != null)
                    {
                        if (persistentScanOptions?.FeederContrast != null)
                        {
                            SelectedContrast = (int)persistentScanOptions.FeederContrast;
                        }
                        else
                        {
                            SelectedContrast = SelectedScanner.FeederContrastConfig.DefaultContrast;
                        }
                    }
                    break;

                case Enums.ScannerSource.Auto:
                case Enums.ScannerSource.None:
                default:
                    break;
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
                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.AutoFormats, previousScanOptions?.Format);
                    break;
                case Enums.ScannerSource.Flatbed:
                    ScannerColorMode = GetDefaultColorMode
                        (SelectedScanner.IsFlatbedColorAllowed,
                        SelectedScanner.IsFlatbedGrayscaleAllowed,
                        SelectedScanner.IsFlatbedMonochromeAllowed,
                        previousScanOptions?.ColorMode);
                    SelectedScannerAutoCropMode = GetDefaultAutoCropMode(
                        SelectedScanner.IsFlatbedAutoCropSingleRegionAllowed,
                        SelectedScanner.IsFlatbedAutoCropMultiRegionAllowed,
                        previousScanOptions?.AutoCropMode);

                    foreach (ScanResolution resolution in SelectedScanner.FlatbedResolutions)
                    {
                        if (resolution.Annotation == ResolutionAnnotation.Default
                            || resolution.Annotation == ResolutionAnnotation.Documents)
                        {
                            SelectedResolution = resolution;
                        }
                    }

                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.FlatbedFormats, previousScanOptions?.Format);
                    break;

                case Enums.ScannerSource.Feeder:
                    ScannerColorMode = GetDefaultColorMode
                        (SelectedScanner.IsFeederColorAllowed,
                        SelectedScanner.IsFeederGrayscaleAllowed,
                        SelectedScanner.IsFeederMonochromeAllowed,
                        previousScanOptions?.ColorMode);
                    SelectedScannerAutoCropMode = GetDefaultAutoCropMode(
                        SelectedScanner.IsFeederAutoCropSingleRegionAllowed,
                        SelectedScanner.IsFeederAutoCropMultiRegionAllowed,
                        previousScanOptions?.AutoCropMode);

                    foreach (ScanResolution resolution in SelectedScanner.FeederResolutions)
                    {
                        if (resolution.Annotation == ResolutionAnnotation.Default
                            || resolution.Annotation == ResolutionAnnotation.Documents)
                        {
                            SelectedResolution = resolution;
                        }
                    }

                    FeederMultiplePages = true;

                    SelectedFileFormat = GetDefaultFileFormat(SelectedScanner.FeederFormats, previousScanOptions?.Format);
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
            bool monochromeAllowed, ScannerColorMode? previousColorMode)
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
                case null:
                default:
                    break;
            }

            if (colorAllowed) return ScannerColorMode.Color;
            if (grayscaleAllowed) return ScannerColorMode.Grayscale;
            if (monochromeAllowed) return ScannerColorMode.Monochrome;

            throw new ArgumentException("Unable to select default color mode when none is available.");
        }

        /// <summary>
        ///     Gets the default auto crop mode.
        /// </summary>
        private ScannerAutoCropMode GetDefaultAutoCropMode(bool autoCropSingleAllowed,
            bool autoCropMultiAllowed, ScannerAutoCropMode? previousAutoCropMode)
        {
            switch (previousAutoCropMode)
            {
                case ScannerAutoCropMode.Disabled:
                    return ScannerAutoCropMode.Disabled;
                case ScannerAutoCropMode.SingleRegion:
                    if (autoCropSingleAllowed) return ScannerAutoCropMode.SingleRegion;
                    break;
                case ScannerAutoCropMode.MultipleRegions:
                    if (autoCropMultiAllowed) return ScannerAutoCropMode.MultipleRegions;
                    break;
                case ScannerAutoCropMode.None:
                case null:
                default:
                    break;
            }

            if (autoCropSingleAllowed) return ScannerAutoCropMode.SingleRegion;
            if (autoCropMultiAllowed) return ScannerAutoCropMode.MultipleRegions;
            return ScannerAutoCropMode.Disabled;
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
                AutoCropMode = SelectedScannerAutoCropMode,
                FeederMultiplePages = FeederMultiplePages,
                FeederDuplex = FeederDuplex,
                Format = SelectedFileFormat
            };

            if (SelectedResolution != null) result.Resolution = SelectedResolution.Resolution.DpiX;

            if (ScannerBrightnessConfig != null) result.Brightness = SelectedBrightness;
            if (ScannerContrastConfig != null) result.Contrast = SelectedContrast;

            return result;
        }

        /// <summary>
        ///     Instructs the <see cref="ScannerDiscoveryService"/> to add a debug scanner.
        /// </summary>
        private async Task DebugAddScannerAsync()
        {
            if (DebugScanner.IsAutoAllowed)
            {
                DebugScanner.AutoFormats = CreateDebugFileFormatList();
            }

            if (DebugScanner.IsFlatbedAllowed)
            {
                DebugScanner.FlatbedResolutions = CreateDebugResolutionList();
                DebugScanner.FlatbedFormats = CreateDebugFileFormatList();
            }

            if (DebugScanner.IsFeederAllowed)
            {
                DebugScanner.FeederResolutions = CreateDebugResolutionList();
                DebugScanner.FeederFormats = CreateDebugFileFormatList();
            }

            await ScannerDiscoveryService.AddDebugScannerAsync(DebugScanner);
            PrepareDebugScanner();
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
                new ScannerFileFormat(Windows.Devices.Scanners.ImageScannerFormat.Tiff),
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
        ///     Deletes all scan options for the currently selected scanner from the database.
        /// </summary>
        private void DebugDeleteScanOptionsFromDatabase()
        {
            ScanOptionsDatabaseService?.DeleteScanOptionsForScanner(SelectedScanner);
            PersistentScanOptionsDatabaseService?.DeletePersistentScanOptionsForScanner(SelectedScanner);
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
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".tif");
                picker.FileTypeFilter.Add(".tiff");
                picker.FileTypeFilter.Add(".bmp");

                StorageFile file = await picker.PickSingleFileAsync();
                debugImage = await HelperService.GenerateBitmapFromFileAsync(file);
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
                    ScanOptions scanOptions = CreateScanOptions();
                    BitmapImage image = await ScanService.GetPreviewAsync(SelectedScanner, scanOptions);
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

        private void PrepareDebugScanner()
        {
            DebugScanner = new DiscoveredScanner("Debug scanner")
            {
                IsAutoAllowed = true,
                IsAutoPreviewAllowed = true,
                IsFlatbedAllowed = true,
                IsFlatbedPreviewAllowed = true,
                IsFlatbedColorAllowed = true,
                IsFlatbedGrayscaleAllowed = true,
                IsFlatbedMonochromeAllowed = false,
                IsFeederAllowed = true,
                IsFeederPreviewAllowed = true,
                IsFeederColorAllowed = true,
                IsFeederGrayscaleAllowed = true,
                IsFeederMonochromeAllowed = false,
                IsFeederDuplexAllowed = false,
                FlatbedBrightnessConfig = new BrightnessConfig
                {
                    MinBrightness = -1000,
                    MaxBrightness = 1000,
                    BrightnessStep = 10,
                    DefaultBrightness = 0,
                },
                FeederBrightnessConfig = new BrightnessConfig
                {
                    MinBrightness = -1000,
                    MaxBrightness = 1000,
                    BrightnessStep = 10,
                    DefaultBrightness = 0,
                },
                FlatbedContrastConfig = new ContrastConfig
                {
                    MinContrast = -1000,
                    MaxContrast = 1000,
                    ContrastStep = 10,
                    DefaultContrast = 0,
                },
                FeederContrastConfig = new ContrastConfig
                {
                    MinContrast = -1000,
                    MaxContrast = 1000,
                    ContrastStep = 10,
                    DefaultContrast = 0,
                },
            };
        }

        private async Task ScanAsync(bool startFresh, bool debug)
        {
            if (ScanCommand.IsRunning) return;      // already running?

            StorageFolder targetFolder;
            bool askForFolder = (!CanAddToScanResult || startFresh)
                && (SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType) == SettingSaveLocationType.AskEveryTime;
            int numberOfScannedPages = 0;

            try
            {
                ScanOptions scanOptions = CreateScanOptions();

                // save scan options
                if ((bool)SettingsService.GetSetting(AppSetting.SettingRememberScanOptions))
                {
                    ScanOptionsDatabaseService?.SaveScanOptionsForScanner(SelectedScanner, scanOptions);
                }

                // save persistent scan options
                if (PersistentScanOptionsDatabaseService != null
                    && (ScannerBrightnessConfig != null || ScannerContrastConfig != null))
                {
                    PersistentScanOptions persistentScanOptions = 
                        PersistentScanOptionsDatabaseService.GetPersistentScanOptionsForScanner(SelectedScanner);

                    if (persistentScanOptions == null) persistentScanOptions = new PersistentScanOptions();

                    if (ScannerBrightnessConfig != null)
                    {
                        if (ScannerSource == Enums.ScannerSource.Flatbed)
                        {
                            persistentScanOptions.FlatbedBrightness = SelectedBrightness;
                        }
                        else if (ScannerSource == Enums.ScannerSource.Feeder)
                        {
                            persistentScanOptions.FeederBrightness = SelectedBrightness;
                        }
                    }

                    if (ScannerContrastConfig != null)
                    {
                        if (ScannerSource == Enums.ScannerSource.Flatbed)
                        {
                            persistentScanOptions.FlatbedContrast = SelectedContrast;
                        }
                        else if (ScannerSource == Enums.ScannerSource.Feeder)
                        {
                            persistentScanOptions.FeederContrast = SelectedContrast;
                        }
                    }

                    PersistentScanOptionsDatabaseService.SavePersistentScanOptionsForScanner(
                        SelectedScanner, persistentScanOptions);
                }

                // clean folders
                if (!CanAddToScanResult || startFresh)
                {
                    await AppDataService.Initialize();
                }

                if (!debug)
                {
                    // real scan ~> get file destination
                    if (askForFolder)
                    {
                        // user has to select location
                        var pickerFolder = new FolderPicker();
                        pickerFolder.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                        targetFolder = await pickerFolder.PickSingleFolderAsync();

                        if (targetFolder == null) return;
                    }
                    else
                    {
                        targetFolder = SettingsService.ScanSaveLocation;
                    }

                    // scan
                    var result = await ScanService?.GetScanAsync(SelectedScanner, scanOptions,
                        AppDataService.FolderReceivedPages);

                    if (!CanAddToScanResult || startFresh)
                    {
                        // create new result
                        if (scanOptions.Format.OriginalFormat != scanOptions.Format.TargetFormat)
                        {
                            await ScanResultService.CreateResultFromFilesAsync(result.ScannedFiles,
                                targetFolder, scanOptions.Format.TargetFormat);
                        }
                        else
                        {
                            await ScanResultService.CreateResultFromFilesAsync(result.ScannedFiles,
                                targetFolder);
                        }
                    }
                    else
                    {
                        // add to existing result
                        if (scanOptions.Format.OriginalFormat != ScanResultService.Result.ScanResultFormat)
                        {
                            await ScanResultService.AddToResultFromFilesAsync(result.ScannedFiles,
                                scanOptions.Format.TargetFormat);
                        }
                        else
                        {
                            await ScanResultService.AddToResultFromFilesAsync(result.ScannedFiles,
                                null);
                        }
                    }

                    numberOfScannedPages = result.ScannedFiles.Count;
                }
                else
                {
                    // debug scan
                    var picker = new FileOpenPicker();
                    picker.ViewMode = PickerViewMode.Thumbnail;
                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.FileTypeFilter.Add(".jpg");
                    picker.FileTypeFilter.Add(".jpeg");
                    picker.FileTypeFilter.Add(".png");
                    picker.FileTypeFilter.Add(".tif");
                    picker.FileTypeFilter.Add(".tiff");
                    picker.FileTypeFilter.Add(".bmp");

                    IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
                    if (files != null)
                    {
                        // get file destination
                        if (askForFolder)
                        {
                            // user has to select location
                            var pickerFolder = new FolderPicker();
                            pickerFolder.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                            targetFolder = await pickerFolder.PickSingleFolderAsync();

                            if (targetFolder == null) throw new ArgumentException("No folder selected");
                        }
                        else
                        {
                            targetFolder = SettingsService.ScanSaveLocation;
                        }

                        List<StorageFile> copiedFiles = new List<StorageFile>();
                        foreach (StorageFile file in files)
                        {
                            copiedFiles.Add(await file.CopyAsync(AppDataService.FolderReceivedPages, file.Name, NameCollisionOption.GenerateUniqueName));
                        }

                        if (!CanAddToScanResult || startFresh)
                        {
                            // create new result
                            if (ConvertFormatStringToImageScannerFormat(copiedFiles[0].FileType)
                                != DebugSelectedScanFormat)
                            {
                                await ScanResultService.CreateResultFromFilesAsync(copiedFiles.AsReadOnly(),
                                    targetFolder, DebugSelectedScanFormat);
                            }
                            else
                            {
                                await ScanResultService.CreateResultFromFilesAsync(copiedFiles.AsReadOnly(),
                                    targetFolder);
                            }
                        }
                        else
                        {
                            // add to existing result
                            if (ConvertFormatStringToImageScannerFormat(copiedFiles[0].FileType)
                                != ScanResultService.Result.ScanResultFormat)
                            {
                                await ScanResultService.AddToResultFromFilesAsync(copiedFiles.AsReadOnly(),
                                    DebugSelectedScanFormat);
                            }
                            else
                            {
                                await ScanResultService.AddToResultFromFilesAsync(copiedFiles.AsReadOnly(),
                                    null);
                            }
                        }

                        numberOfScannedPages = copiedFiles.Count;
                    }
                    else
                    {
                        throw new ArgumentException("No debug file(s) selected");
                    }
                }
                SettingsService.LastSaveLocationPath = targetFolder.Path;
            }
            catch (Exception exc)
            {                
                LogService?.Log.Error(exc, "Unhandled exception occurred during scan.");
                if (exc.GetType() == typeof(TaskCanceledException)) return;

                AppCenterService?.TrackError(exc);
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageScanErrorHeading"),
                    MessageText = LocalizedString("ErrorMessageScanErrorBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                Messenger.Send(new NarratorAnnouncementMessage
                {
                    AnnouncementText = LocalizedString("TextScanFailAccessibility")
                });

                try { CancelScan(true); }
                catch { }
            }

            // narrator announcement
            if (numberOfScannedPages == 1)
            {
                Messenger.Send(new NarratorAnnouncementMessage
                {
                    AnnouncementText = LocalizedString("TextScanCompleteSingleAccessibility")
                });
            }
            else if (numberOfScannedPages > 1)
            {
                Messenger.Send(new NarratorAnnouncementMessage
                {
                    AnnouncementText = String.Format(LocalizedString("TextScanCompleteMultipleAccessibility"), numberOfScannedPages)
                });
            }
        }

        private void CancelScan(bool suppressErrors)
        {
            try
            {
                ScanService?.CancelScan();
            }
            catch (Exception exc)
            {
                if (!suppressErrors)
                {
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageScanCancelHeading"),
                        MessageText = LocalizedString("ErrorMessageScanCancelBody"),
                        Severity = AppWideStatusMessageSeverity.Warning,
                        AdditionalText = exc.Message
                    });
                }
            }
        }

        private void CancelScan()
        {
            CancelScan(false);
        }

        private void RefreshCanAddToScanResult()
        {
            if (ScanResultService.Result == null)
            {
                // no result exists
                CanAddToScanResult = false;
                NextScanMustBeFresh = false;
            }
            else
            {
                if (ScanResultService.Result.ScanResultFormat == SelectedFileFormat.TargetFormat)
                {
                    // same format as existing result
                    CanAddToScanResult = true;
                    NextScanMustBeFresh = false;
                }
                else
                {
                    // different format
                    CanAddToScanResult = false;
                    NextScanMustBeFresh = true;
                }
            }
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            RefreshCanAddToScanResult();
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            RefreshCanAddToScanResult();
        }

        private void DebugShowScannerTip()
        {
            ScannerSearchTipRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingShowAdvancedScanOptions)
            {
                SettingShowAdvancedScanOptions = (bool)SettingsService.GetSetting(AppSetting.SettingShowAdvancedScanOptions);
            }
        }

        private void ResetBrightness()
        {
            if (ScannerBrightnessConfig != null)
            {
                SelectedBrightness = ScannerBrightnessConfig.DefaultBrightness;
            }
        }

        private void ResetContrast()
        {
            if (ScannerContrastConfig != null)
            {
                SelectedContrast = ScannerContrastConfig.DefaultContrast;
            }
        }
    }
}
