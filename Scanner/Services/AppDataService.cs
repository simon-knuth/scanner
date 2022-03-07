using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Services
{
    internal class AppDataService : IAppDataService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        private const string FolderReceivedPagesName = "ReceivedPages";
        private const string FolderConversionName = "Conversion";
        private const string FolderWithoutRotationName = "WithoutRotation";
        private const string FolderPreviewName = "Preview";

        private StorageFolder _FolderTemp;
        public StorageFolder FolderTemp
        {
            get => _FolderTemp;
        }

        private StorageFolder _FolderReceivedPages;
        public StorageFolder FolderReceivedPages
        {
            get => _FolderReceivedPages;
        }

        private StorageFolder _FolderConversion;
        public StorageFolder FolderConversion
        {
            get => _FolderConversion;
        }

        private StorageFolder _FolderWithoutRotation;
        public StorageFolder FolderWithoutRotation
        {
            get => _FolderWithoutRotation;
        }

        private StorageFolder _FolderPreview;
        public StorageFolder FolderPreview
        {
            get => _FolderPreview;
        }
        

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AppDataService()
        {
            Task.Run(async () => await Initialize());
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Initializes the temp directory by first cleaning it up and then creating the necessary folders.
        /// </summary>
        public async Task Initialize()
        {
            LogService?.Log.Information("AppDataService: Initialize");

            // clean up temp folder
            try
            {
                _FolderTemp = ApplicationData.Current.TemporaryFolder;

                IReadOnlyList<StorageFile> files = await FolderTemp.GetFilesAsync();
                foreach (StorageFile file in files)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't clean up temp folder.");
                throw;
            }

            // attempt to actively delete folders first, replacing is not terribly reliable
            try
            {
                StorageFolder folder = await FolderTemp.GetFolderAsync(FolderConversionName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'Conversion' in temp folder failed."); }

            try
            {
                StorageFolder folder = await FolderTemp.GetFolderAsync(FolderReceivedPagesName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'ReceivedPages' in temp folder failed."); }

            try
            {
                StorageFolder folder = await FolderTemp.GetFolderAsync(FolderWithoutRotationName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'WithoutRotation' in temp folder failed."); }

            try
            {
                StorageFolder folder = await FolderTemp.GetFolderAsync(FolderPreviewName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'Preview' in temp folder failed."); }

            // replace/create folders
            try
            {
                _FolderConversion = await FolderTemp.CreateFolderAsync(FolderConversionName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'Conversion' in temp folder.");
                throw;
            }

            try
            {
                _FolderReceivedPages = await FolderTemp.CreateFolderAsync(FolderReceivedPagesName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'ReceivedPages' in temp folder.");
                throw;
            }

            try
            {
                _FolderWithoutRotation = await FolderTemp.CreateFolderAsync(FolderWithoutRotationName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'WithoutRotation' in temp folder.");
                throw;
            }

            try
            {
                _FolderPreview = await FolderTemp.CreateFolderAsync(FolderPreviewName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'Preview' in temp folder.");
                throw;
            }

            LogService?.Log.Information("Initialized temp folder");
        }

        /// <summary>
        ///     Removes all files from the <see cref="FolderReceivedPages"/>.
        /// </summary>
        public async Task EmptyReceivedPagesFolderAsync()
        {
            var files = await FolderReceivedPages.GetFilesAsync();

            foreach (StorageFile file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }
    }
}
