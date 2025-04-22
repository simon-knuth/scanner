using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Scanner.Models;
using Scanner.Models.FileNaming;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;

namespace Scanner.ViewModels
{
    public partial class CustomFileNamingViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Events
        public event EventHandler CloseRequested;
        #endregion

        #region Commands
        public RelayCommand AcceptCommand => new RelayCommand(AcceptPattern);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);
        public RelayCommand<string> AddBlockCommand => new RelayCommand<string>((x) => AddBlock(x));
        public RelayCommand<IFileNamingBlock> DeleteBlockCommand => new RelayCommand<IFileNamingBlock>(DeleteBlock);
        public RelayCommand DeleteAllBlocksCommand => new RelayCommand(DeleteAllBlocks);
        public RelayCommand<IFileNamingBlock> MoveBlockForwardCommand => new RelayCommand<IFileNamingBlock>(MoveBlockForward);
        public RelayCommand<IFileNamingBlock> MoveBlockBackwardCommand => new RelayCommand<IFileNamingBlock>(MoveBlockBackward);
        public RelayCommand<IFileNamingBlock> MoveBlockToFrontCommand => new RelayCommand<IFileNamingBlock>(MoveBlockToFront);
        public RelayCommand<IFileNamingBlock> MoveBlockToBackCommand => new RelayCommand<IFileNamingBlock>(MoveBlockToBack);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        private ObservableCollection<IFileNamingBlock> selectedBlocks = new();

        [ObservableProperty]
        private string previewResult;

        [ObservableProperty]
        private FileNamingPattern pattern;

        private IScanningDevice previewScanner;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomFileNamingViewModel()
        {
            // get current pattern
            Pattern = SettingsService.CustomFileNamingPattern;
            SelectedBlocks = new ObservableCollection<IFileNamingBlock>(Pattern.Blocks);
            foreach (IFileNamingBlock block in SelectedBlocks)
            {
                block.PropertyChanged += Block_PropertyChanged;
            }
            SelectedBlocks.CollectionChanged += SelectedBlocks_CollectionChanged;

            // ensure initial pattern is visible
            UpdatePattern();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void AcceptPattern()
        {
            if (Pattern.IsValid)
            {
                SettingsService.CustomFileNamingPattern = Pattern;
                LogService?.Log.Information("CustomFileNamingViewModel - Changes in file naming pattern confirmed");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Cancel()
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Changes in file naming pattern discarded");
        }

        private void AddBlock(string blockName)
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Adding file naming {block}", blockName);

            // construct block
            Type[] parameterTypes = [];
            string[] parameters = [];
            IFileNamingBlock? block = FileNamingStatics.FileNamingBlocksDictionary[blockName].GetConstructor(parameterTypes)?
                .Invoke(parameters) as IFileNamingBlock;

            // add to pattern
            if (block != null)
            {
                block.PropertyChanged += Block_PropertyChanged;
                SelectedBlocks.Add(block);
            }
        }

        private void Block_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            LogService?.Log.Information("CustomFileNamingViewModel - File naming {block} {property} changed", ((IFileNamingBlock)sender).Name, e.PropertyName);
            UpdatePattern();
        }

        private void DeleteBlock(IFileNamingBlock block)
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Removing file naming {block}", block.Name);

            block.PropertyChanged -= Block_PropertyChanged;
            SelectedBlocks.Remove(block);
        }

        private void DeleteAllBlocks()
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Removing all file naming blocks");

            foreach (IFileNamingBlock block in SelectedBlocks)
            {
                block.PropertyChanged -= Block_PropertyChanged;
            }

            for (int i = SelectedBlocks.Count - 1; i >= 0; i--)
            {
                SelectedBlocks.RemoveAt(i);
            }
        }

        private void SelectedBlocks_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePattern();
        }

        private void UpdatePattern()
        {
            Pattern = new FileNamingPattern(SelectedBlocks.ToList());

            // generate new preview
            PreviewResult = Pattern.GenerateResult(FileNamingStatics.GetPreviewScanOptions(previewScanner), true);
        }

        private void MoveBlockForward(IFileNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex > 0)
            {
                SelectedBlocks.Move(oldIndex, oldIndex - 1);
            }
        }

        private void MoveBlockBackward(IFileNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex < SelectedBlocks.Count - 1)
            {
                SelectedBlocks.Move(oldIndex, oldIndex + 1);
            }
        }

        private void MoveBlockToFront(IFileNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex > 0)
            {
                SelectedBlocks.Move(oldIndex, 0);
            }
        }

        private void MoveBlockToBack(IFileNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex < SelectedBlocks.Count - 1)
            {
                SelectedBlocks.Move(oldIndex, SelectedBlocks.Count - 1);
            }
        }
    }
}
