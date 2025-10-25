using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Helpers.Helpers;
using Scanner.Extensions;

namespace Scanner.Services
{
    internal partial class ProjectService : ObservableRecipient, IProjectService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetRequiredService<ICopilotRuntimeService>();
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        private ProjectBase? currentProject;
        public ProjectBase? CurrentProject
        {
            get => currentProject;
            private set
            {
                if (currentProject != value)
                {
                    if (currentProject != null)
                    {
                        currentProject.PagesAdded -= CurrentProject_PagesAdded;
                        currentProject.PagesRemoved -= CurrentProject_PagesRemoved;
                        currentProject.PropertyChanged -= CurrentProject_PropertyChanged;
                    }

                    if (SetProperty(ref currentProject, value))
                    {
                        OnPropertyChanged(nameof(CanSelectPreviousPage));
                        OnPropertyChanged(nameof(CanSelectNextPage));
                        OnPropertyChanged(nameof(CanSaveProject));
                        OnPropertyChanged(nameof(TotalNumberOfPages));
                    }

                    if (value != null)
                    {
                        value.PagesAdded += CurrentProject_PagesAdded;
                        value.PagesRemoved += CurrentProject_PagesRemoved;
                        value.PropertyChanged += CurrentProject_PropertyChanged;
                    }
                }
            }
        }

        public int TotalNumberOfPages => CurrentProject?.Pages?.Count ?? 0;

        private IProjectPage? selectedPage;
        public IProjectPage? SelectedPage
        {
            get => selectedPage;
            set
            {
                if (SetProperty(ref selectedPage, value))
                {
                    OnPropertyChanged(nameof(CanSelectPreviousPage));
                    OnPropertyChanged(nameof(CanSelectNextPage));

                    if (value != null)
                        SelectedPagesCount = 1;
                }
            }
        }

        private ObservableCollection<IProjectPage>? selectedPages;
        public ObservableCollection<IProjectPage>? SelectedPages
        {
            get => selectedPages;
            set
            {
                if (SetProperty(ref selectedPages, value) && value != null)
                {
                    value.CollectionChanged += SelectedPages_CollectionChanged;
                }
            }
        }

        [ObservableProperty]
        private int selectedPagesCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
        [NotifyPropertyChangedFor(nameof(IsProcessRunningOrEditing))]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        [NotifyPropertyChangedFor(nameof(CanSaveProject))]
        private bool isActionRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
        [NotifyPropertyChangedFor(nameof(IsProcessRunningOrEditing))]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        [NotifyPropertyChangedFor(nameof(CanSaveProject))]
        private bool isEditing;

        public bool IsProcessRunning => IsScanProcessRunning || IsActionRunning;
        public bool IsProcessRunningOrEditing => IsScanProcessRunning || IsActionRunning || IsEditing;

        private bool isScanProcessRunning;
        public bool IsScanProcessRunning
        {
            get => isScanProcessRunning;
            private set
            {
                SetProperty(ref isScanProcessRunning, value);
                OnPropertyChanged(nameof(IsProcessRunning));
                OnPropertyChanged(nameof(IsProcessRunningOrEditing));
                OnPropertyChanged(nameof(CanSaveProject));
            }
        }

        private ScanState currentScanState;
        public ScanState CurrentScanState
        {
            get => currentScanState;
            private set
            {
                SetProperty(ref currentScanState, value);
                OnPropertyChanged(nameof(FriendlyCurrentScanState));
            }
        }

        public string FriendlyCurrentScanState => GetFriendlyCurrentScanState();


        // TODO: Update properties if selected page is moved
        public bool CanSelectPreviousPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index > 0;
        public bool CanSelectNextPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index < CurrentProject.Pages.Count - 1;

        public Stack<IProjectAction> UndoStack { get; private set; } = new();
        public Stack<IProjectAction> RedoStack { get; private set; } = new();
        public bool CanUndo => UndoStack.Count > 0;
        public bool CanRedo => RedoStack.Count > 0;

        public bool CanSaveProject => CurrentProject != null && !IsProcessRunning && !CurrentProject.IsSaved;

        public DispatcherQueue? UiDispatcherQueue { get; set; }

        private ThreadPoolTimer? autoSaveTimer;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectService()
        {
            SettingsService.PropertyChanged += SettingsService_PropertyChanged;
            ResetAutoSaveTimer();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task TryCreateProjectAsync(IProjectCreationData creationData, DispatcherQueue uiDispatcherQueue)
        {
            try
            {
                // close project
                if (await TryCloseProjectAsync() == false) return;

                // create project
                CurrentScanState = ScanState.Processing;
                ProjectBase? project = null;
                await Task.Run(async () =>
                {
                    project = await creationData.CreateProjectAsync(false, uiDispatcherQueue);
                });
                CurrentProject = project;

                // save if needed
                if (SettingsService.SettingAutoSave)
                {
                    // save
                    await Task.Run(async () => await project.SaveAsync(false, UiDispatcherQueue!));
                }

                // free up space
                _ = Task.Run(() => _ = AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder));
            }
            catch (Exception)
            {
                // TODO: catch exceptions and notify user
                throw;
            }
            finally
            {
                IsActionRunning = IsScanProcessRunning = false;
            }
        }

        public async Task TryCreateProjectFromScanAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue)
        {
            try
            {
                CurrentScanState = ScanState.Scanning;

                // close project
                if (await TryCloseProjectAsync() == false) return;

                // get save options
                IsActionRunning = true;
                SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(UiDispatcherQueue!, ((App)Application.Current).MainWindow, scanOptions, CurrentProject, false, false, null);
                if (saveOptions == null) return;

                // preheat AI models
                if (saveOptions.GenerateAIFileName)
                    _ = Task.Run(CopilotRuntimeService.PreheatFileNameGenerationModelsAsync);

                // scan
                IsScanProcessRunning = true;
                await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
                IReadOnlyList<StorageFile> files = [];
                await Task.Run(async () => files = await scanOptions.Scanner.GetScanAsync(scanOptions, AppDataService.IncomingFolder));

                if (files.Count == 0)
                    return;

                // automatic rotation
                if (SettingsService.SettingAutoRotate)
                {
                    CurrentScanState = ScanState.AutomaticRotation;

                    await Task.Run(async () =>
                    {
                        Dictionary<StorageFile, RotationIntent> instructions = new();
                        foreach (StorageFile file in files)
                        {
                            instructions.Add(file, RotationIntent.Automatic);
                        }

                        await ProjectBase.RotateFilesAsync(instructions, true, AppDataService.IncomingFolder);
                    });
                }

                // create project
                CurrentScanState = ScanState.Processing;
                ProjectBase? project = null;
                await Task.Run(async () =>
                {
                    switch (scanOptions.TargetFormat)
                    {
                        case TargetFormat.PDF:
                            PdfProjectCreationData pdfCreationData = new(files, saveOptions.FileName, saveOptions.TargetFolder, scanOptions);
                            project = await pdfCreationData.CreateProjectAsync(false, uiDispatcherQueue);
                            break;
                        case TargetFormat.JPG:
                        case TargetFormat.PNG:
                        case TargetFormat.BMP:
                        case TargetFormat.TIFF:
                        case TargetFormat.RAW:
                            ImageProjectCreationData imageCreationData = new(files, scanOptions.TargetFormat, saveOptions.FileName, saveOptions.TargetFolder, scanOptions);
                            project = await imageCreationData.CreateProjectAsync(false, uiDispatcherQueue);
                            break;
                        default:
                            throw new ArgumentException($"Can't create project for format {scanOptions.TargetFormat}");
                    }
                });
                CurrentProject = project;

                // kick off AI file name generation
                if (saveOptions.GenerateAIFileName)
                {
                    if (CurrentProject is PdfProject pdfProject)
                    {
                        // load bitmap
                        using ImageBuffer imageBuffer = await pdfProject.GetImageBufferForAIFileNameGenerationAsync(uiDispatcherQueue);

                        // generate name in the background
                        _ = Task.Run(async () => await pdfProject.GenerateFileNameWithAIAsync(imageBuffer, uiDispatcherQueue));
                    }
                    else if (CurrentProject is ImageProject imageProject)
                    {
                        throw new NotImplementedException();
                    }
                }

                // save if needed
                if (SettingsService.SettingAutoSave)
                {
                    // save
                    if (scanOptions.TargetFormat == TargetFormat.PDF)
                    {
                        CurrentScanState = ScanState.GeneratingPDF;
                    }
                    else
                    {
                        CurrentScanState = ScanState.Saving;
                    }
                    await Task.Run(async () => await CurrentProject.SaveAsync(false, UiDispatcherQueue!));
                }

                // free up space
                _ = Task.Run(() => _ = AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder));
            }
            catch (Exception)
            {
                // TODO: catch exceptions and notify user
                throw;
            }
            finally
            {
                IsActionRunning = IsScanProcessRunning = false;
                _ = Task.Run(CopilotRuntimeService.StopPreheatingFileNameGenerationModelsAsync);
            }
        }

        public async Task TryScanToProjectAsync(ScanOptions scanOptions)
        {
            try
            {
                CurrentScanState = ScanState.Scanning;

                if (CurrentProject == null) return;

                // get save options
                IsActionRunning = true;
                SaveOptions? saveOptions = null;
                if (scanOptions.TargetFormat != TargetFormat.PDF)
                {
                    saveOptions = await SaveLocationService.GetSaveOptionsAsync(UiDispatcherQueue!, ((App)Application.Current).MainWindow, scanOptions, CurrentProject, false, false, null);
                    if (saveOptions == null) return;
                }

                // scan
                IsScanProcessRunning = true;
                await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
                IReadOnlyList<StorageFile> files = [];
                await Task.Run(async () => files = await scanOptions.Scanner.GetScanAsync(scanOptions, AppDataService.IncomingFolder));

                // automatic rotation
                if (SettingsService.SettingAutoRotate)
                {
                    CurrentScanState = ScanState.AutomaticRotation;

                    await Task.Run(async () =>
                    {
                        Dictionary<StorageFile, RotationIntent> instructions = new();
                        foreach (StorageFile file in files)
                        {
                            instructions.Add(file, RotationIntent.Automatic);
                        }

                        await ProjectBase.RotateFilesAsync(instructions, true, AppDataService.IncomingFolder);
                    });
                }

                // add files
                CurrentScanState = ScanState.Processing;
                List<ProjectFileInsertion> insertions = new();
                for (int i = 0; i < files.Count; i++)
                {
                    insertions.Add(new ProjectFileInsertion(files[i], CurrentProject.Pages.Count + i, saveOptions?.FileName, saveOptions?.TargetFolder, scanOptions.GetBaseFilter(), scanOptions.GetFilter(), scanOptions.Brightness, scanOptions.Contrast));
                }
                IProjectAction action = new AddFilesAction(insertions, false);

                await ApplyActionAsync(action);
            }
            catch (Exception)
            {
                // TODO: catch exceptions and notify user
                throw;
            }
            finally
            {
                IsActionRunning = IsScanProcessRunning = false;
            }
        }

        public async Task<bool> TryDeleteProjectAsync()
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // get confirmation and delete
                if (await Messenger.Send(new ShowProjectDeletionDialogMessage(CurrentProject)).Response == false) return false;

                // close project
                await TryCloseProjectAsync(ignoreUnsavedChanges: true);
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the actions couldn't be completed"
                }));
            }
            catch (Exception)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the project needs to be closed"
                }));

                // close project
                await TryCloseProjectAsync(ignoreUnsavedChanges: true);
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TrySaveProjectAsync()
        {
            if (CurrentProject == null) return true;

            // handle unsaved changes
            if (!CurrentProject.IsSaved && await Messenger.Send(new ShowUnsavedChangesDialogMessage()).Response == false)
            {
                // changes couldn't be handled
                return false;
            }

            // changes were handled (saved or discarded)
            return true;
        }

        public async Task<bool> TryCopyProjectAsync()
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // copy project
                if (CurrentProject is PdfProject pdfProject)
                {
                    await pdfProject.CopyAsync();
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TryCopyPagesAsync(List<IProjectPage> pages)
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // copy project
                if (CurrentProject is ImageProject imageProject)
                {
                    await imageProject.CopyPagesAsync(pages);
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TryOpenWithProjectAsync(AppInfo? app)
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // open with project
                if (CurrentProject is PdfProject pdfProject)
                {
                    await pdfProject.TryOpenWithAsync(app);
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TryOpenWithPageAsync(AppInfo? app, IProjectPage page)
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // open with page
                if (CurrentProject is ImageProject imageProject)
                {
                    await imageProject.TryOpenWithPageAsync(app, page);
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TryShareProjectAsync()
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // share project
                if (CurrentProject is PdfProject pdfProject)
                {
                    await pdfProject.ShareAsync();
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TrySharePagesAsync(List<IProjectPage> pages)
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // copy project
                if (CurrentProject is ImageProject imageProject)
                {
                    await imageProject.SharePagesAsync(pages);
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the action couldn't be completed"
                }));
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        public async Task<bool> TryCloseProjectAsync(bool preserveSourceFilesInIncomingFolder = false, bool ignoreUnsavedChanges = false)
        {
            if (CurrentProject == null)
                return true;

            // handle unsaved changes
            if (!ignoreUnsavedChanges && await TrySaveProjectAsync() == false)
            {
                return false;
            }

            // close project
            ObservableCollection<IProjectPage> pages = CurrentProject.Pages;
            CurrentProject = null;
            SelectedPage = null;
            await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);

            if (preserveSourceFilesInIncomingFolder)
            {
                // move all source files to the Incoming folder before clearing
                Task[] tasks = new Task[pages.Count];
                for (int i = 0; i < pages.Count; i++)
                {
                    tasks[i] = pages[i].SourceFile.MoveAsync(AppDataService.IncomingFolder, pages[i].SourceFile.Name, NameCollisionOption.GenerateUniqueName).AsTask();
                }
                await Task.WhenAll(tasks);
            }

            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.PreviewFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.ChangesFolder);                

            // update undo/redo stacks
            UndoStack.Clear();
            RedoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            // end processes
            IsActionRunning = false;
            IsEditing = false;
            IsScanProcessRunning = false;

            return true;
        }

        public void SelectPreviousPage()
        {
            if (CurrentProject == null) return;
            if (SelectedPage == null) return;

            if (CanSelectPreviousPage)
            {
                SelectedPage = CurrentProject.Pages[SelectedPage.Index - 1];
            }
        }

        public void SelectNextPage()
        {
            if (CurrentProject == null) return;
            if (SelectedPage == null) return;

            if (CanSelectNextPage)
            {
                SelectedPage = CurrentProject.Pages[SelectedPage.Index + 1];
            }
        }

        public async Task ApplyActionAsync(IProjectAction action)
        {
            await InternalApplyActionAsync(action, false);
        }

        private async Task InternalApplyActionAsync(IProjectAction action, bool redoing)
        {
            if (CurrentProject == null) return;

            try
            {
                bool changesMade = false;
                bool merged = false;

                if (action is IAtomicProjectAction atomicProjectAction)
                {
                    // check if action can be merged with previous one
                    UndoStack.TryPeek(out IProjectAction? undoAction);
                    if (!redoing
                        && undoAction != null
                        && undoAction is IAtomicProjectAction previousAtomicProjectAction
                        && DateTime.Now < previousAtomicProjectAction.MostRecentExecution + AppConfig.ConsecutiveAtomicActionMergeTime
                        && previousAtomicProjectAction.Page == atomicProjectAction.Page
                        && previousAtomicProjectAction.IsActionCompatibleForMerge(atomicProjectAction))
                    {
                        // merge with previous action
                        changesMade = previousAtomicProjectAction.MergeAndExecute(CurrentProject, atomicProjectAction, UiDispatcherQueue!);
                        merged = true;
                    }
                    else
                    {
                        // use separate action
                        changesMade = atomicProjectAction.Execute(CurrentProject, UiDispatcherQueue!);
                    }
                }
                else
                {
                    IsActionRunning = true;
                    changesMade = await action.ExecuteAsync(CurrentProject, UiDispatcherQueue!);
                }

                if (changesMade && !merged && CurrentProject != null)
                {
                    // update undo stack
                    UndoStack.Push(action);
                    OnPropertyChanged(nameof(CanUndo));

                    // update redo
                    if (!redoing)
                    {
                        RedoStack.Clear();
                        await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
                    }
                    OnPropertyChanged(nameof(CanRedo));
                }
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the actions couldn't be completed"
                }));

                if (redoing)
                {
                    // update redo stack
                    RedoStack.Push(action);
                    OnPropertyChanged(nameof(RedoStack));
                }
            }
            catch (Exception)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the project needs to be closed"
                }));
                await TryCloseProjectAsync(ignoreUnsavedChanges: true);
            }
            finally
            {
                IsActionRunning = false;
            }
        }

        private async Task UndoActionAsync(IProjectAction action)
        {
            if (CurrentProject == null) return;

            try
            {
                IsActionRunning = true;
                await action.UndoAsync(CurrentProject, UiDispatcherQueue!);

                // update undo/redo
                RedoStack.Push(action);
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (ActionFailedAndRolledBackException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the actions couldn't be completed"
                }));

                // update undo stack
                UndoStack.Push(action);
                OnPropertyChanged(nameof(CanUndo));
            }
            catch (Exception)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and the project needs to be closed"
                }));
                await TryCloseProjectAsync(ignoreUnsavedChanges: true);
            }
            finally
            {
                IsActionRunning = false;
            }
        }

        public async Task TryUndoAsync(IProjectAction? upUntil = null)
        {
            if (!CanUndo) return;
            if (upUntil != null && !UndoStack.Contains(upUntil)) return;

            TaskCompletionSource process = new();

            if (upUntil == null)
                upUntil = UndoStack.Peek();
            else if (upUntil != UndoStack.Peek())
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            while (UndoStack.TryPeek(out IProjectAction? action) && action != upUntil)
            {
                await UndoActionAsync(UndoStack.Pop());
            }
            if (UndoStack.Count > 0) await UndoActionAsync(UndoStack.Pop());

            process.TrySetResult();
        }

        public async Task TryRedoAsync(IProjectAction? upUntil = null)
        {
            if (!CanRedo) return;
            if (upUntil != null && !RedoStack.Contains(upUntil)) return;

            TaskCompletionSource process = new();

            if (upUntil == null)
                upUntil = RedoStack.Peek();
            else if (upUntil != RedoStack.Peek())
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            while (RedoStack.TryPeek(out IProjectAction? action) && action != upUntil)
            {
                await InternalApplyActionAsync(RedoStack.Pop(), true);
            }
            if (RedoStack.Count > 0) await InternalApplyActionAsync(RedoStack.Pop(), true);

            process.TrySetResult();
        }

        public async Task<bool> ConvertProjectAsync(TargetFormat targetFormat, DispatcherQueue uiDispatcherQueue)
        {
            if (CurrentProject == null) return false;
            IsActionRunning = true;

            try
            {
                // collect page data for new project
                IProjectCreationData? creationData = null;
                switch (targetFormat)
                {
                    case TargetFormat.PDF:
                        // get file name and target folder from first page
                        ImagePage imagePage = (ImagePage)CurrentProject.Pages.First(x => x is ImagePage);

                        creationData = new PdfProjectCreationData(CurrentProject.Pages, imagePage.FileNameInfo!.DesiredName, imagePage.TargetFolder, CurrentProject.InitialScanOptions);
                        break;
                    case TargetFormat.JPG:
                    case TargetFormat.PNG:
                    case TargetFormat.BMP:
                    case TargetFormat.TIFF:
                    case TargetFormat.RAW:
                        if (CurrentProject is ImageProject imageProject)
                            creationData = new ImageProjectCreationData(CurrentProject.Pages, targetFormat, null, CurrentProject.InitialScanOptions);
                        else if (CurrentProject is PdfProject pdfProject)
                            creationData = new ImageProjectCreationData(CurrentProject.Pages, targetFormat, pdfProject.FileNameInfo.DesiredName, CurrentProject.InitialScanOptions);
                        break;
                    default:
                        throw new ArgumentException("Can't convert project to " + targetFormat.ToString());
                }

                if (creationData == null)
                    return false;

                // close project and preserve files
                if (!await TryCloseProjectAsync(preserveSourceFilesInIncomingFolder: true))
                    return false;

                // create new project from preserved files
                Task createProjectTask = TryCreateProjectAsync(creationData, uiDispatcherQueue);
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(createProjectTask));
                await createProjectTask;
            }
            finally
            {
                IsActionRunning = false;
            }

            return true;
        }

        private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsService.SettingAutoSave):
                    ResetAutoSaveTimer();
                    break;
            }
        }

        private void ResetAutoSaveTimer()
        {
            // TODO: ensure auto save continues after exception
            if (SettingsService?.SettingAutoSave == true)
            {
                TimerElapsedHandler? handler = null;
                handler = new TimerElapsedHandler(async (source) =>
                {
                    if (SettingsService?.SettingAutoSave == false)
                        return;

                    if (CurrentProject != null && !CurrentProject.IsSaved)
                    {
                        await CurrentProject.SaveAsync(false, UiDispatcherQueue);
                    }
                    autoSaveTimer = ThreadPoolTimer.CreateTimer(handler, TimeSpan.FromSeconds(5));
                });
                autoSaveTimer?.Cancel();
                autoSaveTimer = ThreadPoolTimer.CreateTimer(handler, TimeSpan.FromSeconds(5));
            }
            else
            {
                autoSaveTimer?.Cancel();
                autoSaveTimer = null;
            }
        }

        private void CurrentProject_PagesAdded(object? sender, EventArgs e)
        {
            // automatically select last page when pages are added
            if (sender is ProjectBase project && project.Pages is ObservableCollection<IProjectPage> pages)
            {
                if (pages.Count == 0) return;
                SelectedPage = pages[pages.Count - 1];
            }

            OnPropertyChanged(nameof(TotalNumberOfPages));
        }

        private void CurrentProject_PagesRemoved(object? sender, EventArgs e)
        {
            // update selection accordingly
            if (SelectedPages != null)
            {
                for (int i = 0; i < SelectedPages.Count; i++)
                {
                    if (CurrentProject?.Pages.Contains(SelectedPages[i]) == false)
                    {
                        SelectedPages.RemoveAt(i);
                        i--;
                    }
                }
            }
            else if (SelectedPage != null && CurrentProject?.Pages.Contains(SelectedPage) == false)
            {
                SelectedPage = CurrentProject?.Pages.FirstOrDefault();
            }

            OnPropertyChanged(nameof(TotalNumberOfPages));
        }

        private void CurrentProject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProjectBase.IsSaving):
                case nameof(ProjectBase.IsSaved):
                    OnPropertyChanged(nameof(CanSaveProject));
                    break;
            }
        }

        private void SelectedPages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SelectedPagesCount = SelectedPages.Count;
        }

        private string GetFriendlyCurrentScanState()
        {
            return CurrentScanState switch
            {
                ScanState.Scanning => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressScanning),
                ScanState.AutomaticRotation => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressAutomaticRotation),
                ScanState.GeneratingPDF => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressPdfGeneration),
                ScanState.Processing => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressProcessing),
                ScanState.Saving => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressSaving),
                _ => "",
            };
        }
    }
}
