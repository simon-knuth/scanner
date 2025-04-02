using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Scanner.AppWindows;
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

namespace Scanner.ViewModels
{
    partial class ShellViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        private IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        public ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Events
        public event EventHandler<TaskCompletionSource<bool>> SaveChangesDialogRequested;
        public event EventHandler<Tuple<TaskCompletionSource<SaveOptions?>, ScanOptions, Project?>> SaveFileDialogRequested;
        public event EventHandler<TaskCompletionSource> SaveInProgressDialogRequested;
        public event EventHandler<Notification> ShowNotificationRequested;
        #endregion

        #region Commands
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
        public AsyncRelayCommand TryUndoAsyncCommand => new AsyncRelayCommand(TryUndoAsync);
        public AsyncRelayCommand TryRedoAsyncCommand => new AsyncRelayCommand(TryRedoAsync);
        public AsyncRelayCommand TrySaveAsyncCommand => new AsyncRelayCommand(TrySaveAsync);
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private Project? currentProject;
        public Project? CurrentProject
        {
            get => currentProject;
            set
            {
                if (currentProject != null)
                {
                    currentProject.PropertyChanged -= Project_PropertyChanged;
                }

                if (SetProperty(ref currentProject, value))
                {
                    OnPropertyChanged(nameof(CanStartNewProject));
                    OnPropertyChanged(nameof(CanSaveProject));

                    if (value != null)
                    {
                        value.PropertyChanged += Project_PropertyChanged;
                    }
                }
            }
        }

        public bool CanStartNewProject => CurrentProject != null && !ProjectService.IsProcessRunning;
        public bool CanSaveProject => CurrentProject != null && !CurrentProject.IsSaved && !ProjectService.IsProcessRunning;

        public bool CanUndo => ProjectService.CanUndo && !ProjectService.IsProcessRunning;
        public bool CanRedo => ProjectService.CanRedo && !ProjectService.IsProcessRunning;

        private TaskCompletionSource viewLoading = new();
        private DispatcherQueue? viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellViewModel()
        {
            _ = ScannerDiscoveryService.InitializeSearchAsync();

            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;

            Messenger.Register<ShowSaveChangesDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowSaveChangesDialogAsync());
            });
            Messenger.Register<ShowSaveFileDialogMessage>(this, (r, m) =>
            {
                m.Reply(ShowSaveFileDialogAsync(m.ScanOptions, m.Project));
            });
            Messenger.Register<ShowNotificationMessage>(this, (r, m) =>
            {
                ShowNotificationRequested?.Invoke(this, m.Notification);
            });
            Messenger.Register<ShowSettingsMessage>(this, (r, m) =>
            {
                ShowSettings();
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
                case nameof(IProjectService.IsProcessRunning):
                    OnPropertyChanged(nameof(CanStartNewProject));
                    OnPropertyChanged(nameof(CanSaveProject));
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

        private async Task<SaveOptions?> ShowSaveFileDialogAsync(ScanOptions scanOptions, Project? project)
        {
            TaskCompletionSource<SaveOptions?> result = new();
            SaveFileDialogRequested?.Invoke(this, new(result, scanOptions, project));
            return await result.Task;
        }

        private async Task ShowSaveInProgressDialogAsync()
        {
            TaskCompletionSource result = new();
            SaveInProgressDialogRequested?.Invoke(this, result);
            await result.Task;
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

        private async Task TrySaveAsync()
        {
            if (CurrentProject == null) return;
            await CurrentProject.SaveAsync(viewDispatcherQueue);
        }

        private async Task TryCloseProjectAsync()
        {
            await ProjectService.TryCloseProjectAsync();
        }

        private async Task TryUndoAsync()
        {
            await ProjectService.TryUndoAsync();
        }

        private async Task TryRedoAsync()
        {
            await ProjectService.TryRedoAsync();
        }

        private void ShowSettings()
        {
            ((App)Application.Current).ShowSettings();
        }

        private void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Project.IsSaved):
                    OnPropertyChanged(nameof(CanSaveProject));
                    break;
            }
        }
    }
}