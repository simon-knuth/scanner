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

        private const string IncomingFolderName = "Incoming";
        private const string ProjectFolderName = "Project";
        private const string ChangesFolderName = "Changes";
        private const string PreviewFolderName = "Preview";
        private const string PdfOutputFolderName = "PdfOutput";
        private const string PreviewScanFolderName = "PreviewScan";
        private const string UndoFolderName = "Undo";
        private const string RedoFolderName = "Redo";

        public StorageFolder TempFolder { get; private set; }
        public StorageFolder IncomingFolder { get; private set; }
        public StorageFolder ProjectFolder { get; private set; }
        public StorageFolder ChangesFolder { get; private set; }
        public StorageFolder PreviewFolder { get; private set; }
        public StorageFolder EffectsAppliedForPdfFolder { get; private set; }
        public StorageFolder PdfOutputFolder { get; private set; }
        public StorageFolder PreviewScanFolder { get; private set; }
        public StorageFolder UndoFolder { get; private set; }
        public StorageFolder RedoFolder { get; private set; }


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
            LogService?.Log.Information("Initializing");

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
                LogService?.Log.Error(exc, "Failed to clean up temp folder");
                throw;
            }

            // replace folders
            IncomingFolder = await CreateOrReplaceFolderAsync(IncomingFolderName);
            ProjectFolder = await CreateOrReplaceFolderAsync(ProjectFolderName);
            ChangesFolder = await CreateOrReplaceFolderAsync(ChangesFolderName);
            PreviewFolder = await CreateOrReplaceFolderAsync(PreviewFolderName);
            PdfOutputFolder = await CreateOrReplaceFolderAsync(PdfOutputFolderName);
            PreviewScanFolder = await CreateOrReplaceFolderAsync(PreviewScanFolderName);
            UndoFolder = await CreateOrReplaceFolderAsync(UndoFolderName);
            RedoFolder = await CreateOrReplaceFolderAsync(RedoFolderName);

            LogService?.Log.Information("Initialized temp folder");
        }

        private async Task<StorageFolder> CreateOrReplaceFolderAsync(string name)
        {
            try
            {
                return await TempFolder.CreateFolderAsync(name, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, $"Failed to replace folder '{name}' in temp folder");
                throw;
            }
        }

        /// <summary>
        ///     Removes all files from the given <paramref name="folder"/>.
        /// </summary>
        public async Task EmptyFolderAsync(StorageFolder folder)
        {
            await CreateOrReplaceFolderAsync(folder.Name);
        }

        public string GetUriForAppDataFolder(StorageFolder folder, string fileName = "")
        {
            if (folder == TempFolder)
            {
                return Path.Combine("ms-appdata:///temp/", fileName);
            }
            else if (folder == IncomingFolder)
            {
                return Path.Combine("ms-appdata:///temp/", IncomingFolderName, fileName);
            }
            else if (folder == ProjectFolder)
            {
                return Path.Combine("ms-appdata:///temp/", ProjectFolderName, fileName);
            }
            else if (folder == ChangesFolder)
            {
                return Path.Combine("ms-appdata:///temp/", ChangesFolderName, fileName);
            }
            else if (folder == PreviewFolder)
            {
                return Path.Combine("ms-appdata:///temp/", PreviewFolderName, fileName);
            }
            else if (folder == PdfOutputFolder)
            {
                return Path.Combine("ms-appdata:///temp/", PdfOutputFolderName, fileName);
            }
            else if (folder == PreviewScanFolder)
            {
                return Path.Combine("ms-appdata:///temp/", PreviewScanFolderName, fileName);
            }
            else
            {
                throw new ArgumentException("Failed to get URI for unknown folder");
            }
        }
    }
}
