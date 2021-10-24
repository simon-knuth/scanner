using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;

using static Scanner.Helpers.AppConstants;

namespace Scanner.Services
{
    internal class HelperService : IHelperService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public HelperService()
        {
            
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task ShowRatingDialogAsync()
        {
            try
            {
                LogService?.Log.Information("Displaying rating dialog.");
                StoreContext storeContext = StoreContext.GetDefault();
                await storeContext.RequestRateAndReviewAppAsync();
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "Displaying the rating dialog failed.");
                try { await Launcher.LaunchUriAsync(new Uri(UriStoreRating)); } catch (Exception) { }
            }
        }

        /// <summary>
        ///     Moves the <paramref name="file"/> to the <paramref name="targetFolder"/>. Attempts to name
        ///     it <paramref name="desiredName"/>.
        /// </summary>
        /// <param name="file">The file that's to be moved.</param>
        /// <param name="targetFolder">The folder that the file shall be moved to.</param>
        /// <param name="desiredName">The name that the file should ideally have when finished.</param>
        /// <param name="replaceExisting">Replaces file if true, otherwise asks the OS to generate a unique name.</param>
        /// <returns>The final name of the file.</returns>
        public async Task<string> MoveFileToFolderAsync(StorageFile file, StorageFolder targetFolder, string desiredName, bool replaceExisting)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();

            logService?.Log.Information("Requested to move file to folder. [desiredName={Name}|replaceExisting={Replace}]", desiredName, replaceExisting);
            try
            {
                if (replaceExisting) await file.MoveAsync(targetFolder, desiredName, NameCollisionOption.ReplaceExisting);
                else await file.MoveAsync(targetFolder, desiredName, NameCollisionOption.FailIfExists);
            }
            catch (Exception)
            {
                if (replaceExisting) throw;

                try { await file.MoveAsync(targetFolder, desiredName, NameCollisionOption.GenerateUniqueName); }
                catch (Exception) { throw; }
            }

            return file.Name;
        }

    }
}
