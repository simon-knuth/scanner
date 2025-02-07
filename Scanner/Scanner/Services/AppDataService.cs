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
using Serilog.Sinks.File;
using Serilog;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Serilog.Formatting.Compact;
using Serilog.Exceptions;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Scanner.Services
{
    internal class AppDataService : IAppDataService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private const string ReceivedPagesFolderName = "ReceivedPages";
        private const string ProjectFolderName = "Project";

        public StorageFolder TempFolder { get; private set; }
        public StorageFolder ReceivedPagesFolder { get; private set; }
        public StorageFolder ProjectFolder { get; private set; }



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AppDataService()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Initializes the temp directory by first cleaning it up and then creating the necessary folders.
        /// </summary>
        public async Task InitializeAsync()
        {
            LogService?.Log.Information("AppDataService - Initializing");

            // clean up temp folder
            try
            {
                TempFolder = ApplicationData.Current.TemporaryFolder;

                IReadOnlyList<StorageFile> files = await TempFolder.GetFilesAsync();
                foreach (StorageFile file in files)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "AppDataService - Failed to clean up temp folder");
                throw;
            }

            // replace folders
            ReceivedPagesFolder = await CreateOrReplaceFolderAsync(ReceivedPagesFolderName);
            ProjectFolder = await CreateOrReplaceFolderAsync(ProjectFolderName);

            LogService?.Log.Information("AppDataService - Initialized temp folder");
        }

        private async Task<StorageFolder> CreateOrReplaceFolderAsync(string name)
        {
            try
            {
                return await TempFolder.CreateFolderAsync(name, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, $"AppDataService - Failed to replace folder '{name}' in temp folder");
                throw;
            }
        }

        /// <summary>
        ///     Removes all files from the <see cref="ReceivedPagesFolder"/>.
        /// </summary>
        public async Task EmptyReceivedPagesFolderAsync()
        {
            ReceivedPagesFolder = await CreateOrReplaceFolderAsync(ReceivedPagesFolderName);
        }

        /// <summary>
        ///     Removes all files from the <see cref="ProjectFolder"/>.
        /// </summary>
        public async Task EmptyProjectFolderAsync()
        {
            ProjectFolder = await CreateOrReplaceFolderAsync(ProjectFolderName);
        }

        public string GetUriForAppDataFolder(StorageFolder folder, string fileName = "")
        {
            if (folder == TempFolder)
            {
                return Path.Combine("ms-appdata:///temp/", fileName);
            }
            else if (folder == ReceivedPagesFolder)
            {
                return Path.Combine("ms-appdata:///temp/", ReceivedPagesFolderName, fileName);
            }
            else if (folder == ProjectFolder)
            {
                return Path.Combine("ms-appdata:///temp/", ProjectFolderName, fileName);
            }
            else
            {
                throw new ArgumentException("Failed to get URI for unknown folder");
            }
        }
    }
}
