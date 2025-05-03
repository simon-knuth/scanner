using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
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
        public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
        public RelayCommand SelectPreviousPageCommand => new RelayCommand(SelectPreviousPage);
        public RelayCommand SelectNextPageCommand => new RelayCommand(SelectNextPage);
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public AsyncRelayCommand<IProjectPage?> ShowInFileExplorerAsyncCommand => new AsyncRelayCommand<IProjectPage?>(ShowInFileExplorerAsync);
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private Project? currentProject;
        public Project? CurrentProject
        {
            get => currentProject;
            set
            {
                if (currentProject != null && currentProject.FileNameInfo != null) currentProject.FileNameInfo.NameChanged -= FileNameInfo_NameChanged;

                if (SetProperty(ref currentProject, value))
                {
                    OnPropertyChanged(nameof(FileName));

                    if (value != null && value.FileNameInfo != null) value.FileNameInfo.NameChanged += FileNameInfo_NameChanged;
                }
            }
        }

        public string FileName
        {
            get
            {
                if (CurrentProject == null) return string.Empty;

                if (CurrentProject.IsPdf)
                {
                    return Path.GetFileNameWithoutExtension(CurrentProject.FileNameInfo!.DesiredName);
                }
                else if (ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return Path.GetFileNameWithoutExtension(imagePage.FileNameInfo!.DesiredName);
                }
                return string.Empty;
            }
            set
            {
                if (CurrentProject == null) return;

                if (CurrentProject.IsPdf)
                {
                    _ = ProjectService.ApplyActionAsync(new RenameAction(null, value));
                }
                else if (ProjectService.SelectedPage is ImagePage imagePage)
                {
                    _ = ProjectService.ApplyActionAsync(new RenameAction(imagePage, value));
                }
            }
        }

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
                    if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage)
                    {
                        imagePage.FileNameInfo.NameChanged -= FileNameInfo_NameChanged;
                    }
                    break;
            }
        }

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentProject):
                    CurrentProject = ProjectService.CurrentProject;
                    break;
                case nameof(IProjectService.SelectedPage):
                    if (CurrentProject != null && !CurrentProject.IsPdf)
                    {
                        OnPropertyChanged(nameof(FileName));
                    }

                    if (ProjectService.SelectedPage != null && ProjectService.SelectedPage is ImagePage imagePage)
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
            if (CurrentProject.IsPdf)
            {
                await Windows.System.Launcher.LaunchFolderAsync(CurrentProject.TargetFolder);
            }
            else
            {
                // use currently selected page if no page is provided
                if (page == null && ProjectService.SelectedPage != null) page = ProjectService.SelectedPage;

                if (page is not ImagePage imagePage) return;

                await Windows.System.Launcher.LaunchFolderAsync(imagePage.TargetFolder);
            }
        }
    }
}
