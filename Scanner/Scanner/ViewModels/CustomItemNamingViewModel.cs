using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Scanner.Models;
using Scanner.Models.ItemNaming;
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
    public partial class CustomItemNamingViewModel : ObservableRecipient, IDisposable
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
        public RelayCommand<IItemNamingBlock> DeleteBlockCommand => new RelayCommand<IItemNamingBlock>(DeleteBlock);
        public RelayCommand DeleteAllBlocksCommand => new RelayCommand(DeleteAllBlocks);
        public RelayCommand<IItemNamingBlock> MoveBlockForwardCommand => new RelayCommand<IItemNamingBlock>(MoveBlockForward);
        public RelayCommand<IItemNamingBlock> MoveBlockBackwardCommand => new RelayCommand<IItemNamingBlock>(MoveBlockBackward);
        public RelayCommand<IItemNamingBlock> MoveBlockToFrontCommand => new RelayCommand<IItemNamingBlock>(MoveBlockToFront);
        public RelayCommand<IItemNamingBlock> MoveBlockToBackCommand => new RelayCommand<IItemNamingBlock>(MoveBlockToBack);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private ItemNamingKind? kind;
        public ItemNamingKind? Kind
        {
            get => kind;
            set
            {
                SetProperty(ref kind, value);
                Pattern = Kind == ItemNamingKind.File ? SettingsService.CustomFileNamingPattern : SettingsService.CustomSubFolderNamingPattern;
                UpdatePattern();
            }
        }

        [ObservableProperty]
        private ObservableCollection<IItemNamingBlock> selectedBlocks = [];

        [ObservableProperty]
        private string previewResult;

        private ItemNamingPattern pattern;
        public ItemNamingPattern Pattern
        {
            get => pattern;
            set
            {
                SetProperty(ref pattern, value);

                SelectedBlocks.CollectionChanged -= SelectedBlocks_CollectionChanged;
                SelectedBlocks = new ObservableCollection<IItemNamingBlock>(Pattern.Blocks);
                foreach (IItemNamingBlock block in SelectedBlocks)
                {
                    block.PropertyChanged += Block_PropertyChanged;
                }
                SelectedBlocks.CollectionChanged += SelectedBlocks_CollectionChanged;
            }
        }

        private IScanningDevice previewScanner;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomItemNamingViewModel()
        {
            // get current pattern
            Pattern = Kind == ItemNamingKind.File ? SettingsService.CustomFileNamingPattern : SettingsService.CustomSubFolderNamingPattern;

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
                if (Kind == ItemNamingKind.File)
                    SettingsService.CustomFileNamingPattern = Pattern;
                else
                    SettingsService.CustomSubFolderNamingPattern = Pattern;

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
            IItemNamingBlock? block = ItemNamingStatics.ItemNamingBlocksDictionary[blockName].GetConstructor(parameterTypes)?
                .Invoke(parameters) as IItemNamingBlock;

            // add to pattern
            if (block != null)
            {
                block.PropertyChanged += Block_PropertyChanged;
                SelectedBlocks.Add(block);
            }
        }

        private void Block_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            LogService?.Log.Information("CustomFileNamingViewModel - File naming {block} {property} changed", ((IItemNamingBlock)sender).Name, e.PropertyName);
            UpdatePattern();
        }

        private void DeleteBlock(IItemNamingBlock block)
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Removing file naming {block}", block.Name);

            block.PropertyChanged -= Block_PropertyChanged;
            SelectedBlocks.Remove(block);
        }

        private void DeleteAllBlocks()
        {
            LogService?.Log.Information("CustomFileNamingViewModel - Removing all file naming blocks");

            foreach (IItemNamingBlock block in SelectedBlocks)
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
            Pattern = new ItemNamingPattern([.. SelectedBlocks]);

            // generate new preview
            PreviewResult = Pattern.GenerateResult(ItemNamingStatics.GetPreviewScanOptions(previewScanner), Kind == ItemNamingKind.File);
        }

        private void MoveBlockForward(IItemNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex > 0)
            {
                SelectedBlocks.Move(oldIndex, oldIndex - 1);
            }
        }

        private void MoveBlockBackward(IItemNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex < SelectedBlocks.Count - 1)
            {
                SelectedBlocks.Move(oldIndex, oldIndex + 1);
            }
        }

        private void MoveBlockToFront(IItemNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex > 0)
            {
                SelectedBlocks.Move(oldIndex, 0);
            }
        }

        private void MoveBlockToBack(IItemNamingBlock block)
        {
            int oldIndex = SelectedBlocks.IndexOf(block);
            if (oldIndex < SelectedBlocks.Count - 1)
            {
                SelectedBlocks.Move(oldIndex, SelectedBlocks.Count - 1);
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public enum ItemNamingKind
        {
            File = 0,
            Folder = 1
        }
    }
}
