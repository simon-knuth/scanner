using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
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
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        private Project? currentProject;
        public Project? CurrentProject
        {
            get => currentProject;
            private set
            {
                if (currentProject != value)
                {
                    if (currentProject != null)
                    {
                        currentProject.PagesAdded -= CurrentProject_PagesAdded;
                    }

                    if (SetProperty(ref currentProject, value))
                    {
                        OnPropertyChanged(nameof(CanSelectPreviousPage));
                        OnPropertyChanged(nameof(CanSelectNextPage));
                    }

                    if (value != null)
                    {
                        currentProject.PagesAdded += CurrentProject_PagesAdded;
                    }
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        private IProjectPage? selectedPage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        private bool isActionRunning;

        public bool IsProcessRunning => IsScanProcessRunning || IsActionRunning;

        private bool isScanProcessRunning;
        public bool IsScanProcessRunning
        {
            get => isScanProcessRunning;
            private set
            {
                SetProperty(ref isScanProcessRunning, value);
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
        public bool CanSelectPreviousPage => !IsProcessRunning && CurrentProject != null && SelectedPage != null && SelectedPage.Index > 0;
        public bool CanSelectNextPage => !IsProcessRunning && CurrentProject != null && SelectedPage != null && SelectedPage.Index < CurrentProject.Pages.Count - 1;

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public DispatcherQueue? UiDispatcherQueue { get; set; }

        private Stack<IProjectAction> undoStack = new();
        private Stack<IProjectAction> redoStack = new();

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
            CurrentScanState = ScanState.Scanning;

            // close project
            if (await TryCloseProjectAsync() == false) return;

            // TODO: catch exceptions and notify user
            IsActionRunning = IsScanProcessRunning = true;

            // scan
            await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
            IList<StorageFile> files = await scanOptions.Scanner.GetScanAsync(AppDataService.IncomingFolder);

            if (files.Count == 0)
            {
                return;
            }

            // create project
            CurrentScanState = ScanState.Processing;
            CurrentProject = await Project.CreateAsync(files, scanOptions.TargetFormat);

            // auto rotate and save if needed
            if (SettingsService.SettingAutoSave)
            {
                // auto rotate
                if (SettingsService.SettingAutoRotate)
                {
                    CurrentScanState = ScanState.AutomaticRotation;

                    Dictionary<IProjectPage, RotationIntent> instructions = new();
                    foreach (IProjectPage page in CurrentProject.Pages)
                    {
                        instructions.Add(page, RotationIntent.Automatic);
                    }

                    await Project.RotatePagesAsync(instructions);
                }

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

            IsActionRunning = IsScanProcessRunning = false;
        }

        public async Task TryScanToProjectAsync(ScanOptions scanOptions)
        {
            CurrentScanState = ScanState.Scanning;

            // TODO: catch exceptions and notify user
            if (CurrentProject == null) return;

            IsActionRunning = IsScanProcessRunning = true;

            // scan
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

                await Project.RotatePagesAsync(instructions, true);
            }

            // add files
            CurrentScanState = ScanState.Processing;
            Dictionary<StorageFile, int> insertions = new();
            for (int i = 0; i < files.Count; i++)
            {
                insertions.Add(files[i], CurrentProject.Pages.Count + i);
            }
            IProjectAction action = new AddFilesAction(insertions);

            await ApplyActionAsync(action);

            IsActionRunning = false;
        }

        public async Task<bool> TrySaveProjectAsync()
        {
            if (CurrentProject == null) return true;

            // handle unsaved changes
            if (!CurrentProject.IsSaved && await Messenger.Send(new ShowSaveChangesDialogMessage()).Response == false)
            {
                // changes couldn't be handled
                return false;
            }

            // changes were handled (saved or discarded)
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
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);

            // update undo/redo stacks
            undoStack.Clear();
            redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

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
                await action.ExecuteAsync(CurrentProject, UiDispatcherQueue!);

                // update undo stack
                undoStack.Push(action);
                OnPropertyChanged(nameof(CanUndo));

                // update redo
                if (!redoing)
                {
                    redoStack.Clear();
                    await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
                }
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (ProjectException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and your changes couldn't be completed"
                }));

                if (redoing)
                {
                    // update redo stack
                    redoStack.Push(action);
                    OnPropertyChanged(nameof(redoStack));
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
                await action.UndoAsync(CurrentProject);

                // update undo/redo
                redoStack.Push(action);
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (ProjectException)
            {
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = "Something went wrong and your changes couldn't be completed"
                }));

                // update undo stack
                undoStack.Push(action);
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

        public async Task TryUndoAsync()
        {
            if (!CanUndo) return;

            await UndoActionAsync(undoStack.Pop());
        }

        public async Task TryRedoAsync()
        {
            if (!CanRedo) return;

            await InternalApplyActionAsync(redoStack.Pop(), true);
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
            if (sender is Project project && project.Pages is ObservableCollection<IProjectPage> pages)
            {
                if (pages.Count == 0) return;
                SelectedPage = pages[pages.Count - 1];
            }
        }
    }
}
