using Microsoft.Toolkit.Mvvm.ComponentModel;
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

namespace Scanner.ViewModels
{
    public class ScanOptionsViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AsyncRelayCommand ViewLoadedCommand => new AsyncRelayCommand(ViewLoaded);

        public RelayCommand HelpRequestScannerDiscoveryCommand => new RelayCommand(HelpRequestScannerDiscovery);
        public RelayCommand HelpRequestChooseResolutionCommand => new RelayCommand(HelpRequestChooseResolution);
        public RelayCommand HelpRequestChooseFileFormatCommand => new RelayCommand(HelpRequestChooseFileFormat);

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
            set => SetProperty(ref _SelectedScanner, value);
        }

        private ScannerSource _ScannerSource = ScannerSource.None;
        public ScannerSource ScannerSource
        {
            get => _ScannerSource;
            set
            {
                SetProperty(ref _ScannerSource, value);

                // show applicable resolutions and file formats
                switch (value)
                {
                    case ScannerSource.Auto:
                        ScannerResolutions = null;
                        FileFormats = SelectedScanner?.AutoFormats;
                        break;
                    case ScannerSource.Flatbed:
                        ScannerResolutions = SelectedScanner?.FlatbedResolutions;
                        FileFormats = SelectedScanner?.FlatbedFormats;
                        break;
                    case ScannerSource.Feeder:
                        ScannerResolutions = SelectedScanner?.FeederResolutions;
                        FileFormats = SelectedScanner?.FeederFormats;
                        break;
                    case ScannerSource.None:
                    default:
                        ScannerResolutions = null;
                        break;
                }
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

        public readonly IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();

        // Debug properties
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

        public AsyncRelayCommand DebugAddScannerCommand => new AsyncRelayCommand(DebugAddScanner);
        public RelayCommand DebugRestartScannerDiscoveryCommand => new RelayCommand(DebugRestartScannerDiscovery);

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsViewModel()
        {
            
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async Task ViewLoaded()
        {
            await ScannerDiscoveryService.RestartSearchAsync();
            Scanners = ScannerDiscoveryService.DiscoveredScanners;
            Scanners.CollectionChanged += Scanners_CollectionChanged;
        }
        
        private void RestartScannerDiscovery()
        {
            ScannerDiscoveryService.RestartSearchAsync();
        }

        /// <summary>
        ///     Selects the first <see cref="DiscoveredScanner"/> added to <see cref="Scanners"/>,
        ///     if none is selected yet.
        /// </summary>
        private async void Scanners_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                && e.NewItems.Count > 0
                && SelectedScanner == null)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => SelectedScanner = Scanners[0]);
            }
        }

        private async Task DebugAddScanner()
        {
            DiscoveredScanner debugScanner = new DiscoveredScanner(DebugScannerName)
            {
                IsAutoAllowed = DebugScannerAutoEnabled,
                IsFlatbedAllowed = DebugScannerFlatbedEnabled,
                IsFeederAllowed = DebugScannerFeederEnabled,

                IsAutoPreviewAllowed = DebugScannerAutoPreviewEnabled,

                IsFlatbedPreviewAllowed = DebugScannerFlatbedPreviewEnabled,
                IsFlatbedColorAllowed = DebugScannerFlatbedColorEnabled,
                IsFlatbedGrayscaleAllowed = DebugScannerFlatbedGrayscaleEnabled,
                IsFlatbedMonochromeAllowed = DebugScannerFlatbedMonochromeEnabled,

                IsFeederPreviewAllowed = DebugScannerFeederPreviewEnabled,
                IsFeederColorAllowed = DebugScannerFeederColorEnabled,
                IsFeederGrayscaleAllowed = DebugScannerFeederGrayscaleEnabled,
                IsFeederMonochromeAllowed = DebugScannerFeederMonochromeEnabled,
                IsFeederDuplexAllowed = DebugScannerFeederDuplexEnabled
            };

            await ScannerDiscoveryService.AddDebugScannerAsync(debugScanner);
        }

        private void DebugRestartScannerDiscovery()
        {
            RestartScannerDiscovery();
        }

        private void HelpRequestScannerDiscovery()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ScannerDiscovery));
        }

        private void HelpRequestChooseResolution()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseResolution));
        }

        private void HelpRequestChooseFileFormat()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseFileFormat));
        }
    }
}
