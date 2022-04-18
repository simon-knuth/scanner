using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Models;
using Scanner.Models.FileNaming;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.System;
using static Utilities;

namespace Scanner.ViewModels
{
    public class CustomFileNamingDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Commands
        public RelayCommand AcceptCommand => new RelayCommand(AcceptPattern);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);
        public RelayCommand<string> AddBlockCommand => new RelayCommand<string>((x) => AddBlock(x));
        public RelayCommand<IFileNamingBlock> DeleteBlockCommand => new RelayCommand<IFileNamingBlock>((x) => DeleteBlock(x));
        public AsyncRelayCommand LaunchScannerSettingsCommand;
        #endregion

        #region Events
        public event EventHandler CloseRequested;
        #endregion

        private ObservableCollection<IFileNamingBlock> _SelectedBlocks = new ObservableCollection<IFileNamingBlock>();
        public ObservableCollection<IFileNamingBlock> SelectedBlocks
        {
            get => _SelectedBlocks;
            set => SetProperty(ref _SelectedBlocks, value);
        }

        private string _PreviewResult;
        public string PreviewResult
        {
            get => _PreviewResult;
            set => SetProperty(ref _PreviewResult, value);
        }

        private FileNamingPattern _Pattern;
        private ScanOptions _PreviewScanOptions;
        private DiscoveredScanner _PreviewScanner;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomFileNamingDialogViewModel()
        {
            LaunchScannerSettingsCommand = new AsyncRelayCommand(LaunchScannerSettings);

            SelectedBlocks.CollectionChanged += SelectedBlocks_CollectionChanged;

            // create preview ScanOptions
            _PreviewScanOptions = new ScanOptions
            {
                Brightness = -20,
                Contrast = 5,
                Format = new ScannerFileFormat(ImageScannerFormat.Pdf, ImageScannerFormat.Jpeg),
                Resolution = 300,
                Source = Enums.ScannerSource.Flatbed
            };

            // create preview DiscoveredScanner
            string currentScannerName = Messenger.Send(new SelectedScannerRequestMessage()).Response?.Name;
            if (String.IsNullOrEmpty(currentScannerName))
            {
                _PreviewScanner = new DiscoveredScanner("IntelliQ TX3000-S");
            }
            else
            {
                _PreviewScanner = new DiscoveredScanner(currentScannerName);
            }
            _PreviewScanner.FlatbedBrightnessConfig = new BrightnessConfig
            {
                DefaultBrightness = 0
            };
            _PreviewScanner.FlatbedContrastConfig = new ContrastConfig
            {
                DefaultContrast = 0
            };

            // ensure intital pattern is visible
            UpdatePattern();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void AcceptPattern()
        {

        }

        private void Cancel()
        {

        }

        private void AddBlock(string blockName)
        {
            // construct block
            Type[] parameterTypes = new Type[0];
            string[] parameters = new string[0];
            IFileNamingBlock block = FileNamingStatics.FileNamingBlocksDictionary[blockName].GetConstructor(parameterTypes)
                .Invoke(parameters) as IFileNamingBlock;

            // add to pattern
            block.PropertyChanged += Block_PropertyChanged;
            SelectedBlocks.Add(block);
        }

        private void Block_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdatePattern();
        }

        private void DeleteBlock(IFileNamingBlock block)
        {            
            block.PropertyChanged -= Block_PropertyChanged;
            SelectedBlocks.Remove(block);
        }

        private void SelectedBlocks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePattern();
        }

        private void UpdatePattern()
        {
            _Pattern = new FileNamingPattern();

            foreach (IFileNamingBlock block in SelectedBlocks)
            {
                _Pattern.Blocks.Add(block);
            }

            // generate new preview
            PreviewResult = _Pattern.GenerateResult(_PreviewScanOptions, _PreviewScanner);
        }

        private async Task LaunchScannerSettings()
        {
            LogService?.Log.Information("LaunchScannerSettings");
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
            }
            catch (Exception) { }
        }
    }
}
