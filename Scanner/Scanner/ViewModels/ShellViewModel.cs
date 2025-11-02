using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Scanner.AppWindows;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Resources.Strings;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner.ViewModels
{
    partial class ShellViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
        private ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        private IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        public ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Events
        public event EventHandler<TaskCompletionSource<bool>> SaveChangesDialogRequested;
        public event EventHandler<(TaskCompletionSource<SaveOptions?> Process, ScanOptions ScanOptions, ProjectBase? Project, string? DesiredFileDisplayName)> SaveFileDialogRequested;
        public event EventHandler<(TaskCompletionSource<bool> Process, ProjectBase? Project)> ProjectDeletionDialogRequested;
        public event EventHandler<TaskCompletionSource> SaveInProgressDialogRequested;
        public event EventHandler<(string Title, Task Task)> IndeterminateProgressDialogRequested;
        public event EventHandler DonationDialogRequested;
        public event EventHandler OtherAppsDialogRequested;
        public event EventHandler ScanMergeDialogRequested;
        public event EventHandler<Notification> ShowNotificationRequested;
        #endregion

        #region Commands
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public RelayCommand ShowFeedbackCommand => new RelayCommand(ShowFeedback);
        public RelayCommand ShowDonationDialogCommand => new RelayCommand(ShowDonationDialog);
        public RelayCommand ShowOtherAppsDialogCommand => new RelayCommand(ShowOtherAppsDialog);
        public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
        public AsyncRelayCommand<IProjectAction> TryUndoAsyncCommand => new AsyncRelayCommand<IProjectAction>(TryUndoAsync);
        public AsyncRelayCommand<IProjectAction> TryRedoAsyncCommand => new AsyncRelayCommand<IProjectAction>(TryRedoAsync);
        public AsyncRelayCommand SaveAsyncCommand;
        public AsyncRelayCommand SaveAsAsyncCommand;
        public AsyncRelayCommand SaveAsCurrentPageAsyncCommand;
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartNewProject))]
        private ProjectBase? currentProject;

        public bool CanStartNewProject => CurrentProject != null && !ProjectService.IsProcessRunningOrEditing;

        public bool CanUndo => ProjectService.CanUndo && !ProjectService.IsProcessRunningOrEditing;
        public bool CanRedo => ProjectService.CanRedo && !ProjectService.IsProcessRunningOrEditing;

        private TaskCompletionSource viewLoading = new();
        private DispatcherQueue? viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellViewModel()
        {
            SaveAsyncCommand = new AsyncRelayCommand(SaveAsync);
            SaveAsAsyncCommand = new AsyncRelayCommand(SaveAsAsync);
            SaveAsCurrentPageAsyncCommand = new AsyncRelayCommand(SaveAsCurrentPageAsync);

            _ = ScannerDiscoveryService.InitializeSearchAsync();

            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;

            Messenger.Register<ShowUnsavedChangesDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowSaveChangesDialogAsync());
            });
            Messenger.Register<ShowSaveOptionsDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowSaveFileDialogAsync(m.ScanOptions, m.Project, m.DesiredFileDisplayName));
            });
            Messenger.Register<ShowProjectDeletionDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowProjectDeletionDialogAsync(m.Project));
            });
            Messenger.Register<ShowSaveInProgressDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowSaveInProgressDialogAsync());
            });
            Messenger.Register<ShowIndeterminateProgressDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowIndeterminateProgressDialogAsync(m.Title, m.Process));
            });
            Messenger.Register<ShowDonationDialogMessage>(this, (r, m) =>
            {
                ShowDonationDialog();
            });
            Messenger.Register<ShowNotificationMessage>(this, (r, m) =>
            {
                ShowNotificationRequested?.Invoke(this, m.Notification);
            });
            Messenger.Register<ShowSettingsMessage>(this, (r, m) =>
            {
                ShowSettings();
            });
            Messenger.Register<ShowFeedbackMessage>(this, (r, m) =>
            {
                ShowFeedback();
            });
            Messenger.Register<ShowScanMergeDialogMessage>(this, (r, m) =>
            {
                ShowScanMergeDialog();
            });
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
            viewDispatcherQueue = dispatcherQueue;
            ProjectService.UiDispatcherQueue = dispatcherQueue;
            viewLoading.TrySetResult();

            ((App)Application.Current).MainWindow.Closed += MainWindow_Closed;
        }

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentProject):
                    CurrentProject = ProjectService.CurrentProject;
                    break;
                case nameof(IProjectService.IsProcessRunningOrEditing):
                    OnPropertyChanged(nameof(CanStartNewProject));
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                    break;
                case nameof(IProjectService.CanUndo):
                    OnPropertyChanged(nameof(CanUndo));
                    break;
                case nameof(IProjectService.CanRedo):
                    OnPropertyChanged(nameof(CanRedo));
                    break;
            }
        }

        private async Task<bool> ShowSaveChangesDialogAsync()
        {
            TaskCompletionSource<bool> result = new();
            SaveChangesDialogRequested?.Invoke(this, result);
            return await result.Task;
        }

        private async Task<SaveOptions?> ShowSaveFileDialogAsync(ScanOptions scanOptions, ProjectBase? project, string? desiredFileDisplayName)
        {
            TaskCompletionSource<SaveOptions?> result = new();
            SaveFileDialogRequested?.Invoke(this, new(result, scanOptions, project, desiredFileDisplayName));
            return await result.Task;
        }

        private async Task ShowSaveInProgressDialogAsync()
        {
            TaskCompletionSource result = new();
            SaveInProgressDialogRequested?.Invoke(this, result);
            await result.Task;
        }

        private async Task<bool> ShowProjectDeletionDialogAsync(ProjectBase project)
        {
            TaskCompletionSource<bool> result = new();
            ProjectDeletionDialogRequested?.Invoke(this, (result, project));
            return await result.Task;
        }

        private async Task ShowIndeterminateProgressDialogAsync(string title, Task process)
        {
            if (!ProjectService.IsScanProcessRunning)
                IndeterminateProgressDialogRequested?.Invoke(this, (title, process));
            
            await process;
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (CurrentProject != null && !CurrentProject.IsSaved)
            {
                args.Handled = true;

                // unsaved changes present
                if (SettingsService.SettingAutoSave)
                {
                    // ask user
                    await ShowSaveInProgressDialogAsync();

                    // process result
                    if (CurrentProject.IsSaved)
                    {
                        // changes saved successfully ~> close window for good
                        ((MainWindow)sender).Closed -= MainWindow_Closed;
                        ((MainWindow)sender).Close();
                    }
                }
                else
                {
                    // ask user
                    bool result = await ShowSaveChangesDialogAsync();

                    // process result
                    if (result)
                    {
                        // changes saved or discarded ~> close window for good
                        ((MainWindow)sender).Closed -= MainWindow_Closed;
                        ((MainWindow)sender).Close();
                    }
                }
            }
        }

        private async Task SaveAsync()
        {
            if (CurrentProject == null) return;
            await CurrentProject.SaveAsync(false, viewDispatcherQueue!);
        }

        private async Task SaveAsAsync()
        {
            if (CurrentProject == null) return;
            await CurrentProject.SaveAsync(true, viewDispatcherQueue!);
        }

        private async Task SaveAsCurrentPageAsync()
        {
            if (CurrentProject == null) return;
            if (CurrentProject is not ImageProject imageProject) return;

            if (ProjectService.SelectedPage != null)
                await imageProject.SaveAsSinglePageAsync(ProjectService.SelectedPage, viewDispatcherQueue!);
        }

        private async Task TryCloseProjectAsync()
        {
            await ProjectService.TryCloseProjectAsync();
        }

        private async Task TryUndoAsync(IProjectAction? upUntil = null)
        {
            await ProjectService.TryUndoAsync(upUntil);
        }

        private async Task TryRedoAsync(IProjectAction? upUntil = null)
        {
            await ProjectService.TryRedoAsync(upUntil);
        }

        private void ShowSettings()
        {
            ((App)Application.Current).ShowSettings();
        }

        private void ShowFeedback()
        {
            ((App)Application.Current).ShowFeedback();
        }

        private void ShowDonationDialog()
        {
            DonationDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowOtherAppsDialog()
        {
            OtherAppsDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowScanMergeDialog()
        {
            ScanMergeDialogRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}