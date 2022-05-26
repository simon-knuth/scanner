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
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        #endregion

        #region Commands
        public RelayCommand AcceptCommand => new RelayCommand(AcceptPattern);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);
        public RelayCommand<string> AddBlockCommand => new RelayCommand<string>((x) => AddBlock(x));
        public RelayCommand<IFileNamingBlock> DeleteBlockCommand => new RelayCommand<IFileNamingBlock>((x) => DeleteBlock(x));
        public RelayCommand<IFileNamingBlock> DeleteAllBlocksCommand => new RelayCommand<IFileNamingBlock>((x) => DeleteAllBlocks());
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
        public FileNamingPattern Pattern
        {
            get => _Pattern;
            set => SetProperty(ref _Pattern, value);
        }

        private DiscoveredScanner _PreviewScanner;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomFileNamingDialogViewModel()
        {
            LogService.Log.Information("Opening custom file naming dialog");

            // get current pattern
            Pattern = new FileNamingPattern((string)SettingsService.GetSetting(AppSetting.CustomFileNamingPattern));
            SelectedBlocks = new ObservableCollection<IFileNamingBlock>(Pattern.Blocks);
            foreach (IFileNamingBlock block in SelectedBlocks)
            {
                block.PropertyChanged += Block_PropertyChanged;
            }
            SelectedBlocks.CollectionChanged += SelectedBlocks_CollectionChanged;

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

            // ensure initial pattern is visible
            UpdatePattern();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void AcceptPattern()
        {
            if (Pattern.IsValid)
            {
                SettingsService.SetSetting(AppSetting.CustomFileNamingPattern, Pattern.GetSerialized(false));
                LogService.Log.Information("Changes in file naming {pattern} confirmed", Pattern.GetSerialized(false));
            }
        }

        private void Cancel()
        {
            LogService.Log.Information("Changes in file naming pattern discarded");
        }

        private void AddBlock(string blockName)
        {
            LogService.Log.Information("Adding file naming {block}", blockName);

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
            LogService.Log.Information("File naming {block} {property} changed", ((IFileNamingBlock)sender).Name, e.PropertyName);
            UpdatePattern();
        }

        private void DeleteBlock(IFileNamingBlock block)
        {
            LogService.Log.Information("Removing file naming {block}", block.Name);

            block.PropertyChanged -= Block_PropertyChanged;
            SelectedBlocks.Remove(block);
        }

        private void DeleteAllBlocks()
        {
            LogService.Log.Information("Removing all file naming blocks");

            foreach (IFileNamingBlock block in SelectedBlocks)
            {
                block.PropertyChanged -= Block_PropertyChanged;
            }

            for (int i = SelectedBlocks.Count - 1; i >= 0; i--)
            {
                SelectedBlocks.RemoveAt(i);
            }
        }

        private void SelectedBlocks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePattern();
        }

        private void UpdatePattern()
        {
            Pattern = new FileNamingPattern(SelectedBlocks.ToList());

            // generate new preview
            PreviewResult = Pattern.GenerateResult(FileNamingStatics.PreviewScanOptions, _PreviewScanner);
        }
    }
}
