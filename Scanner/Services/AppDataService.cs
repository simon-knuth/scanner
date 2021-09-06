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
        public async Task Initialize()
        {
            // clean up temp folder
            StorageFolder folderTemp = ApplicationData.Current.TemporaryFolder;

            IReadOnlyList<StorageFile> files = await folderTemp.GetFilesAsync();
            foreach (StorageFile file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            // attempt to actively delete folders first, replacing is not terribly reliable
            try
            {
                StorageFolder folder = await folderTemp.GetFolderAsync(FolderConversionName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'Conversion' in temp folder failed."); }

            try
            {
                StorageFolder folder = await folderTemp.GetFolderAsync(FolderReceivedPagesName);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception exc) { LogService?.Log.Error(exc, "Actively deleting folder 'ReceivedPages' in temp folder failed."); }

            // replace/create folders
            try
            {
                _FolderConversion = await folderTemp.CreateFolderAsync(FolderConversionName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'Conversion' in temp folder.");
                throw;
            }

            try
            {
                _FolderReceivedPages = await folderTemp.CreateFolderAsync(FolderReceivedPagesName, CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't create/replace folder 'ReceivedPages' in temp folder.");
                throw;
            }

            LogService?.Log.Information("Initialized temp folder");
        }
    }
}
