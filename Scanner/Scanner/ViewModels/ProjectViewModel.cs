using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.Helpers;

namespace Scanner.ViewModels
{
    partial class ProjectViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand TryRemoveCurrentPageAsyncCommand => new AsyncRelayCommand(TryRemoveCurrentPageAsync);
        public AsyncRelayCommand TryDeleteProjectAsyncCommand => new AsyncRelayCommand(TryDeleteProjectAsync);
        public AsyncRelayCommand TryCopySelectionAsyncCommand => new AsyncRelayCommand(TryCopySelectionAsync);
        public AsyncRelayCommand<AppInfo?> TryOpenWithAsyncCommand => new AsyncRelayCommand<AppInfo?>(TryOpenWithAsync);
        public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
        public RelayCommand SelectPreviousPageCommand => new RelayCommand(SelectPreviousPage);
        public RelayCommand SelectNextPageCommand => new RelayCommand(SelectNextPage);
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public AsyncRelayCommand<IProjectPage?> ShowInFileExplorerAsyncCommand => new AsyncRelayCommand<IProjectPage?>(ShowInFileExplorerAsync);
        public AsyncRelayCommand TrySaveAsyncCommand => new AsyncRelayCommand(TrySaveAsync);
        public AsyncRelayCommand AddFilesCommand => new AsyncRelayCommand(AddFilesAsync);
        public AsyncRelayCommand FindAppForFileTypeCommand => new AsyncRelayCommand(FindAppForFileTypeAsync);
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private ProjectBase? currentProject;
        public ProjectBase? CurrentProject
        {
            get => currentProject;
            set
            {
                if (currentProject != null && currentProject is PdfProject pdfProject) pdfProject.FileNameInfo.NameChanged -= FileNameInfo_NameChanged;

                if (SetProperty(ref currentProject, value))
                {
                    OnPropertyChanged(nameof(FileName));

                    if (value != null && value is PdfProject pdfProject2) pdfProject2.FileNameInfo.NameChanged += FileNameInfo_NameChanged;
                }
            }
        }

        public string FileName
        {
            get
            {
                if (CurrentProject == null) return string.Empty;

                if (CurrentProject is PdfProject pdfProject)
                {
                    return Path.GetFileNameWithoutExtension(pdfProject.FileNameInfo.DesiredName);
                }
                else if (ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return Path.GetFileNameWithoutExtension(imagePage.FileNameInfo.DesiredName);
                }
                return string.Empty;
            }
            set
            {
                if (CurrentProject == null) return;

                if (CurrentProject is PdfProject pdfProject)
                {
                    if (pdfProject.FileNameInfo.DesiredName != value)
                    {
                        _ = ProjectService.ApplyActionAsync(new RenameAction(null, value));
                    }
                }
                else if (ProjectService.SelectedPage is ImagePage imagePage)
                {
                    if (imagePage.FileNameInfo!.DesiredName != value)
                    {
                        _ = ProjectService.ApplyActionAsync(new RenameAction(imagePage, value));
                    }
                }
            }
        }

        public List<OpenWithTarget> OpenWithTargets = new();

        private DispatcherQueue? viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectViewModel()
        {
            ProjectService.PropertyChanging += ProjectService_PropertyChanging;
            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void ViewLoading(DispatcherQueue? dispatcherQueue)
        {
            if (dispatcherQueue != null)
            {
                viewDispatcherQueue = dispatcherQueue;
            }
        }

        private void ProjectService_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.SelectedPage):
                    if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage && imagePage.FileNameInfo != null)
                    {
                        imagePage.FileNameInfo.NameChanged -= FileNameInfo_NameChanged;
                    }
                    break;
            }
        }

        private async void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentProject):
                    CurrentProject = ProjectService.CurrentProject;
                    await UpdateOpenWithTargetsAsync();
                    break;
                case nameof(IProjectService.SelectedPage):
                    if (CurrentProject != null && !CurrentProject.IsPdf)
                    {
                        OnPropertyChanged(nameof(FileName));
                    }

                    if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage && imagePage.FileNameInfo != null)
                    {
                        imagePage.FileNameInfo.NameChanged += FileNameInfo_NameChanged;
                    }
                    break;
            }
        }

        private void FileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(FileName));
        }

        private void SelectPreviousPage()
        {
            ProjectService.SelectPreviousPage();
        }

        private void SelectNextPage()
        {
            ProjectService.SelectNextPage();
        }

        private async Task TryRemoveCurrentPageAsync()
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;

            await ProjectService.ApplyActionAsync(new RemovePagesAction(new()
            {
                ProjectService.SelectedPage
            }));
        }

        private async Task TryDeleteProjectAsync()
        {
            await ProjectService.TryDeleteProjectAsync();
        }

        private async Task TryCloseProjectAsync()
        {
            await ProjectService.TryCloseProjectAsync();
        }

        private void ShowSettings()
        {
            Messenger.Send(new ShowSettingsMessage());
        }

        private async Task ShowInFileExplorerAsync(IProjectPage? page)
        {
            if (CurrentProject == null) return;
            if (CurrentProject is PdfProject pdfProject)
            {
                await Windows.System.Launcher.LaunchFolderAsync(pdfProject.TargetFolder);
            }
            else
            {
                // use currently selected page if no page is provided
                if (page == null && ProjectService.SelectedPage != null) page = ProjectService.SelectedPage;

                if (page is not ImagePage imagePage) return;

                await Windows.System.Launcher.LaunchFolderAsync(imagePage.TargetFolder);
            }
        }

        private async Task AddFilesAsync()
        {
            if (CurrentProject == null) return;
            if (!CurrentProject.IsPdf) throw new ApplicationException("Adding files is only supported for PDF projects");

            // select files to add to project
            FileOpenPicker picker = new();

            // connect picker to window
            IntPtr hwnd = WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            // set picker properties
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");

            // pick files
            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;

            // construct insertions
            List<ProjectFileInsertion> insertions = new();
            for (int i = 0; i < files.Count; i++)
            {
                insertions.Add(new ProjectFileInsertion(files[i], CurrentProject.Pages.Count + i, null, null, ImageFilter.None, ImageFilter.None));
            }

            // add files to project
            AddFilesAction action = new AddFilesAction(insertions, true);
            await ProjectService.ApplyActionAsync(action);
        }

        private async Task TrySaveAsync()
        {
            if (CurrentProject == null) return;
            await CurrentProject.SaveAsync(viewDispatcherQueue!);
        }

        private async Task TryCopySelectionAsync()
        {
            if (CurrentProject == null) return;
            
            if (CurrentProject is PdfProject)
            {
                await ProjectService.TryCopyProjectAsync();
            }
            else if (CurrentProject is ImageProject imageProject && ProjectService.SelectedPage != null)
            {
                await ProjectService.TryCopyPagesAsync(new List<IProjectPage>([ProjectService.SelectedPage]));
            }
        }

        private async Task TryOpenWithAsync(AppInfo? app)
        {
            if (CurrentProject == null) return;

            if (CurrentProject is PdfProject pdfProject)
            {
                await ProjectService.TryOpenWithProjectAsync(app);
            }
            if (CurrentProject is ImageProject)
            {
                if (ProjectService.SelectedPage == null) return;
                await ProjectService.TryOpenWithPageAsync(app, ProjectService.SelectedPage);
            }
        }

        private async Task UpdateOpenWithTargetsAsync()
        {
            if (CurrentProject == null) return;
            List<OpenWithTarget> result = new();

            // find installed apps for file type
            string fileExtension = TargetFormatToFileExtension(CurrentProject.Format);
            IReadOnlyList<AppInfo> readOnlyList = await Windows.System.Launcher.FindFileHandlersAsync(fileExtension);
            foreach (AppInfo appInfo in readOnlyList)
            {
                try
                {
                    RandomAccessStreamReference stream = appInfo.DisplayInfo.GetLogo(new Size(128, 128));
                    using (IRandomAccessStreamWithContentType content = await stream.OpenReadAsync())
                    {
                        BitmapImage bmp = new BitmapImage();
                        await bmp.SetSourceAsync(content);
                        result.Add(new OpenWithTarget(appInfo, bmp));
                    }
                }
                catch (Exception)
                {
                    // add without logo
                    result.Add(new OpenWithTarget(appInfo, null));
                }

                if (result.Count >= 5) break;   // 5 apps max
            }

            OpenWithTargets = result;
        }

        private async Task FindAppForFileTypeAsync()
        {
            if (CurrentProject == null) return;

            string fileExtension = TargetFormatToFileExtension(CurrentProject.Format);
            await Windows.System.Launcher.LaunchUriAsync(new Uri($"ms-windows-store://assoc/?FileExt={fileExtension.Substring(1)}"));
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record OpenWithTarget(AppInfo AppInfo, BitmapImage? Logo);
}
