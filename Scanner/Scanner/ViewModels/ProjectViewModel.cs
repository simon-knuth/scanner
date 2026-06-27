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
using Scanner.Services;
using Scanner.Services.Interfaces;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Models.PdfProjectSnapshot;
using static System.Net.WebRequestMethods;

namespace Scanner.ViewModels;

partial class ProjectViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetService<ICopilotRuntimeService>();
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
    public readonly ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
    public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    #endregion

    #region Commands
    public AsyncRelayCommand TryRemoveCurrentPageAsyncCommand => new AsyncRelayCommand(TryRemoveCurrentPageAsync);
    public AsyncRelayCommand TryDeleteProjectAsyncCommand => new AsyncRelayCommand(TryDeleteProjectAsync);
    public AsyncRelayCommand TryCopyProjectOrPageAsyncCommand => new AsyncRelayCommand(TryCopyProjectOrPageAsync);
    public AsyncRelayCommand TryShareProjectOrPageAsyncCommand => new AsyncRelayCommand(TryShareProjectOrPageAsync);
    public AsyncRelayCommand<AppInfo?> TryOpenWithAsyncCommand => new AsyncRelayCommand<AppInfo?>(TryOpenWithAsync);
    public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
    public RelayCommand SelectPreviousPageCommand => new RelayCommand(SelectPreviousPage);
    public RelayCommand SelectNextPageCommand => new RelayCommand(SelectNextPage);
    public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
    public AsyncRelayCommand<IProjectPage?> ShowInFileExplorerAsyncCommand => new AsyncRelayCommand<IProjectPage?>(ShowInFileExplorerAsync);
    public AsyncRelayCommand SaveAsyncCommand => new AsyncRelayCommand(SaveAsync);
    public AsyncRelayCommand SaveAsAsyncCommand => new AsyncRelayCommand(SaveAsAsync);
    public AsyncRelayCommand SaveAsCurrentPageAsyncCommand => new AsyncRelayCommand(SaveAsCurrentPageAsync);
    public AsyncRelayCommand PickAndAddFilesCommand => new AsyncRelayCommand(PickAndAddFilesAsync, canExecute: () => CurrentProject?.IsPdf is true);
    public AsyncRelayCommand<List<StorageFile>?> AddFilesCommand => new AsyncRelayCommand<List<StorageFile>?>(AddFilesAsync, canExecute: (x) => CurrentProject?.IsPdf is true);
    public AsyncRelayCommand<TargetFormat> ConvertProjectAsyncCommand => new AsyncRelayCommand<TargetFormat>(ConvertProjectAsync);
    public AsyncRelayCommand FindAppForFileTypeCommand => new AsyncRelayCommand(FindAppForFileTypeAsync);
    public AsyncRelayCommand RotateSelectedPages90DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateSelectedPagesAsync(RotationIntent.Degrees90));
    public AsyncRelayCommand RotateSelectedPages180DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateSelectedPagesAsync(RotationIntent.Degrees180));
    public AsyncRelayCommand RotateSelectedPages270DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateSelectedPagesAsync(RotationIntent.Degrees270));
    public AsyncRelayCommand RotateSelectedPagesAutomaticallyAsyncCommand => new AsyncRelayCommand(async (x) => await RotateSelectedPagesAsync(RotationIntent.Automatic));
    public AsyncRelayCommand RemoveSelectedPagesAsyncCommand => new AsyncRelayCommand(RemoveSelectedPagesAsync);
    public AsyncRelayCommand TryCopySelectedPagesAsyncCommand => new AsyncRelayCommand(TryCopySelectedPagesAsync);
    public AsyncRelayCommand TryShareSelectedPagesAsyncCommand => new AsyncRelayCommand(TryShareSelectedPagesAsync);
    public AsyncRelayCommand ExportSelectedPagesAsSeparatePDFAsyncCommand => new AsyncRelayCommand(ExportSelectedPagesAsSeparatePDFAsync);
    public AsyncRelayCommand<ImageFilter> ApplyFilterToSelectedPagesAsyncCommand => new AsyncRelayCommand<ImageFilter>(ApplyFilterToSelectedPagesAsync);
    public RelayCommand StartStopGenerateFileNameWithAICommand => new RelayCommand(StartStopGenerateFileNameWithAI);
    public AsyncRelayCommand ApplyOrderOfPagesToProjectAsyncCommand => new AsyncRelayCommand(ApplyOrderOfPagesToProjectAsync);
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
                OnPropertyChanged(nameof(IsFileNameGenerationInProgress));
                AddFilesCommand.NotifyCanExecuteChanged();

                if (value != null && value is PdfProject pdfProject2) pdfProject2.FileNameInfo.NameChanged += FileNameInfo_NameChanged;
            }
        }
    }

    /// <summary>
    /// A copy of <see cref="ProjectBase.Pages"/> which is always in sync with the original, unless the user just reordered
    /// it and the new order is about to be applied to the original.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<IProjectPage> pages = [];

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

    public bool IsFileNameGenerationInProgress
    {
        get
        {
            if (CurrentProject == null) return false;

            if (CurrentProject is PdfProject pdfProject)
            {
                return pdfProject.FileNameInfo.IsNameGenerationInProgress;
            }
            else if (ProjectService.SelectedPage is ImagePage imagePage)
            {
                return imagePage.FileNameInfo.IsNameGenerationInProgress;
            }
            return false;
        }
    }

    private bool isMultiSelect;
    public bool IsMultiSelect
    {
        get => isMultiSelect;
        set
        {
            IProjectPage? selectedPage = ProjectService.SelectedPage;
            if (SetProperty(ref isMultiSelect, value))
            {
                if (value)
                {
                    ProjectService.SelectedPage = null;

                    if (selectedPage != null)
                    {
                        ProjectService.SelectedPages = new([selectedPage]);
                    }
                    else
                    {
                        ProjectService.SelectedPages = new();
                    }
                }
                else
                {
                    ProjectService.SelectedPage = ProjectService.SelectedPages?.OrderBy(x => x.Index).FirstOrDefault();
                    if (ProjectService.SelectedPage == null && ProjectService.CurrentProject?.Pages.Count > 0)
                    {
                        ProjectService.MakeDefaultSelection();
                    }
                    ProjectService.SelectedPages = null;
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

        if (CurrentProject != null)
            Pages = new(CurrentProject.Pages);
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
            case nameof(IProjectService.CurrentProject):
                if (ProjectService.CurrentProject != null)
                    ProjectService.CurrentProject.Pages.CollectionChanged -= Pages_CollectionChanged;

                if (ProjectService.CurrentProject != null && ProjectService.CurrentProject is PdfProject pdfProject)
                    pdfProject.FileNameInfo.PropertyChanged -= FileNameInfo_PropertyChanged;
                break;
            case nameof(IProjectService.SelectedPage):
                if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage && imagePage.FileNameInfo != null)
                {
                    imagePage.FileNameInfo.NameChanged -= FileNameInfo_NameChanged;
                    imagePage.FileNameInfo.PropertyChanged -= FileNameInfo_PropertyChanged;
                }
                break;
            case nameof(IProjectService.IsScanProcessRunning):
                if (ProjectService.IsScanProcessRunning)
                {
                    IsMultiSelect = false;
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

                if (CurrentProject != null)
                {
                    CurrentProject.Pages.CollectionChanged += Pages_CollectionChanged;
                    Pages = new(CurrentProject.Pages);
                }

                if (ProjectService.CurrentProject != null && ProjectService.CurrentProject is PdfProject pdfProject)
                {
                    pdfProject.FileNameInfo.PropertyChanged += FileNameInfo_PropertyChanged;
                    OnPropertyChanged(nameof(IsFileNameGenerationInProgress));
                }
                break;
            case nameof(IProjectService.SelectedPage):
                if (CurrentProject != null && !CurrentProject.IsPdf)
                {
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(IsFileNameGenerationInProgress));
                }

                if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage && imagePage.FileNameInfo != null)
                {
                    imagePage.FileNameInfo.NameChanged += FileNameInfo_NameChanged;
                    imagePage.FileNameInfo.PropertyChanged += FileNameInfo_PropertyChanged;
                }
                break;
        }
    }

    private void Pages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ObservableCollection<IProjectPage>? collection = sender as ObservableCollection<IProjectPage>;
        if (collection == null)
            return;

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                if (e.NewItems == null)
                    return;

                foreach (IProjectPage page in e.NewItems)
                {
                    int index = collection.IndexOf(page);
                    if (index >= 0)
                        Pages.Insert(index, page);
                }
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null)
                    return;

                foreach (IProjectPage page in e.OldItems)
                {
                    Pages.Remove(page);
                }
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                if (e.NewItems == null)
                    return;

                foreach (IProjectPage page in e.NewItems)
                {
                    int index = Pages.IndexOf(page);
                    if (index != -1)
                        Pages[index] = page;
                }
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                if (collection[e.NewStartingIndex] != Pages[e.NewStartingIndex])
                    Pages.Move(e.OldStartingIndex, e.NewStartingIndex);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                Pages = new(collection);
                break;
        }
    }

    private void FileNameInfo_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileNameInfo.IsNameGenerationInProgress))
            OnPropertyChanged(nameof(IsFileNameGenerationInProgress));
    }

    private void FileNameInfo_NameChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(IsFileNameGenerationInProgress));
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
            (ImagePage)ProjectService.SelectedPage
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

        // get folder
        StorageFolder? folder = null;
        if (CurrentProject is PdfProject pdfProject)
        {
            folder = pdfProject.TargetFile != null ? pdfProject.TargetFolder : null;
        }
        else
        {
            // use currently selected page if no page is provided
            if (page == null && ProjectService.SelectedPage != null)
                page = ProjectService.SelectedPage;

            if (page is not ImagePage imagePage)
                return;

            folder = imagePage.TargetFile != null ? imagePage.TargetFolder : null;
        }

        // ensure folder
        if (folder == null)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
            {
                Title = "Project not saved",
                Message = "The project needs to be saved to complete this action.",
                Severity = InfoBarSeverity.Error
            }));
            return;
        }

        // open it
        await Windows.System.Launcher.LaunchFolderAsync(folder);
    }

    private async Task PickAndAddFilesAsync()
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

        // insert
        await AddFilesAsync([.. files]);
    }

    private async Task AddFilesAsync(List<StorageFile>? files)
    {
        if (files is null)
            return;

        if (CurrentProject == null)
            return;

        if (!CurrentProject.IsPdf)
            throw new ApplicationException("Adding files is only supported for PDF projects");

        if (files == null || files.Count == 0)
            return;

        // construct insertions
        List<ProjectFileInsertion> insertions = new();
        for (int i = 0; i < files.Count; i++)
        {
            insertions.Add(new ProjectFileInsertion(files[i], CurrentProject.Pages.Count + i, null, null, ImageFilter.None, ImageFilter.None, AppConfig.DefaultBrightness, AppConfig.DefaultContrast));
        }

        // add files to project
        AddFilesAction action = new AddFilesAction(insertions, true);
        await ProjectService.ApplyActionAsync(action);

        SentryService?.TrackEvent(AnalyticsEvent.AddImageFiles, new Dictionary<string, string>
        {
            { "files", files.Count.ToString() }
        });
    }

    private async Task ConvertProjectAsync(TargetFormat targetFormat)
    {
        if (CurrentProject == null) return;
        await ProjectService.TryConvertProjectAsync(targetFormat, viewDispatcherQueue!);
    }

    private async Task SaveAsync()
    {
        if (CurrentProject == null) return;
        await CurrentProject.SaveAsync(false, viewDispatcherQueue!, isUserInitiated: true);
    }

    private async Task SaveAsAsync()
    {
        if (CurrentProject == null) return;
        await CurrentProject.SaveAsync(true, viewDispatcherQueue!, isUserInitiated: true);
    }

    private async Task SaveAsCurrentPageAsync()
    {
        if (CurrentProject == null) return;
        if (CurrentProject is not MultiFileProject imageProject) return;

        if (ProjectService.SelectedPage != null)
            await imageProject.SaveAsSinglePageAsync(ProjectService.SelectedPage, viewDispatcherQueue!);
    }

    private async Task TryCopyProjectOrPageAsync()
    {
        if (CurrentProject == null) return;
        
        if (CurrentProject is PdfProject)
        {
            await ProjectService.TryCopyProjectAsync();
        }
        else if (CurrentProject is MultiFileProject imageProject && ProjectService.SelectedPage != null)
        {
            await ProjectService.TryCopyPagesAsync([.. imageProject.Pages.Cast<ImagePage>()]);
        }
    }

    private async Task TryShareProjectOrPageAsync()
    {
        if (CurrentProject == null) return;

        if (CurrentProject is PdfProject)
        {
            await ProjectService.TryShareProjectAsync();
        }
        else if (CurrentProject is MultiFileProject imageProject && ProjectService.SelectedPage != null)
        {
            await ProjectService.TrySharePagesAsync([.. imageProject.Pages.Cast<ImagePage>()]);
        }
    }

    private async Task TryOpenWithAsync(AppInfo? app)
    {
        if (CurrentProject == null)
            return;

        if (CurrentProject is PdfProject pdfProject)
        {
            if (await ProjectService.TryOpenWithProjectAsync(app) && app != null)
                SettingsService.LastOpenWithAppPdf = app.AppUserModelId;
        }
        if (CurrentProject is MultiFileProject)
        {
            if (ProjectService.SelectedPage == null)
                return;

            if (await ProjectService.TryOpenWithPageAsync(app, (ImagePage)ProjectService.SelectedPage) && app != null)
            {
                switch (CurrentProject.Format)
                {
                    case TargetFormat.JPG:
                        SettingsService.LastOpenWithAppJpg = app.AppUserModelId;
                        break;
                    case TargetFormat.PNG:
                        SettingsService.LastOpenWithAppPng = app.AppUserModelId;
                        break;
                    case TargetFormat.BMP:
                        SettingsService.LastOpenWithAppBmp = app.AppUserModelId;
                        break;
                    case TargetFormat.SinglePagePDF:
                        SettingsService.LastOpenWithAppPdf = app.AppUserModelId;
                        break;
                    case TargetFormat.TIFF:
                        SettingsService.LastOpenWithAppTiff = app.AppUserModelId;
                        break;
                    case TargetFormat.None:
                        break;
                }
            }
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

    private async Task RotateSelectedPagesAsync(RotationIntent rotationIntent)
    {
        if (CurrentProject == null) return;
        if (!IsMultiSelect) return;
        if (ProjectService.SelectedPagesCount == 0) return;
        if (ProjectService.SelectedPages == null) return;

        // gather instructions
        Dictionary<ImagePage, RotationIntent> rotations = new();
        foreach (IProjectPage page in ProjectService.SelectedPages)
        {
            rotations.Add((ImagePage)page, rotationIntent);
        }

        Task process = ProjectService.ApplyActionAsync(new RotatePagesAction(rotations));

        if (rotations.Count > 1)
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, process));

        await process;
    }

    private async Task RemoveSelectedPagesAsync()
    {
        if (CurrentProject == null) return;
        if (!IsMultiSelect) return;
        if (ProjectService.SelectedPagesCount == 0) return;
        if (ProjectService.SelectedPages == null) return;

        List<ImagePage> pages = [.. ProjectService.SelectedPages.Cast<ImagePage>()];

        Task process = ProjectService.ApplyActionAsync(new RemovePagesAction(pages));

        if (pages.Count > 1)
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, process));

        await process;
    }

    private async Task TryCopySelectedPagesAsync()
    {
        if (CurrentProject == null) return;
        if (ProjectService.SelectedPagesCount == 0) return;

        if (IsMultiSelect && ProjectService.SelectedPages != null)
            await ProjectService.TryCopyPagesAsync([.. ProjectService.SelectedPages.Cast<ImagePage>()]);
        else if (ProjectService.SelectedPage != null)
            await ProjectService.TryCopyPagesAsync([(ImagePage)ProjectService.SelectedPage]);
    }

    private async Task TryShareSelectedPagesAsync()
    {
        if (CurrentProject == null) return;
        if (ProjectService.SelectedPagesCount == 0) return;

        if (IsMultiSelect && ProjectService.SelectedPages != null)
            await ProjectService.TrySharePagesAsync([.. ProjectService.SelectedPages.Cast<ImagePage>()]);
        else if (ProjectService.SelectedPage != null)
            await ProjectService.TrySharePagesAsync([(ImagePage)ProjectService.SelectedPage]);
    }

    private async Task ApplyFilterToSelectedPagesAsync(ImageFilter filter)
    {
        if (CurrentProject == null) return;
        if (!IsMultiSelect) return;
        if (ProjectService.SelectedPagesCount == 0) return;
        if (ProjectService.SelectedPages == null) return;

        List<ImagePage> pages = ProjectService.SelectedPages.OfType<ImagePage>().ToList();
        if (pages.Count > 0)
        {
            await ProjectService.ApplyActionAsync(new ApplyFilterAction(pages, filter));
        }
    }

    private void StartStopGenerateFileNameWithAI()
    {
        if (CurrentProject == null) return;

        if (CurrentProject is PdfProject pdfProject)
        {
            if (pdfProject.FileNameInfo.IsNameGenerationInProgress)
            {
                SentryService?.TrackEvent(AnalyticsEvent.AIFileNameGenerationCancelled);
                pdfProject.FileNameInfo.NameGenerationCts?.Cancel();
            }
            else
                Task.Run(() => pdfProject.GenerateFileNameWithAIAsync(viewDispatcherQueue));
        }
        if (CurrentProject is MultiFileProject)
        {
            throw new NotImplementedException();
        }
    }

    private async Task ApplyOrderOfPagesToProjectAsync()
    {
        if (CurrentProject == null) return;

        IProjectPage? selectedPage = ProjectService.SelectedPage;
        await ProjectService.ApplyActionAsync(new ApplyOrderOfPagesAction(Pages.ToList()));
        ProjectService.SelectedPage = selectedPage;
    }

    private async Task ExportSelectedPagesAsSeparatePDFAsync()
    {
        if (CurrentProject == null) return;
        if (CurrentProject is not PdfProject pdfProject) return;
        if (ProjectService.SelectedPagesCount == 0) return;

        await Task.Run(async () =>
        {
            // get save options
            SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, CurrentProject.InitialScanOptions, CurrentProject,
                true, viewDispatcherQueue!, true, pdfProject.FileNameInfo.DesiredDisplayName);
            if (saveOptions == null)
                return;

            // show loading dialog
            TaskCompletionSource tcs = new();
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ExportInProgress, tcs.Task));

            // export
            if (IsMultiSelect && ProjectService.SelectedPages != null)
            {
                // export selected pages as one PDF file
                Dictionary<IProjectPage, IProjectSnapshotPage> pages = [];
                foreach (IProjectPage page in ProjectService.SelectedPages.OrderBy(x => x.Index))
                {
                    if (page is ImagePage imagePage)
                        pages.Add(page, new PdfProjectSnapshotPage(imagePage.SourceFile, null, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
                    else if (page is PdfPage pdfPage)
                        pages.Add(page, new PdfProjectSnapshotPage(pdfProject.SourceFile!.File, pdfPage.IndexInPdf, ImageFilter.None, AppConfig.DefaultBrightness, AppConfig.DefaultContrast));
                }
                await PdfProject.CreatePdfFromPagesAsync(pages, null, saveOptions.FileName, saveOptions.TargetFolder, SettingsService.SettingOcrPdfs, viewDispatcherQueue!);

                SentryService?.TrackEvent(AnalyticsEvent.ExportPagesFromPdf, new Dictionary<string, string>
                {
                    { "scope", "Some" },
                    { "pages", pages.Count.ToString() },
                });
            }
            else if (ProjectService.SelectedPage != null)
            {
                // export every page as a separate PDF
                foreach (IProjectPage page in CurrentProject.Pages)
                {
                    Dictionary<IProjectPage, IProjectSnapshotPage> pages = [];
                    if (page is ImagePage imagePage)
                        pages.Add(page, new PdfProjectSnapshotPage(imagePage.SourceFile, null, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
                    else if (page is PdfPage pdfPage)
                        pages.Add(page, new PdfProjectSnapshotPage(pdfProject.SourceFile!.File, pdfPage.IndexInPdf, ImageFilter.None, AppConfig.DefaultBrightness, AppConfig.DefaultContrast));

                    await PdfProject.CreatePdfFromPagesAsync(pages, null, saveOptions.FileName, saveOptions.TargetFolder, SettingsService.SettingOcrPdfs, viewDispatcherQueue!);
                }

                SentryService?.TrackEvent(AnalyticsEvent.ExportPagesFromPdf, new Dictionary<string, string>
                {
                    { "scope", "All" },
                    { "pages", CurrentProject.Pages.Count.ToString() },
                });
            }

            tcs.TrySetResult();
        });
    }
}


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public record OpenWithTarget(AppInfo AppInfo, BitmapImage? Logo);
