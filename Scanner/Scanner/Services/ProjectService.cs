using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using CommunityToolkit.Mvvm.DependencyInjection;
using Windows.Devices.Scanners;
using Scanner.Models;
using Windows.Storage;
using Scanner.Messages;

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
                        currentProject.PropertyChanged -= CurrentProject_PropertyChanged;
                    }

                    if (SetProperty(ref currentProject, value))
                    {
                        OnPropertyChanged(nameof(CanSelectPreviousPage));
                        OnPropertyChanged(nameof(CanSelectNextPage));
                    }

                    if (value != null)
                    {
                        value.PropertyChanged += CurrentProject_PropertyChanged;
                    }
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
        [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
        private IProjectPage? selectedPage;

        private bool isProcessRunning;
        public bool IsProcessRunning
        {
            get => isProcessRunning;
            private set
            {
                if (SetProperty(ref isProcessRunning, value))
                {
                    OnPropertyChanged(nameof(CanSelectPreviousPage));
                    OnPropertyChanged(nameof(CanSelectNextPage));
                }
            }
        }

        private bool isScanProcessRunning;
        public bool IsScanProcessRunning
        {
            get => isScanProcessRunning;
            private set
            {
                SetProperty(ref isScanProcessRunning, value);
            }
        }

        // TODO: Update properties if selected page is moved
        public bool CanSelectPreviousPage => !IsProcessRunning && CurrentProject != null && SelectedPage != null && SelectedPage.Index > 0;
        public bool CanSelectNextPage => !IsProcessRunning && CurrentProject != null && SelectedPage != null && SelectedPage.Index < CurrentProject.Pages.Count - 1;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectService()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task TryCreateProjectAsync(ScanOptions scanOptions)
        {
            // TODO: catch exceptions and notify user
            IsProcessRunning = IsScanProcessRunning = true;

            // scan
            IList<StorageFile> files = await scanOptions.Scanner.GetScanAsync(AppDataService.IncomingFolder);

            // create project
            CurrentProject = await Project.CreateAsync(files, scanOptions.TargetFormat);

            IsProcessRunning = IsScanProcessRunning = false;
        }

        private void CurrentProject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            
        }

        public async Task<bool> TrySaveProjectAsync()
        {
            if (CurrentProject == null) return true;

            // handle unsaved changes
            if (!CurrentProject.IsSaved && await Messenger.Send(new ShowSaveChangesDialogMessage()).Response == false)
            {
                // changes couldn't be saved
                return false;
            }

            return true;
        }

        public async Task<bool> TryCloseProjectAsync()
        {
            if (CurrentProject == null) return true;

            // handle unsaved changes
            if (await TrySaveProjectAsync() == false)
            {
                return false;
            }

            // close project
            CurrentProject = null;
            return true;
        }

        public void SelectPreviousPage()
        {
            if (CanSelectPreviousPage)
            {
                SelectedPage = CurrentProject.Pages[SelectedPage.Index - 1];
            }
        }

        public void SelectNextPage()
        {
            if (CanSelectNextPage)
            {
                SelectedPage = CurrentProject.Pages[SelectedPage.Index + 1];
            }
        }
    }
}
