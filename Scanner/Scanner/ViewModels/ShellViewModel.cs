using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
        private IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        private IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
        #endregion

        #region Events
        public event EventHandler<TaskCompletionSource<bool>> SaveChangesDialogRequested;
        #endregion

        #region Commands
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        private Project? currentProject;

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
            }
        }

        private async Task<bool> ShowSaveChangesDialogAsync()
        {
            TaskCompletionSource<bool> result = new();
            SaveChangesDialogRequested?.Invoke(this, result);
            return await result.Task;
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (CurrentProject != null && !CurrentProject.IsSaved)
            {
                // unsaved changes present ~> ask user
                args.Handled = true;
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
}