using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
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

        public async Task<BitmapEncoder> CreateOptimizedBitmapEncoderAsync(ImageScannerFormat? encoderFormat, IRandomAccessStream stream)
        {
            // get encoder ID
            Guid encoderId;
            switch (encoderFormat)
            {
                case ImageScannerFormat.Jpeg:
                    encoderId = BitmapEncoder.JpegEncoderId;
                    break;
                case ImageScannerFormat.Png:
                    encoderId = BitmapEncoder.PngEncoderId;
                    break;
                case ImageScannerFormat.Tiff:
                    encoderId = BitmapEncoder.TiffEncoderId;
                    break;
                case ImageScannerFormat.DeviceIndependentBitmap:
                    encoderId = BitmapEncoder.BmpEncoderId;
                    break;
                default:
                    throw new ApplicationException($"CreateOptimizedBitmapEncoderAsync received invalid ImageScannerFormat {encoderFormat}");
            }

            // create encoder
            if (encoderFormat == ImageScannerFormat.Jpeg)
            {
                // prevent large JPG size
                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(0.85d, Windows.Foundation.PropertyType.Single);
                propertySet.Add("ImageQuality", qualityValue);

                stream.Size = 0;
                return await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, propertySet);
            }
            else
            {
                return await BitmapEncoder.CreateAsync(encoderId, stream);
            }
        }
    }
}
