using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using static Scanner.Helpers.AppConstants;

using static Utilities;


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
        /// <summary>
        ///     Shows the dialog for rating the app. Opens the Microsoft Store, if something goes wrong.
        /// </summary>
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

        /// <summary>
        ///     Converts the <paramref name="file"/> to a <see cref="BitmapImage"/>.
        /// </summary>
        /// <remarks>
        ///     Partially runs on the UI thread.
        /// </remarks>
        public async Task<BitmapImage> GenerateBitmapFromFileAsync(StorageFile file)
        {
            BitmapImage bmp = null;
            int attempt = 0;

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, async () =>
            {
                using (IRandomAccessStream sourceStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    switch (ConvertFormatStringToImageScannerFormat(file.FileType))
                    {
                        case ImageScannerFormat.Jpeg:
                        case ImageScannerFormat.Png:
                        case ImageScannerFormat.Tiff:
                        case ImageScannerFormat.DeviceIndependentBitmap:
                            while (attempt != -1)
                            {
                                try
                                {
                                    bmp = new BitmapImage();
                                    await bmp.SetSourceAsync(sourceStream);
                                    attempt = -1;
                                }
                                catch (Exception e)
                                {
                                    if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating bitmap of image.", e);

                                    LogService.Log.Warning(e, "Opening the file stream of image failed, retrying in 500ms.");
                                    await Task.Delay(500);
                                    attempt++;
                                }
                            }
                            break;

                        case ImageScannerFormat.Pdf:
                            throw new NotImplementedException("Can not generate bitmap from PDF.");

                        case ImageScannerFormat.Xps:
                        case ImageScannerFormat.OpenXps:
                            throw new NotImplementedException("Can not generate bitmap from (O)XPS.");

                        default:
                            throw new ApplicationException("Could not determine file type for generating a bitmap.");
                    }
                }
            });

            return bmp;
        }
    }
}
