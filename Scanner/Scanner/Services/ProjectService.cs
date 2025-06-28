using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System.Threading;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.Services
{
    internal partial class ProjectService : ObservableRecipient, IProjectService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
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
                        OnPropertyChanged(nameof(CanSaveAsProject));
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
        [NotifyPropertyChangedFor(nameof(CanSaveAsProject))]
        private bool isActionRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
        [NotifyPropertyChangedFor(nameof(IsProcessRunningOrEditing))]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        [NotifyPropertyChangedFor(nameof(CanSaveProject))]
        [NotifyPropertyChangedFor(nameof(CanSaveAsProject))]
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
                OnPropertyChanged(nameof(CanSaveAsProject));
            }
        }

        private ScanState currentScanState;
        public ScanState CurrentScanState
        {
            get => currentScanState;
            private set
            {
                SetProperty(ref currentScanState, value);
            }
        }


        // TODO: Update properties if selected page is moved
        public bool CanSelectPreviousPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index > 0;
        public bool CanSelectNextPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index < CurrentProject.Pages.Count - 1;

        public Stack<IProjectAction> UndoStack { get; private set; } = new();
        public Stack<IProjectAction> RedoStack { get; private set; } = new();
        public bool CanUndo => UndoStack.Count > 0;
        public bool CanRedo => RedoStack.Count > 0;

        public bool CanSaveProject => CurrentProject != null && !CurrentProject.IsSaving && !CurrentProject.IsSaved && !IsProcessRunning;
        public bool CanSaveAsProject => CurrentProject != null && !CurrentProject.IsSaving && !IsProcessRunning;

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
        public async Task TryCreateProjectAsync(ScanOptions scanOptions)
        {
            try
            {
                CurrentScanState = ScanState.Scanning;

                // close project
                if (await TryCloseProjectAsync() == false) return;

                // get save options
                IsActionRunning = true;
                SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(UiDispatcherQueue!, ((App)Application.Current).MainWindow, scanOptions, CurrentProject);
                if (saveOptions == null) return;

                // scan
                IsScanProcessRunning = true;
                await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
                IList<StorageFile> files = await scanOptions.Scanner.GetScanAsync(AppDataService.IncomingFolder);

                if (files.Count == 0)
                {
                    return;
                }

                // create project
                CurrentScanState = ScanState.Processing;
                switch (scanOptions.TargetFormat)
                {
                    case TargetFormat.PDF:
                        CurrentProject = await PdfProject.CreateAsync(files, scanOptions.TargetFormat, saveOptions.FileName, saveOptions.TargetFolder, false, GetBaseFilterForScanOptions(scanOptions), GetFilterForScanOptions(scanOptions));
                        break;
                    case TargetFormat.JPG:
                    case TargetFormat.PNG:
                    case TargetFormat.BMP:
                    case TargetFormat.TIFF:
                    case TargetFormat.RAW:
                        CurrentProject = await ImageProject.CreateAsync(files, scanOptions.TargetFormat, saveOptions.FileName, saveOptions.TargetFolder, false, GetBaseFilterForScanOptions(scanOptions), GetFilterForScanOptions(scanOptions));
                        break;
                    default:
                        throw new ArgumentException($"Can't create project for format {scanOptions.TargetFormat}");
                }

                // auto rotate
                if (SettingsService.SettingAutoRotate)
                {
                    CurrentScanState = ScanState.AutomaticRotation;

                    Dictionary<IProjectPage, RotationIntent> instructions = new();
                    foreach (IProjectPage page in CurrentProject.Pages)
                    {
                        instructions.Add(page, RotationIntent.Automatic);
                    }

                    await CurrentProject.RotatePagesAsync(instructions, AppDataService.ProjectFolder);
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
                    await CurrentProject.SaveAsync(UiDispatcherQueue!);
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
                    saveOptions = await SaveLocationService.GetSaveOptionsAsync(UiDispatcherQueue!, ((App)Application.Current).MainWindow, scanOptions, CurrentProject);
                    if (saveOptions == null) return;
                }

                // scan
                IsScanProcessRunning = true;
                await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
                IList<StorageFile> files = await scanOptions.Scanner.GetScanAsync(AppDataService.IncomingFolder);
                IsScanProcessRunning = false;

                // automatic rotation
                if (SettingsService.SettingAutoRotate)
                {
                    CurrentScanState = ScanState.AutomaticRotation;

                    Dictionary<StorageFile, RotationIntent> instructions = new();
                    foreach (StorageFile file in files)
                    {
                        instructions.Add(file, RotationIntent.Automatic);
                    }

                    await ProjectBase.RotateFilesAsync(instructions, true, AppDataService.ProjectFolder);
                }

                // add files
                CurrentScanState = ScanState.Processing;
                List<ProjectFileInsertion> insertions = new();
                for (int i = 0; i < files.Count; i++)
                {
                    insertions.Add(new ProjectFileInsertion(files[i], CurrentProject.Pages.Count + i, saveOptions?.FileName, saveOptions?.TargetFolder, GetBaseFilterForScanOptions(scanOptions), GetFilterForScanOptions(scanOptions)));
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

        private static ImageFilter GetFilterForScanOptions(ScanOptions scanOptions)
        {
            switch (scanOptions.ColorMode)
            {
                case ScannerColorMode.None:
                case ScannerColorMode.Color:
                case ScannerColorMode.Automatic:
                    return ImageFilter.None;
                case ScannerColorMode.Grayscale:
                    return ImageFilter.Grayscale;
                case ScannerColorMode.Monochrome:
                    return ImageFilter.Monochrome;
                default:
                    throw new ArgumentException("Failed to determine page's Filter for given configuration");
            }
        }

        private static ImageFilter GetBaseFilterForScanOptions(ScanOptions scanOptions)
        {
            switch (scanOptions.SourceMode)
            {
                case ScannerSource.Auto:
                    return ImageFilter.None;
                case ScannerSource.Flatbed:
                    if (scanOptions.Scanner.IsFlatbedColorAllowed)
                        return ImageFilter.None;
                    else if (scanOptions.Scanner.IsFlatbedGrayscaleAllowed)
                        return ImageFilter.Grayscale;
                    else
                        return ImageFilter.Monochrome;
                case ScannerSource.Feeder:
                    if (scanOptions.Scanner.IsFeederColorAllowed)
                        return ImageFilter.None;
                    else if (scanOptions.Scanner.IsFeederGrayscaleAllowed)
                        return ImageFilter.Grayscale;
                    else
                        return ImageFilter.Monochrome;
                case ScannerSource.None:
                default:
                    throw new ArgumentException("Failed to determine page's BaseFilter for given configuration");
            }
        }

        public async Task<bool> TryDeleteProjectAsync()
        {
            if (CurrentProject == null) return true;
            IsActionRunning = true;

            try
            {
                // get confirmation
                if (await Messenger.Send(new ShowProjectDeletionDialogMessage(CurrentProject)).Response == false) return false;

                // close project
                await TryCloseProjectAsync(true);
            }
            catch (ProjectException)
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
                await TryCloseProjectAsync(true);
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
            catch (ProjectException)
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
            catch (ProjectException)
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
            catch (ProjectException)
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
            catch (ProjectException)
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

        public async Task<bool> TryCloseProjectAsync(bool ignoreUnsavedChanges = false)
        {
            if (CurrentProject == null) return true;

            // handle unsaved changes
            if (!ignoreUnsavedChanges && await TrySaveProjectAsync() == false)
            {
                return false;
            }

            // close project
            CurrentProject = null;
            SelectedPage = null;
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.PreviewFolder);

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
                IsActionRunning = true;
                bool changesMade = await action.ExecuteAsync(CurrentProject, UiDispatcherQueue!);

                if (changesMade)
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
            catch (ProjectException)
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
                await TryCloseProjectAsync(true);
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
            catch (ProjectException)
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
                await TryCloseProjectAsync(true);
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

            if (upUntil == null) upUntil = UndoStack.Peek();

            while (UndoStack.TryPeek(out IProjectAction? action) && action != upUntil)
            {
                await UndoActionAsync(UndoStack.Pop());
            }
            if (UndoStack.Count > 0) await UndoActionAsync(UndoStack.Pop());
        }

        public async Task TryRedoAsync(IProjectAction? upUntil = null)
        {
            if (!CanRedo) return;
            if (upUntil != null && !RedoStack.Contains(upUntil)) return;

            if (upUntil == null) upUntil = RedoStack.Peek();

            while (RedoStack.TryPeek(out IProjectAction? action) && action != upUntil)
            {
                await InternalApplyActionAsync(RedoStack.Pop(), true);
            }
            if (RedoStack.Count > 0) await InternalApplyActionAsync(RedoStack.Pop(), true);
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
                    if (CurrentProject != null && !CurrentProject.IsSaved)
                    {
                        await CurrentProject.SaveAsync(UiDispatcherQueue);
                    }
                    autoSaveTimer = ThreadPoolTimer.CreateTimer(handler, TimeSpan.FromSeconds(5));
                });
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
        }

        private void CurrentProject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProjectBase.IsSaving):
                case nameof(ProjectBase.IsSaved):
                    OnPropertyChanged(nameof(CanSaveProject));
                    OnPropertyChanged(nameof(CanSaveAsProject));
                    break;
            }
        }

        private void SelectedPages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SelectedPagesCount = SelectedPages.Count;
        }
    }
}
