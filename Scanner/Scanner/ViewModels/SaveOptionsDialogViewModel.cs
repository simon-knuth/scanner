using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.FileNaming;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
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
    public partial class SaveOptionsDialogViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
        private ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand PickFolderAsyncCommand;
        public AsyncRelayCommand<DispatcherQueue> ViewLoadingAsyncCommand => new AsyncRelayCommand<DispatcherQueue>(ViewLoadingAsync);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        public SaveOptions? SaveOptions
        {
            get
            {
                if (AreValidOptionsSelected)
                {
                    return new SaveOptions(SelectedFolder!, FileDisplayName + FileExtension);
                }
                else
                {
                    return null;
                }
            }
        }

        private StorageFolder? selectedFolder;
        public StorageFolder? SelectedFolder
        {
            get => selectedFolder;
            set
            {
                SetProperty(ref selectedFolder, value);
                OnPropertyChanged(nameof(AreValidOptionsSelected));
                OnPropertyChanged(nameof(IsFileNameCollision));

                _ = Task.Run(UpdateOccupiedFoldersAsync);
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreValidOptionsSelected))]
        [NotifyPropertyChangedFor(nameof(SelectedFileNamingPattern))]
        [NotifyPropertyChangedFor(nameof(IsFileNameCollision))]
        private string fileDisplayName;

        public bool IsFileNameCollision => occupiedFileNames.Contains(FileDisplayName + FileExtension);

        public SettingFileNamingPattern? SelectedFileNamingPattern
        {
            get
            {
                if (FileDisplayName == DateTimeFileNamingPatternValue)
                {
                    return SettingFileNamingPattern.DateTime;
                }
                else if (FileDisplayName == DateFileNamingPatternValue)
                {
                    return SettingFileNamingPattern.Date;
                }
                else if (FileDisplayName == CustomFileNamingPatternValue)
                {
                    return SettingFileNamingPattern.Custom;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                switch (value)
                {
                    case SettingFileNamingPattern.DateTime:
                        FileDisplayName = DateTimeFileNamingPatternValue;
                        break;
                    case SettingFileNamingPattern.Date:
                        FileDisplayName = DateFileNamingPatternValue;
                        break;
                    case SettingFileNamingPattern.Custom:
                        FileDisplayName = CustomFileNamingPatternValue;
                        break;
                }
            }
        }

        public string DateTimeFileNamingPatternValue;
        public string DateFileNamingPatternValue;
        public string CustomFileNamingPatternValue;

        public string FileExtension;

        public bool IsPdf => ScanOptions.TargetFormat == TargetFormat.PDF;

        public bool AreValidOptionsSelected => SelectedFolder != null && IsValidFileName(FileDisplayName);

        public ScanOptions ScanOptions;

        public ProjectBase? Project;

        public List<StorageFolder> RecentFolders;

        private string[] occupiedFileNames = [];

        private DispatcherQueue? viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SaveOptionsDialogViewModel(ScanOptions scanOptions, ProjectBase? project)
        {
            ScanOptions = scanOptions;
            Project = project;
            FileExtension = TargetFormatToFileExtension(ScanOptions.TargetFormat);

            PickFolderAsyncCommand = new AsyncRelayCommand(() => SelectFolderAsync());

            SettingsService.PropertyChanged += SettingsService_PropertyChanged;
            DateTimeFileNamingPatternValue = FileNamingStatics.DateTimePattern.GenerateResult(ScanOptions, false);
            DateFileNamingPatternValue = FileNamingStatics.DatePattern.GenerateResult(ScanOptions, false);
            CustomFileNamingPatternValue = SettingsService.CustomFileNamingPattern.GenerateResult(ScanOptions, false);
            SelectedFileNamingPattern = SettingsService.SettingFileNamingPattern;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private async Task ViewLoadingAsync(DispatcherQueue dispatcherQueue)
        {
            viewDispatcherQueue = dispatcherQueue;

            if (Project != null && Project is PdfProject pdfProject)
            {
                SelectedFolder = pdfProject.TargetFolder;
                _ = Task.Run(UpdateOccupiedFoldersAsync);
            }
            else
            {
                SelectedFolder = await SaveLocationService.GetFixedSaveLocationAsync();
                _ = Task.Run(UpdateOccupiedFoldersAsync);
            }

            _ = GenerateRecentFoldersListAsync();
        }

        private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISettingsService.CustomFileNamingPattern):
                    bool updateFileName = SelectedFileNamingPattern == SettingFileNamingPattern.Custom;
                    CustomFileNamingPatternValue = SettingsService.CustomFileNamingPattern.GenerateResult(ScanOptions, false);
                    if (updateFileName)
                    {
                        FileDisplayName = CustomFileNamingPatternValue;
                    }
                    break;
            }
        }

        private async Task UpdateOccupiedFoldersAsync()
        {
            if (SelectedFolder == null)
            {
                occupiedFileNames = [];
                return;
            }

            occupiedFileNames = (await SelectedFolder.GetFilesAsync()).Select((x) => x.Name).ToArray();

            viewDispatcherQueue?.RunOnThread(DispatcherQueuePriority.Low, () => OnPropertyChanged(nameof(IsFileNameCollision)));
        }

        private async Task SelectFolderAsync()
        {
            // create picker
            FolderPicker picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, ((App)Application.Current).MainWindow.GetWindowHandle());

            // pick folder
            StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedFolder = folder;
                await UpdateOccupiedFoldersAsync();
            }
        }

        private async Task GenerateRecentFoldersListAsync()
        {
            // get actual recents
            List<StorageFolder> recents = await SaveLocationService.GetRecentFoldersAsync();

            // add fixed location to bottom of list if it is not already included
            StorageFolder? fixedLocation = await SaveLocationService.GetFixedSaveLocationAsync();
            if (fixedLocation != null && !recents.Any((x) => x.Path == fixedLocation.Path))
            {
                recents.Add(fixedLocation);
            }

            RecentFolders = recents;
        }
    }
}
