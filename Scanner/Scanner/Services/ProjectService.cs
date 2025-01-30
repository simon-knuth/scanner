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
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using CommunityToolkit.Mvvm.DependencyInjection;
using Windows.Devices.Scanners;
using Scanner.Models;
using Windows.Storage;

namespace Scanner.Services
{
    internal partial class ProjectService : IProjectService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Events
        public event EventHandler<Project> ProjectChanged;
        public event EventHandler<bool> IsProcessRunningChanged;
        public event EventHandler<bool> IsScanProcessRunningChanged;
        #endregion

        private Project currentProject;
        public Project CurrentProject
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

                    currentProject = value;
                    ProjectChanged?.Invoke(this, value);

                    if (value != null)
                    {
                        value.PropertyChanged += CurrentProject_PropertyChanged;
                    }
                }
            }
        }

        private bool isProcessRunning;
        public bool IsProcessRunning
        {
            get => isProcessRunning;
            private set
            {
                if (isProcessRunning != value)
                {
                    isProcessRunning = value;
                    IsProcessRunningChanged?.Invoke(this, value);
                }
            }
        }

        private bool isScanProcessRunning;
        public bool IsScanProcessRunning
        {
            get => isScanProcessRunning;
            private set
            {
                if (isScanProcessRunning != value)
                {
                    isScanProcessRunning = value;
                    IsScanProcessRunningChanged?.Invoke(this, value);
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectService()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task TryCreateProjectAsync(IScanningDevice scanner)
        {
            // TODO: catch exceptions and notify user
            IsProcessRunning = IsScanProcessRunning = true;

            // scan
            IList<StorageFile> files = await scanner.GetScanAsync(AppDataService.ReceivedPagesFolder);

            // create project
            CurrentProject = await Project.CreateAsync(files);

            IsProcessRunning = IsScanProcessRunning = false;
        }

        private void CurrentProject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            
        }
    }
}
