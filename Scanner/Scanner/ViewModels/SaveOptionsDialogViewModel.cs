using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.ItemNaming;
using Scanner.Models.Interfaces;
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
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
        public readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetRequiredService<ICopilotRuntimeService>();
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
                    return new SaveOptions(SelectedFolder!, CreateSubFolder ? SubFolderName : null, FileDisplayName + FileExtension, GenerateAIFileName);
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

        public bool IsFileNameCollision => occupiedFileNames.Contains(FileDisplayName.ToLower() + FileExtension);

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

        [ObservableProperty]
        private bool createSubFolder;
        partial void OnCreateSubFolderChanged(bool value) => _ = Task.Run(UpdateOccupiedFoldersAsync);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreValidOptionsSelected))]
        [NotifyPropertyChangedFor(nameof(SelectedSubFolderNamingPattern))]
        private string subFolderName;
        partial void OnSubFolderNameChanged(string value) => _ = Task.Run(UpdateOccupiedFoldersAsync);

        public SettingSubFolderNamingPattern? SelectedSubFolderNamingPattern
        {
            get
            {
                if (SubFolderName == DateSubFolderNamingPatternValue)
                {
                    return SettingSubFolderNamingPattern.Date;
                }
                else if (SubFolderName == FileTypeSubFolderNamingPatternValue)
                {
                    return SettingSubFolderNamingPattern.FileType;
                }
                else if (SubFolderName == CustomSubFolderNamingPatternValue)
                {
                    return SettingSubFolderNamingPattern.Custom;
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
                    case SettingSubFolderNamingPattern.Date:
                        SubFolderName = DateSubFolderNamingPatternValue;
                        break;
                    case SettingSubFolderNamingPattern.FileType:
                        SubFolderName = FileTypeSubFolderNamingPatternValue;
                        break;
                    case SettingSubFolderNamingPattern.Custom:
                        SubFolderName = CustomSubFolderNamingPatternValue;
                        break;
                    default:
                        SubFolderName = "";
                        break;
                }
            }
        }

        [ObservableProperty]
        private bool generateAIFileName;

        public bool CanGenerateAIFileName => CopilotRuntimeService.IsSupported && ScanOptions.TargetFormat == TargetFormat.PDF && Project == null;

        public string DateTimeFileNamingPatternValue;
        public string DateFileNamingPatternValue;
        public string CustomFileNamingPatternValue;

        public string DateSubFolderNamingPatternValue;
        public string FileTypeSubFolderNamingPatternValue;
        public string CustomSubFolderNamingPatternValue;

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
        public SaveOptionsDialogViewModel(ScanOptions scanOptions, ProjectBase? project, string? desiredFileDisplayName)
        {
            ScanOptions = scanOptions;
            Project = project;
            FileExtension = TargetFormatToFileExtension(ScanOptions.TargetFormat);

            PickFolderAsyncCommand = new AsyncRelayCommand(() => SelectFolderAsync());

            SettingsService.PropertyChanged += SettingsService_PropertyChanged;

            DateTimeFileNamingPatternValue = ItemNamingStatics.FileDateTimePattern.GenerateResult(ScanOptions, false);
            DateFileNamingPatternValue = ItemNamingStatics.FileDatePattern.GenerateResult(ScanOptions, false);
            CustomFileNamingPatternValue = SettingsService.CustomFileNamingPattern.GenerateResult(ScanOptions, false);
            SelectedFileNamingPattern = SettingsService.SettingFileNamingPattern;
            SelectedSubFolderNamingPattern = SettingsService.SettingSubFolderNamingPattern;

            DateSubFolderNamingPatternValue = ItemNamingStatics.FolderDatePattern.GenerateResult(ScanOptions, false);
            FileTypeSubFolderNamingPatternValue = ItemNamingStatics.FolderFileTypePattern.GenerateResult(ScanOptions, false);
            CustomSubFolderNamingPatternValue = SettingsService.CustomSubFolderNamingPattern.GenerateResult(ScanOptions, false);
            SelectedSubFolderNamingPattern = SettingsService.SettingSubFolderNamingPattern;

            // keep name if already present
            if (desiredFileDisplayName != null)
                FileDisplayName = desiredFileDisplayName;
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

            if (Project != null && Project is PdfProject pdfProject && pdfProject.TargetFolder != null)
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
                case nameof(ISettingsService.CustomSubFolderNamingPattern):
                    bool updateSubFolderName = SelectedSubFolderNamingPattern == SettingSubFolderNamingPattern.Custom;
                    CustomSubFolderNamingPatternValue = SettingsService.CustomSubFolderNamingPattern.GenerateResult(ScanOptions, false);
                    if (updateSubFolderName)
                    {
                        SubFolderName = CustomSubFolderNamingPatternValue;
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

            StorageFolder folder = SelectedFolder;

            try
            {
                if (CreateSubFolder)
                    folder = await SelectedFolder.GetFolderAsync(SubFolderName);
            }
            catch (Exception) { }

            occupiedFileNames = [.. (await folder.GetFilesAsync()).Select((x) => x.Name.ToLower())];
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
