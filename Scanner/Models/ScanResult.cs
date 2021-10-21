using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

using static Globals;
using static Utilities;


namespace Scanner
{
    public class ScanResult : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly IAppDataService AppDataService = Ioc.Default.GetService<IAppDataService>();
        private readonly IAutoRotatorService AutoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();

        private ObservableCollection<ScanResultElement> _Elements = new ObservableCollection<ScanResultElement>();
        public ObservableCollection<ScanResultElement> Elements
        {
            get => _Elements;
        }

        public ImageScannerFormat ScanResultFormat;
        public StorageFile Pdf = null;
        public readonly StorageFolder OriginalTargetFolder;

        public bool IsImage => IsImageFormat(ScanResultFormat);

        private int _NumberOfPages;
        public int NumberOfPages
        {
            get => _NumberOfPages;
            set => SetProperty(ref _NumberOfPages, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ScanResult(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, int futureAccessListIndexStart)
        {
            LogService?.Log.Information("ScanResult constructor [futureAccessListIndexStart={Index}]", futureAccessListIndexStart);
            int futureAccessListIndex = futureAccessListIndexStart;
            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;

                _Elements.Add(new ScanResultElement(file, futureAccessListIndex));
                NumberOfPages = _Elements.Count;

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                futureAccessListIndex += 1;
            }
            ScanResultFormat = (ImageScannerFormat)ConvertFormatStringToImageScannerFormat(_Elements[0].ScanFile.FileType);
            OriginalTargetFolder = targetFolder;
            RefreshItemDescriptors();
            _Elements.CollectionChanged += (x, y) => PagesChanged?.Invoke(this, y);
        }

        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList,
            StorageFolder targetFolder, int futureAccessListIndexStart)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
            IAutoRotatorService autoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();

            logService?.Log.Information("Creating a ScanResult without any conversion from {Num} pages.", fileList.Count);

            // construct ScanResult
            Task[] moveTasks = new Task[fileList.Count];
            for (int i = 0; i < fileList.Count; i++)
            {
                moveTasks[i] = MoveFileToFolderAsync(fileList[i], targetFolder, RemoveNumbering(fileList[i].Name), false);
            }
            await Task.WhenAll(moveTasks);

            ScanResult result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart);

            // set initial name(s)
            if ((bool)settingsService.GetSetting(AppSetting.SettingAppendTime))
            {
                try { await result.SetInitialNamesAsync(); } catch (Exception) { }
            }

            // automatic rotation
            if ((bool)settingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < result.Elements.Count; i++)
                {
                    ScanResultElement element = result.Elements[i];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    if (format != null)
                    {
                        BitmapRotation recommendedRotation = await autoRotatorService.TryGetRecommendedRotationAsync(
                            element.ScanFile, (ImageScannerFormat)format);

                        if (recommendedRotation != BitmapRotation.None)
                        {
                            instructions.Add(new Tuple<int, BitmapRotation>(i, recommendedRotation));
                        }
                    }
                }

                if (instructions.Count > 0)
                {
                    await result.RotateScansAsync(instructions);
                }
            }

            // create previews
            await result.GetImagesAsync();
            logService?.Log.Information("ScanResult created.");
            return result;
        }

        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList,
            StorageFolder targetFolder, ImageScannerFormat targetFormat, int futureAccessListIndexStart)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
            IAutoRotatorService autoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();

            logService?.Log.Information("Creating a ScanResult with conversion from {SourceFormat} to {TargetFormat} from {Num} pages.",
                fileList[0].FileType, targetFormat, fileList.Count);
            
            // construct ScanResult
            ScanResult result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart);
            if (targetFormat == ImageScannerFormat.Pdf)
            {
                string pdfName = fileList[0].DisplayName + ".pdf";
                await PrepareNewConversionFiles(fileList, 0);
                await result.GeneratePDFAsync(pdfName);
            }
            else
            {
                Task[] conversionTasks = new Task[result.NumberOfPages];
                for (int i = 0; i < result.NumberOfPages; i++)
                {
                    conversionTasks[i] = result.ConvertScanAsync(i, targetFormat, targetFolder);
                }
                await Task.WhenAll(conversionTasks);
            }
            result.ScanResultFormat = targetFormat;

            // set initial name(s)
            if ((bool)settingsService.GetSetting(AppSetting.SettingAppendTime))
            {
                try { await result.SetInitialNamesAsync(); } catch (Exception) { }
            }

            // automatic rotation
            if ((bool)settingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < result.Elements.Count; i++)
                {
                    ScanResultElement element = result.Elements[i];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    if (format != null)
                    {
                        BitmapRotation recommendedRotation = await autoRotatorService.TryGetRecommendedRotationAsync(
                            element.ScanFile, (ImageScannerFormat)format);

                        if (recommendedRotation != BitmapRotation.None)
                        {
                            instructions.Add(new Tuple<int, BitmapRotation>(i, recommendedRotation));
                        }
                    }
                }

                if (instructions.Count > 0)
                {
                    await result.RotateScansAsync(instructions);
                }
            }

            // create previews
            await result.GetImagesAsync();

            logService?.Log.Information("ScanResult created.");
            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // EVENTS ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public event NotifyCollectionChangedEventHandler PagesChanged;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets all files of the individual scans in this instance.
        /// </summary>
        public List<StorageFile> GetImageFiles()
        {
            LogService?.Log.Information("All image files of the scan result have been requested.");
            List<StorageFile> files = new List<StorageFile>();
            foreach (ScanResultElement element in _Elements)
            {
                files.Add(element.ScanFile);
            }
            return files;
        }


        /// <summary>
        ///     Gets the file of a single scan in this instance.
        /// </summary>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public StorageFile GetImageFile(int index)
        {
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Image file for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            }
            else return _Elements.ElementAt(index).ScanFile;
        }


        /// <summary>
        ///     Gets the thumbnail of a single scan in this instance.
        /// </summary>
        /// <param name="index">The desired thumbnail's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public BitmapImage GetThumbnail(int index)
        {
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Thumbnail for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            }
            else return _Elements.ElementAt(index).Thumbnail;
        }


        /// <summary>
        ///     Gets an image preview of every individual scan in this instance.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<List<BitmapImage>> GetImagesAsync()
        {
            LogService?.Log.Information("BitmapImages of all pages have been requested.");
            List<BitmapImage> previews = new List<BitmapImage>();
            List<Task<BitmapImage>> tasks = new List<Task<BitmapImage>>();

            // kick off conversion for all files
            for (int i = 0; i < _Elements.Count; i++)
            {
                tasks.Add(GetImageAsync(i));
            }

            // collect results
            await Task.WhenAll(tasks);
            foreach (var task in tasks)
            {
                previews.Add(await task);
            }

            return previews;
        }


        /// <summary>
        ///     Gets an image preview of a single individual scan in this instance.
        /// </summary>
        /// <remarks>
        ///     Parts of this method are to required run on the UI thread.
        /// </remarks>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<BitmapImage> GetImageAsync(int index)
        {
            LogService?.Log.Information("Image for index {Index} requested.", index);
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Image for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for preview.");
            }

            // use cached image if possible
            if (_Elements[index].CachedImage != null)
            {
                LogService?.Log.Information("Returning a cached image.");
                return _Elements[index].CachedImage;
            }

            // create new bitmap
            StorageFile sourceFile = _Elements[index].ScanFile;
            BitmapImage bmp = null;
            int attempt = 0;
            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, async () =>
            {
                using (IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                {
                    switch (ConvertFormatStringToImageScannerFormat(sourceFile.FileType))
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

                                    LogService?.Log.Warning(e, "Opening the file stream of page at index {Index} failed, retrying in 500ms.", index);
                                    await Task.Delay(500);
                                    attempt++;
                                }
                            }
                            break;

                        case ImageScannerFormat.Pdf:
                            while (attempt != -1)
                            {
                                try
                                {
                                    PdfDocument doc = await PdfDocument.LoadFromStreamAsync(sourceStream);
                                    PdfPage page = doc.GetPage((uint)index);
                                    BitmapImage imageOfPdf = new BitmapImage();

                                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                                    {
                                        await page.RenderToStreamAsync(stream);
                                        await imageOfPdf.SetSourceAsync(stream);
                                    }
                                    break;
                                }
                                catch (Exception e)
                                {
                                    if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating bitmap of PDF page.", e);

                                    LogService?.Log.Warning(e, "Opening the file stream of page at index {Index} failed, retrying in 500ms.", index);
                                    await Task.Delay(500);
                                    attempt++;
                                }
                            }
                            break;

                        case ImageScannerFormat.Xps:
                        case ImageScannerFormat.OpenXps:
                            throw new NotImplementedException("Can not generate bitmap from (O)XPS.");

                        default:
                            throw new ApplicationException("Could not determine file type for generating a bitmap.");
                    }

                    // save image to cache
                    _Elements[index].CachedImage = bmp;

                    // generate thumbnail
                    BitmapImage thumbnail = new BitmapImage();
                    BitmapDecoder bitmapDecoder = null;
                    attempt = 0;
                    while (attempt != -1)
                    {
                        try
                        {
                            bitmapDecoder = await BitmapDecoder.CreateAsync(sourceStream);
                            SoftwareBitmap softwareBitmap = await bitmapDecoder.GetSoftwareBitmapAsync();
                            Guid encoderId = GetBitmapEncoderId(sourceFile.FileType);
                            var imageStream = new InMemoryRandomAccessStream();
                            BitmapEncoder bitmapEncoder = await BitmapEncoder.CreateAsync(encoderId, imageStream);
                            bitmapEncoder.SetSoftwareBitmap(softwareBitmap);

                            // reduce resolution of thumbnail
                            int resolutionScaling = 1;
                            if (softwareBitmap.PixelWidth < softwareBitmap.PixelHeight)
                            {
                                resolutionScaling = softwareBitmap.PixelWidth / 150;
                            }
                            else
                            {
                                resolutionScaling = softwareBitmap.PixelHeight / 150;
                            }
                            if (resolutionScaling < 1) resolutionScaling = 1;
                            bitmapEncoder.BitmapTransform.ScaledWidth = Convert.ToUInt32(bitmapDecoder.PixelWidth / resolutionScaling);
                            bitmapEncoder.BitmapTransform.ScaledHeight = Convert.ToUInt32(bitmapDecoder.PixelHeight / resolutionScaling);

                            await bitmapEncoder.FlushAsync();
                            await thumbnail.SetSourceAsync(imageStream);
                            _Elements[index].Thumbnail = thumbnail;
                            break;
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4)
                            {
                                LogService?.Log.Error(e, "Couldn't generate thumbnail of page at index {Index}", index);
                                return;
                            }

                            LogService?.Log.Warning(e, "Generating the thumbnail of page at index {Index} failed, retrying in 500ms.", index);
                            await Task.Delay(500);
                            attempt++;
                        }
                    }
                }
            });
            LogService?.Log.Information("Returning a newly generated image.");
            return bmp;
        }


        /// <summary>
        ///     Converts a scan to the <paramref name="targetFormat"/>. Also moves it to the <paramref name="targetFolder"/>.
        /// </summary>
        /// <param name="index">The index of the scan that's to be converted.</param>
        /// <param name="targetFormat">The format that the scan shall be converted to.</param>
        /// <param name="targetFolder">The folder that the conversion result shall be moved to.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="NotImplementedException">Attempted to convert to PDF or (O)XPS.</exception>
        /// <exception cref="ApplicationException">Could not determine file type of scan.</exception>
        public async Task ConvertScanAsync(int index, ImageScannerFormat targetFormat, StorageFolder targetFolder)
        {
            LogService?.Log.Information("Conversion of index {Index} into {TargetFormat} requested.", index, targetFormat);
            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Conversion of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for conversion.");
            }

            // convert
            StorageFile sourceFile = _Elements[index].ScanFile;
            string newName, newNameWithoutNumbering;
            switch (targetFormat)
            {
                case ImageScannerFormat.Jpeg:
                case ImageScannerFormat.Png:
                case ImageScannerFormat.Tiff:
                case ImageScannerFormat.DeviceIndependentBitmap:
                    // open image file, decode it and prepare an encoder with the target image format
                    using (IRandomAccessStream stream = await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                        Guid encoderId = GetBitmapEncoderId(targetFormat);

                        BitmapEncoder encoder = null;
                        if (targetFormat == ImageScannerFormat.Jpeg)
                        {
                            // Fix large JPG size
                            var propertySet = new BitmapPropertySet();
                            var qualityValue = new BitmapTypedValue(0.85d, Windows.Foundation.PropertyType.Single);
                            propertySet.Add("ImageQuality", qualityValue);

                            stream.Size = 0;
                            encoder = await BitmapEncoder.CreateAsync(encoderId, stream, propertySet);
                        }
                        else
                        {
                            encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                        }
                        encoder.SetSoftwareBitmap(softwareBitmap);

                        // save/encode the file in the target format
                        try { await encoder.FlushAsync(); }
                        catch (Exception exc)
                        {
                            Crashes.TrackError(exc);
                            LogService?.Log.Error(exc, "Conversion of the scan failed.");
                            throw;
                        }
                    }

                    // get new file name with updated extension
                    newNameWithoutNumbering = RemoveNumbering(sourceFile.Name
                        .Replace("." + sourceFile.Name.Split(".")[1], "." + targetFormat.ToString().ToLower()));
                    newName = newNameWithoutNumbering;

                    // move file to the correct folder
                    newName = await MoveFileToFolderAsync(sourceFile, targetFolder, newName, false);
                    break;

                case ImageScannerFormat.Pdf:
                case ImageScannerFormat.Xps:
                case ImageScannerFormat.OpenXps:
                    LogService?.Log.Error("Requested conversion for unsupported format.");
                    throw new NotImplementedException("Can not convert to (O)XPS.");

                default:
                    LogService?.Log.Error("Requested conversion without sepcifying format.");
                    throw new ApplicationException("Could not determine target file type for conversion.");
            }

            // refresh file
            _Elements[index].ScanFile = await targetFolder.GetFileAsync(newName);
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
        private static async Task<string> MoveFileToFolderAsync(StorageFile file, StorageFolder targetFolder, string desiredName, bool replaceExisting)
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
        ///     Rotates a scan. Consecutive rotations are lossless if the scan isn't edited in-between. Only supports JPG, PNG, TIF and BMP.
        /// </summary>
        /// <param name="instructions">The indices and desired degrees of the scans that are to be rotated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ArgumentException">The selected scan's file type is not supported for rotation.</exception>
        /// <exception cref="ApplicationException">Something went wrong while rotating. Perhaps the scan format isn't supported.</exception>
        public async Task RotateScansAsync(IList<Tuple<int, BitmapRotation>> instructions)
        {
            Analytics.TrackEvent("Rotate pages", new Dictionary<string, string> {
                            { "Rotation", instructions[0].Item2.ToString() },
                        });
            LogService?.Log.Information("Received {@Instructions} for rotations.", instructions);

            // check indices and rotations
            foreach (var instruction in instructions)
            {
                if (!IsValidIndex(instruction.Item1))
                {
                    LogService?.Log.Error("Rotation for index {Index} requested, but there are only {Num} pages. Aborting all rotations.", instruction.Item1, _Elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index " + instruction.Item1 + " for rotation.");
                }
                if (instruction.Item2 == BitmapRotation.None) return;
            }

            // rotate and make sure that consecutive rotations are lossless
            switch (ScanResultFormat)
            {
                case ImageScannerFormat.Jpeg:
                case ImageScannerFormat.Png:
                case ImageScannerFormat.Tiff:
                case ImageScannerFormat.DeviceIndependentBitmap:
                case ImageScannerFormat.Pdf:
                    foreach (var instruction in instructions)
                    {
                        try
                        {
                            using (IRandomAccessStream fileStream = await _Elements[instruction.Item1].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                BitmapDecoder decoder;
                                if (_Elements[instruction.Item1].ImageWithoutRotation == null)
                                {
                                    _Elements[instruction.Item1].ImageWithoutRotation = await GetImageFile(instruction.Item1)
                                        .CopyAsync(AppDataService.FolderWithoutRotation, GetImageFile(instruction.Item1).Name, NameCollisionOption.ReplaceExisting);
                                    decoder = await BitmapDecoder.CreateAsync(fileStream);
                                }
                                else
                                {
                                    using (IRandomAccessStream bitmapStream = await _Elements[instruction.Item1].ImageWithoutRotation.OpenReadAsync())
                                    {
                                        decoder = await BitmapDecoder.CreateAsync(bitmapStream);
                                    }
                                }

                                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                                Guid encoderId = GetBitmapEncoderId(_Elements[instruction.Item1].ScanFile.Name.Split(".")[1]);

                                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, fileStream);
                                encoder.SetSoftwareBitmap(softwareBitmap);

                                encoder.BitmapTransform.Rotation = CombineRotations(instruction.Item2, _Elements[instruction.Item1].CurrentRotation);

                                await encoder.FlushAsync();
                                _Elements[instruction.Item1].CurrentRotation = encoder.BitmapTransform.Rotation;
                            }
                        }
                        catch (Exception e)
                        {
                            throw new ApplicationException("Rotation failed.", e);
                        }

                        // delete image from cache
                        _Elements[instruction.Item1].CachedImage = null;
                        await _Elements[instruction.Item1].GetImageAsync();
                    }

                    if (ScanResultFormat == ImageScannerFormat.Pdf) await GeneratePDF();
                    break;

                case ImageScannerFormat.Xps:
                case ImageScannerFormat.OpenXps:
                    LogService?.Log.Error("Requested rotation for unsupported format {Format}.", ScanResultFormat);
                    throw new ArgumentException("Rotation not supported for PDF, XPS or OXPS.");

                default:
                    LogService?.Log.Error("Requested rotation for unknown file type.");
                    throw new ApplicationException("Could not determine source file type for rotation.");
            }
        }


        /// <summary>
        ///     Combines two rotations.
        /// </summary>
        /// <exception cref="ApplicationException">Logic error.</exception>
        private BitmapRotation CombineRotations(BitmapRotation rotation1, BitmapRotation rotation2)
        {
            switch (rotation1)
            {
                case BitmapRotation.None:
                    return rotation2;
                case BitmapRotation.Clockwise90Degrees:
                    switch (rotation2)
                    {
                        case BitmapRotation.None:
                            return rotation1;
                        case BitmapRotation.Clockwise90Degrees:
                            return BitmapRotation.Clockwise180Degrees;
                        case BitmapRotation.Clockwise180Degrees:
                            return BitmapRotation.Clockwise270Degrees;
                        case BitmapRotation.Clockwise270Degrees:
                            return BitmapRotation.None;
                    }
                    break;

                case BitmapRotation.Clockwise180Degrees:
                    switch (rotation2)
                    {
                        case BitmapRotation.None:
                            return rotation1;
                        case BitmapRotation.Clockwise90Degrees:
                            return BitmapRotation.Clockwise270Degrees;
                        case BitmapRotation.Clockwise180Degrees:
                            return BitmapRotation.None;
                        case BitmapRotation.Clockwise270Degrees:
                            return BitmapRotation.Clockwise90Degrees;
                    }
                    break;

                case BitmapRotation.Clockwise270Degrees:
                    switch (rotation2)
                    {
                        case BitmapRotation.None:
                            return rotation1;
                        case BitmapRotation.Clockwise90Degrees:
                            return BitmapRotation.None;
                        case BitmapRotation.Clockwise180Degrees:
                            return BitmapRotation.Clockwise90Degrees;
                        case BitmapRotation.Clockwise270Degrees:
                            return BitmapRotation.Clockwise180Degrees;
                    }
                    break;
            }

            throw new ApplicationException("Rotations could not be added.");
        }


        /// <summary>
        ///     Renames the selected scan.
        /// </summary>
        /// <param name="index">The index of the scan that's to be renamed.</param>
        /// <param name="newDisplayName">The desired display name (without extension) for the scan.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task RenameScanAsync(int index, string newDisplayName)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.RenamePage);
            LogService?.Log.Information("Renaming index {Index} to display name {Name}.", index, newDisplayName);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Rename for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for rename.");
            }

            // check name is different
            if (Elements[index].ScanFile.DisplayName == newDisplayName) return;

            // rename
            string fullName = newDisplayName + Elements[index].ScanFile.FileType;
            await Elements[index].RenameFileAsync(fullName);

            RefreshItemDescriptors();
        }


        /// <summary>
        ///     Renames the scan. Only for scans that are combined into a single document (e.g. PDF).
        /// </summary>
        /// <param name="newDisplayName">The desired display name for the scan.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task RenameScanAsync(string newDisplayName)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.RenamePDF);
            LogService?.Log.Information("Renaming PDF to display name {Name}.", newDisplayName);

            // check type
            if (ScanResultFormat != ImageScannerFormat.Pdf)
            {
                LogService?.Log.Error("Attempted to rename entire file for non-PDF.");
                throw new ApplicationException("ScanResult represents more than one file.");
            }

            // check name is different
            if (Pdf.Name == newDisplayName) return;

            // rename
            string fullName = newDisplayName + Pdf.FileType;
            await Pdf.RenameAsync(fullName, NameCollisionOption.FailIfExists);

            RefreshItemDescriptors();
        }


        /// <summary>
        ///     Crops the selected scan.
        /// </summary>
        /// <param name="index">The index of the scan that the crop is to be applied to.</param>
        /// <param name="imageCropper">The source of the crop.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Applying the crop failed.</exception>
        public async Task CropScanAsync(int index, ImageCropper imageCropper)
        {
            Analytics.TrackEvent("Crop");
            LogService?.Log.Information("Requested crop for index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Crop for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for crop.");
            }

            // save changes to original file
            IRandomAccessStream stream = null;
            try
            {
                stream = await _Elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(_Elements[index].ScanFile), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            stream.Dispose();

            // refresh cached image, delete image without rotation and reset rotation
            _Elements[index].CachedImage = null;
            await _Elements[index].GetImageAsync();
            if (_Elements[index].ImageWithoutRotation != null)
            {
                await _Elements[index].ImageWithoutRotation.DeleteAsync();
            }
            _Elements[index].ImageWithoutRotation = null;
            _Elements[index].CurrentRotation = BitmapRotation.None;

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf) await GeneratePDF();
        }


        /// <summary>
        ///     Crops the selected scans.
        /// </summary>
        /// <param name="indices">The indices of the scan that the crop is to be applied to.</param>
        /// <param name="cropRegion">The desired crop region.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Applying the crop failed.</exception>
        public async Task CropScansAsync(List<int> indices, Rect cropRegion)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.CropMultiple);
            LogService?.Log.Information("Requested crop for indices {@Indices}.", indices);

            // check indices
            foreach (int index in indices)
            {
                if (!IsValidIndex(index))
                {
                    LogService?.Log.Error("Crop for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index for crop.");
                }
            }

            // save changes to original files
            foreach (int index in indices)
            {
                // loosely based on CropImageAsync(...) used by the ImageCropper control
                cropRegion.X = Math.Max(cropRegion.X, 0);
                cropRegion.Y = Math.Max(cropRegion.Y, 0);
                var x = (uint)Math.Floor(cropRegion.X);
                var y = (uint)Math.Floor(cropRegion.Y);
                var width = (uint)Math.Floor(cropRegion.Width);
                var height = (uint)Math.Floor(cropRegion.Height);

                using (IRandomAccessStream stream = await GetImageFile(index).OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(ScanResultFormat), stream);
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    encoder.BitmapTransform.Bounds = new BitmapBounds
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };
                    await encoder.FlushAsync();
                }

                // refresh cached image, delete image without rotation and reset rotation
                _Elements[index].CachedImage = null;
                await _Elements[index].GetImageAsync();
                if (_Elements[index].ImageWithoutRotation != null)
                {
                    await _Elements[index].ImageWithoutRotation.DeleteAsync();
                }
                _Elements[index].ImageWithoutRotation = null;
                _Elements[index].CurrentRotation = BitmapRotation.None;

                // if necessary, generate PDF
                if (ScanResultFormat == ImageScannerFormat.Pdf) await GeneratePDF();
            }
        }


        /// <summary>
        ///     Crops the selected scan and saves the changes to a new file. The copy is then added to this instance.
        /// </summary>
        /// <param name="index">The scan of which a copy is to be cropped.</param>
        /// <param name="imageCropper">The source of the crop.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Applying the crop to a copy failed.</exception>
        public async Task CropScanAsCopyAsync(int index, ImageCropper imageCropper)
        {
            Analytics.TrackEvent("Crop as copy");
            LogService?.Log.Information("Requested crop as copy for index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Crop as copy index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for crop.");
            }

            // save crop as new file
            StorageFile file;
            IRandomAccessStream stream = null;
            try
            {
                StorageFolder folder = null;
                if (ScanResultFormat == ImageScannerFormat.Pdf) folder = folderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + _Elements[index].FutureAccessListIndex.ToString());

                file = await folder.CreateFileAsync(_Elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(_Elements[index].ScanFile), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
            stream.Dispose();

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
            {
                _Elements.Insert(index + 1, new ScanResultElement(file, _Elements[index].FutureAccessListIndex));
                NumberOfPages += 1;
            });

            RefreshItemDescriptors();
            await _Elements[index + 1].GetImageAsync();

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {

                try
                {
                    List<StorageFile> filesNumbering = new List<StorageFile>();
                    for (int i = index + 1; i < _Elements.Count; i++)
                    {
                        await _Elements[i].ScanFile.RenameAsync("_" + _Elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                        filesNumbering.Add(_Elements[i].ScanFile);
                    }
                    await PrepareNewConversionFiles(filesNumbering, index + 1);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to generate PDF after cropping index {Index} as copy. Attempting to get rid of copy.", index);
                    Crashes.TrackError(exc);
                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.RemoveAt(index + 1));
                    NumberOfPages = _Elements.Count;
                    try { await file.DeleteAsync(); } catch (Exception e) { LogService?.Log.Error(e, "Undo failed as well."); }
                    RefreshItemDescriptors();
                    throw;
                }
                await GeneratePDF();
            }
        }


        /// <summary>
        ///     Deletes the desired scans in this instance. If the last element of a PDF file is deleted,
        ///     the PDF file will be deleted as well.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while deleting the scans.</exception>
        public async Task DeleteScansAsync(List<int> indices, StorageDeleteOption deleteOption)
        {
            Analytics.TrackEvent("Delete pages");
            LogService?.Log.Information("Requested Deletion of indices {@Indices} with option {Option}.", indices, deleteOption);

            List<int> sortedIndices = new List<int>(indices);
            sortedIndices.Sort();
            sortedIndices.Reverse();

            foreach (int index in sortedIndices)
            {
                // check index
                if (!IsValidIndex(index))
                {
                    LogService?.Log.Error("Deletion of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index for mass deletion.");
                }

                await _Elements[index].ScanFile.DeleteAsync(deleteOption);
                await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.RemoveAt(index));
                NumberOfPages = _Elements.Count;
            }

            RefreshItemDescriptors();

            // if necessary, update or delete PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                if (NumberOfPages > 0)
                {
                    // assign temporary names and then reinstate the file order prior to generation
                    for (int i = 0; i < _Elements.Count; i++)
                    {
                        await _Elements[i].ScanFile.RenameAsync("_" + i + _Elements[i].ScanFile.FileType);
                    }
                    await PrepareNewConversionFiles(_Elements.Select(e => e.ScanFile).ToList(), 0);

                    await GeneratePDF();
                }
                else await Pdf.DeleteAsync();
            }
        }


        /// <summary>
        ///     Deletes the desired scans in this instance. If the last element of a PDF file is deleted,
        ///     the PDF file will be deleted as well.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while deleting the scans.</exception>
        public async Task DeleteScansAsync(List<int> indices)
        {
            await DeleteScansAsync(indices, StorageDeleteOption.Default);
        }


        /// <summary>
        ///     Deletes a single scan in this instance. If the last element of a PDF file is deleted,
        ///     the PDF file will be deleted as well.
        /// </summary>
        /// <param name="index">The index of the scan that shall be deleted.</param>
        /// <exception cref="Exception">Something went wrong while delting the scan.</exception>
        public async Task DeleteScanAsync(int index, StorageDeleteOption deleteOption)
        {
            Analytics.TrackEvent("Delete page");
            LogService?.Log.Information("Requested Deletion of index {Index} with option {Option}.", index, deleteOption);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Deletion of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for deletion.");
            }

            await _Elements[index].ScanFile.DeleteAsync(deleteOption);

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.RemoveAt(index));
            NumberOfPages = _Elements.Count;
            RefreshItemDescriptors();

            // if necessary, update or delete PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                if (NumberOfPages > 0)
                {
                    // assign temporary names and then reinstate the file order prior to generation
                    for (int i = 0; i < _Elements.Count; i++)
                    {
                        await _Elements[i].ScanFile.RenameAsync("_" + i + _Elements[i].ScanFile.FileType);
                    }
                    await PrepareNewConversionFiles(_Elements.Select(e => e.ScanFile).ToList(), 0);

                    await GeneratePDF();
                }
                else await Pdf.DeleteAsync();
            }
        }


        /// <summary>
        ///     Deletes a single scan in this instance. If the last element of a PDF file is deleted,
        ///     the PDF file will be deleted as well.
        /// </summary>
        /// <param name="index">The index of the scan that shall be deleted.</param>
        /// <exception cref="Exception">Something went wrong while delting the scan.</exception>
        public async Task DeleteScanAsync(int index)
        {
            await DeleteScanAsync(index, StorageDeleteOption.Default);
        }


        /// <summary>
        ///     Add ink strokes from a canvas to the selected scan.
        /// </summary>
        /// <param name="index">The index of the scan that the strokes shall be added to.</param>
        /// <param name="inkCanvas">The canvas that holds the strokes.</param>
        /// <exception cref="Exception">Something went wrong while applying the strokes.</exception>
        public async Task DrawOnScanAsync(int index, InkCanvas inkCanvas)
        {
            Analytics.TrackEvent("Draw on page");
            LogService?.Log.Information("Drawing on index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Drawing on index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for drawing.");
            }

            // save changes to original file
            IRandomAccessStream fileStream;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96);
                using (fileStream = await _Elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, fileStream);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.White);

                        ds.DrawImage(canvasBitmap);
                        ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                    }
                    await renderTarget.SaveAsync(fileStream, GetCanvasBitmapFileFormat(_Elements[index].ScanFile), 1f);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Saving draw changes failed.");
                throw;
            }

            // refresh cached image, delete image without rotation and reset rotation
            _Elements[index].CachedImage = null;
            await _Elements[index].GetImageAsync();
            if (_Elements[index].ImageWithoutRotation != null)
            {
                await _Elements[index].ImageWithoutRotation.DeleteAsync();
            }
            _Elements[index].ImageWithoutRotation = null;
            _Elements[index].CurrentRotation = BitmapRotation.None;

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf) await GeneratePDF();
        }


        /// <summary>
        ///     Add ink strokes from a canvas to a copy of the selected scan. The copy is then added to this instance.
        /// </summary>
        /// <param name="index">The index of the scan whose copy the strokes shall be added to.</param>
        /// <param name="inkCanvas">The canvas that holds the strokes.</param>
        /// <exception cref="Exception">Something went wrong while applying the strokes.</exception>
        public async Task DrawOnScanAsCopyAsync(int index, InkCanvas inkCanvas)
        {
            Analytics.TrackEvent("Draw on page as copy");
            LogService?.Log.Information("Drawing as copy on index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Drawing as copy on index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for drawing.");
            }

            // save changes to original file
            IRandomAccessStream sourceStream;
            StorageFile file;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi);
                using (sourceStream = await _Elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, sourceStream);
                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.White);

                        ds.DrawImage(canvasBitmap);
                        ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                    }
                }

                StorageFolder folder = null;
                if (ScanResultFormat == ImageScannerFormat.Pdf) folder = folderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + _Elements[index].FutureAccessListIndex.ToString());

                file = await folder.CreateFileAsync(_Elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                using (IRandomAccessStream targetStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderTarget.SaveAsync(targetStream, GetCanvasBitmapFileFormat(_Elements[index].ScanFile), 1f);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Saving draw as copy changes failed.");
                throw;
            }

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () =>
            {
                _Elements.Insert(index + 1, new ScanResultElement(file, _Elements[index].FutureAccessListIndex));
            });
            await _Elements[index + 1].GetImageAsync();
            NumberOfPages += 1;
            RefreshItemDescriptors();

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                try
                {
                    List<StorageFile> filesNumbering = new List<StorageFile>();
                    for (int i = index + 1; i < _Elements.Count; i++)
                    {
                        await _Elements[i].ScanFile.RenameAsync("_" + _Elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                        filesNumbering.Add(_Elements[i].ScanFile);
                    }
                    await PrepareNewConversionFiles(filesNumbering, index + 1);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to generate PDF after drawing on index {Index} as copy. Attempting to get rid of copy.", index);
                    Crashes.TrackError(exc);
                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.RemoveAt(index + 1));
                    try { await file.DeleteAsync(); } catch (Exception e) { LogService?.Log.Error(e, "Undo failed as well."); }
                    RefreshItemDescriptors();
                    throw;
                }
                await GeneratePDF();
            }
        }


        private bool IsValidIndex(int index)
        {
            if (0 <= index && index < _Elements.Count)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        ///     Gets the <see cref="ImageProperties"/> of the selected scan. Only JPG, PNG, TIF and BMP are supported.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ArgumentException">Invalid file format.</exception>
        public async Task<ImageProperties> GetImagePropertiesAsync(int index)
        {
            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("ImageProperties for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting properties.");
            }

            ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(_Elements[index].ScanFile.FileType);
            switch (format)
            {
                case ImageScannerFormat.Jpeg:
                case ImageScannerFormat.Png:
                case ImageScannerFormat.Tiff:
                case ImageScannerFormat.DeviceIndependentBitmap:
                case ImageScannerFormat.Pdf:
                    return (await _Elements[index].ScanFile.Properties.GetImagePropertiesAsync());
                default:
                    throw new ArgumentException("Invalid file format for getting image properties.");
            }
        }


        /// <summary>
        ///     Copies all image files in this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public Task CopyImagesToClipboardAsync()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < NumberOfPages; i++)
            {
                indices.Add(i);
            }

            return CopyImagesToClipboardAsync(indices);
        }


        // <summary>
        ///     Copies some image files in this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyImagesToClipboardAsync(IList<int> indices)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.CopyPages);
            LogService?.Log.Information("Copying indices {@Indices} requested.", indices);

            if (NumberOfPages == 0)
            {
                LogService?.Log.Error("Copying requested, but there are no pages.");
                throw new ApplicationException("No scans left to copy.");
            }

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check indices and whether the corresponding files are still available
            foreach (int index in indices)
            {
                if (index < 0 || index > NumberOfPages - 1)
                {
                    LogService?.Log.Error("Copying index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                    throw new ApplicationException("Invalid index for copying scan file.");
                }

                try { await _Elements[index].ScanFile.OpenAsync(FileAccessMode.Read); }
                catch (Exception e)
                {
                    LogService?.Log.Error(e, "Copying failed because the element at index {Index} is not available anymore.", index);
                    throw new ApplicationException("At least one scan file is not available anymore.", e);
                }
            }

            // copy desired scans to clipboard
            try
            {
                List<StorageFile> list = new List<StorageFile>();
                foreach (int index in indices)
                {
                    list.Add(_Elements[index].ScanFile);
                }
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                LogService?.Log.Error(e, "Copying pages failed.");
                throw new ApplicationException("Something went wrong while copying the scans.", e);
            }
        }



        /// <summary>
        ///     Copies the selected image file to the clipboard.
        /// </summary>
        /// <param name="index">The index of the scan that shall be copied.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyImageToClipboardAsync(int index)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.CopyPage);
            LogService?.Log.Information("Copying index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Copying index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for copying file.");
            }

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await _Elements[index].ScanFile.OpenAsync(FileAccessMode.Read); }
            catch (Exception e)
            {
                LogService?.Log.Error("Copying index {Index} requested, but the file is not available anymore.", index);
                throw new ApplicationException("Selected scan file is not available anymore.", e);
            }

            // set contents according to file type and copy to clipboard
            ImageScannerFormat format = ScanResultFormat;

            try
            {
                switch (format)
                {
                    case ImageScannerFormat.Jpeg:
                    case ImageScannerFormat.Png:
                    case ImageScannerFormat.Tiff:
                    case ImageScannerFormat.DeviceIndependentBitmap:
                        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(_Elements[index].ScanFile));
                        Clipboard.SetContent(dataPackage);
                        break;
                    default:
                        List<StorageFile> list = new List<StorageFile>();
                        list.Add(_Elements[index].ScanFile);
                        dataPackage.SetStorageItems(list);
                        Clipboard.SetContent(dataPackage);
                        break;
                }
            }
            catch (Exception e)
            {
                LogService?.Log.Error(e, "Copying the page failed.");
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Copies the file represented by this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyToClipboardAsync()
        {
            AppCenterService?.TrackEvent(AppCenterEvent.CopyDocument);
            LogService?.Log.Information("Copying document requested.");

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await Pdf.OpenAsync(FileAccessMode.Read); }
            catch (Exception e)
            {
                LogService?.Log.Error(e, "Copying document failed.");
                throw new ApplicationException("File is not available anymore.", e);
            }

            try
            {
                List<StorageFile> list = new List<StorageFile>();
                list.Add(Pdf);
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                LogService?.Log.Error(e, "Copying document failed.");
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Adds the specified files to this instance and converts them to the targetFormat, if necessary. The final files are
        ///     saved to the targetFolder, unless the result is a PDF document.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public async Task AddFiles(IEnumerable<StorageFile> files, ImageScannerFormat? targetFormat, StorageFolder targetFolder, int futureAccessListIndexStart)
        {
            LogService?.Log.Information("Adding {Num} files, the target format is {Format}.", files.Count(), targetFormat);
            int futureAccessListIndex = futureAccessListIndexStart;

            string append = DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00");
            if (targetFormat == null || targetFormat == ImageScannerFormat.Pdf)
            {
                // no conversion (but perhaps generation later on), just add files for now
                if (targetFormat == ImageScannerFormat.Pdf) await PrepareNewConversionFiles(files, NumberOfPages);

                foreach (StorageFile file in files)
                {
                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.Add(new ScanResultElement(file, futureAccessListIndex)));

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                    }

                    if ((bool)SettingsService.GetSetting(AppSetting.SettingAppendTime) && targetFormat != ImageScannerFormat.Pdf)
                    {
                        await SetInitialNameAsync(_Elements[_Elements.Count - 1], append);
                    }
                }
            }
            else
            {
                int numberOfPagesOld = NumberOfPages;

                // immediate conversion necessary
                foreach (StorageFile file in files)
                {
                    if (file == null) continue;

                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.Add(new ScanResultElement(file, futureAccessListIndex)));
                    NumberOfPages = _Elements.Count;

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                    }

                    if ((bool)SettingsService.GetSetting(AppSetting.SettingAppendTime))
                    {
                        await SetInitialNameAsync(_Elements[_Elements.Count - 1], append);
                    }
                }

                Task[] conversionTasks = new Task[files.Count()];
                try
                {
                    for (int i = numberOfPagesOld; i < NumberOfPages; i++)
                    {
                        conversionTasks[i - numberOfPagesOld] = ConvertScanAsync(i, (ImageScannerFormat)targetFormat, targetFolder);
                    }
                    await Task.WhenAll(conversionTasks);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to convert at least one new page. All new pages will be discarded.");

                    // wait until all tasks are completed, the result is irrelevant
                    Task[] actualConversionTasks = conversionTasks.Where(task => task != null).ToArray();
                    while (Array.Find(actualConversionTasks, task => task.IsCompleted != true) != null)
                    {
                        try
                        {
                            await Task.WhenAll(actualConversionTasks);
                        }
                        catch (Exception) { }
                    }

                    // discard new pages to restore the old state
                    List<int> indices = new List<int>();
                    for (int i = numberOfPagesOld; i < NumberOfPages; i++)
                    {
                        indices.Add(i);
                    }
                    await DeleteScansAsync(indices);
                    throw;
                }
            }

            // if necessary, generate PDF now
            if (ScanResultFormat == ImageScannerFormat.Pdf) await GeneratePDF();

            // automatic rotation
            if ((bool)SettingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < NumberOfPages; i++)
                {
                    ScanResultElement element = Elements[i];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    if (format != null)
                    {
                        BitmapRotation recommendedRotation = await AutoRotatorService.TryGetRecommendedRotationAsync(
                            element.ScanFile, (ImageScannerFormat)format);

                        if (recommendedRotation != BitmapRotation.None)
                        {
                            instructions.Add(new Tuple<int, BitmapRotation>(i, recommendedRotation));
                        }
                    }
                }

                if (instructions.Count > 0)
                {
                    await RotateScansAsync(instructions);
                }
            }

            // generate new previews and descriptors
            for (int i = NumberOfPages - files.Count(); i < NumberOfPages; i++)
            {
                await GetImageAsync(i);
                _Elements[i].ItemDescriptor = GetDescriptorForIndex(i);
            }
        }


        /// <summary>
        ///     Adds the specified files to this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public Task AddFiles(IEnumerable<StorageFile> files, ImageScannerFormat? targetFormat, int futureAccessListIndexStart)
        {
            return AddFiles(files, targetFormat, OriginalTargetFolder, futureAccessListIndexStart);
        }


        /// <summary>
        ///     Launches the "Open with" dialog for the selected scan.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task<bool> OpenImageWithAsync(int index)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.OpenWith);
            LogService?.Log.Information("Requested opening with of index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Opening with of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for opening file.");
            }

            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            return await Launcher.LaunchFileAsync(_Elements[index].ScanFile, options);
        }


        /// <summary>
        ///     Launches the selected scan with the given <paramref name="appInfo"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task<bool> OpenImageWithAsync(int index, AppInfo appInfo)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.OpenWith, new Dictionary<string, string> {
                            { "DisplayName", appInfo.DisplayInfo.DisplayName },
                        });
            LogService?.Log.Information($"Requested opening with of index {index} with app '{appInfo.DisplayInfo.DisplayName}'.");

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error($"Opening with of index {index} requested, but there are only {_Elements.Count} pages.");
                throw new ArgumentOutOfRangeException("Invalid index for opening file.");
            }

            LauncherOptions options = new LauncherOptions();
            options.TargetApplicationPackageFamilyName = appInfo.PackageFamilyName;

            return await Launcher.LaunchFileAsync(_Elements[index].ScanFile, options);
        }


        /// <summary>
        ///     Launches the "Open with" dialog for the represented file.
        /// </summary>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task<bool> OpenWithAsync()
        {
            AppCenterService?.TrackEvent(AppCenterEvent.OpenWith);
            LogService?.Log.Information("Requested opening with of document.");

            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            return await Launcher.LaunchFileAsync(Pdf, options);
        }


        /// <summary>
        ///     Launches the represented file with the given <paramref name="appInfo"/>.
        /// </summary>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task<bool> OpenWithAsync(AppInfo appInfo)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.OpenWith, new Dictionary<string, string> {
                            { "DisplayName", appInfo.DisplayInfo.DisplayName },
                        });
            LogService?.Log.Information($"Requested opening with of document with app '{appInfo.DisplayInfo.DisplayName}'.");

            LauncherOptions options = new LauncherOptions();
            options.TargetApplicationPackageFamilyName = appInfo.PackageFamilyName;

            return await Launcher.LaunchFileAsync(Pdf, options);
        }


        /// <summary>
        ///     Returns the file format of the scan result (assumes that there can't be a mix).
        /// </summary>
        public ImageScannerFormat? GetFileFormat()
        {
            return ScanResultFormat;
        }


        /// <summary>
        ///     Generates the PDF file in the temp folder and then moves it to the <see cref="OriginalTargetFolder"/> of
        ///     this scanResult. The pages are constructed using all images found in the temp folder's subfolder "conversion".
        /// </summary>
        /// <param name="fileName"></param>
        private async Task GeneratePDFAsync(string fileName)
        {
            LogService?.Log.Information("Requested PDF generation.");

            string newName;
            StorageFile newPdf;

            if (Pdf == null)
            {
                LogService?.Log.Information("PDF doesn't exist yet.");
                newPdf = await AppDataService.FolderTemp.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                LogService?.Log.Information("PDF already exists.");
                newPdf = Pdf;
            }

            newName = newPdf.Name;

            try
            {
                taskCompletionSource = new TaskCompletionSource<bool>();
                var win32ResultAsync = taskCompletionSource.Task;

                // save the target name
                ApplicationData.Current.LocalSettings.Values["targetFileName"] = newPdf.Name;

                // delete potential rogue files
                await CleanUpReceivedPagesFolder();

                int attempt = 1;
                while (attempt >= 0)
                {
                    // call win32 app and wait for result
                    LogService?.Log.Information("Launching full trust process.");
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                    await win32ResultAsync.ConfigureAwait(false);
                    LogService?.Log.Information("Full trust process is done.");

                    // get result file and move it to its correct folder
                    try
                    {
                        newPdf = null;
                        newPdf = await AppDataService.FolderTemp.GetFileAsync(newName);
                        attempt = -1;
                    }
                    catch (Exception)
                    {
                        if (attempt == 3) throw;

                        attempt++;
                        await Task.Delay(TimeSpan.FromSeconds(3));
                    }
                }

                // move PDF file to target folder
                if (Pdf == null)
                {
                    // PDF generated in target folder for the first time
                    await MoveFileToFolderAsync(newPdf, OriginalTargetFolder, newName, false);
                }
                else
                {
                    // PDF updated ~> replace old file
                    await MoveFileToFolderAsync(newPdf, OriginalTargetFolder, newName, true);
                }
                Pdf = newPdf;
                return;
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Generating the PDF failed. Attempted to generate " + newName);
                var files = await AppDataService.FolderTemp.GetFilesAsync();
                LogService?.Log.Information("State of temp folder: {@Folder}", files.Select(f => f.Name).ToList());
                Crashes.TrackError(exc);
                throw;
            }
        }

        public Task GeneratePDF()
        {
            return GeneratePDFAsync(Pdf.Name);
        }


        /// <summary>
        ///     Numbers new files.
        /// </summary>
        /// <param name="files">The files to be numbered.</param>
        /// <param name="startIndex">The first number.</param>
        private static async Task PrepareNewConversionFiles(IEnumerable<StorageFile> files, int startIndex)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();

            try
            {
                int nextNumber = startIndex;
                foreach (StorageFile file in files)
                {
                    await file.MoveAsync(folderConversion, nextNumber + file.FileType);
                    nextNumber++;
                }
            }
            catch (Exception exc)
            {
                logService?.Log.Error(exc, "Preparing conversion files with startIndex {Index} failed.", startIndex);
                throw;
            }
        }


        /// <summary>
        ///     Applies the order of <see cref="_Elements"/> to the PDF file.
        /// </summary>
        public async Task ApplyElementOrderToFilesAsync()
        {
            if (GetFileFormat() != ImageScannerFormat.Pdf)
            {
                LogService?.Log.Error("Attempted to apply element order to non-PDF file.");
                throw new ApplicationException("Can only reorder source files for PDF.");
            }

            RefreshItemDescriptors();

            int nextNumber = 0;
            List<ScanResultElement> changedElements = new List<ScanResultElement>();

            // first rename all affected files to a temporary name to free up the file names
            foreach (ScanResultElement element in _Elements)
            {
                if (element.ScanFile.DisplayName != nextNumber.ToString())
                {
                    await element.RenameFileAsync("_" + nextNumber + element.ScanFile.FileType);
                    changedElements.Add(element);
                }
                nextNumber++;
            }

            // then give each file its final name
            foreach (ScanResultElement element in changedElements)
            {
                await element.RenameFileAsync(element.ScanFile.DisplayName.Split("_")[1] + element.ScanFile.FileType);
            }
        }


        /// <summary>
        ///     Refreshes all item descriptors of <see cref="_Elements"/>.
        /// </summary>
        private void RefreshItemDescriptors()
        {
            for (int i = 0; i < _Elements.Count; i++)
            {
                _Elements[i].ItemDescriptor = GetDescriptorForIndex(i);
            }
        }


        /// <summary>
        ///     Gets a specific item descriptor.
        /// </summary>
        /// <param name="index"></param>
        public string GetDescriptorForIndex(int index)
        {
            if (IsImage)
            {
                return Elements[index].ScanFile.DisplayName;
            }
            else
            {
                return String.Format(LocalizedString("TextPageListDescriptor"), (index + 1).ToString());
            }
        }


        /// <summary>
        ///     Sets the name of the elements or the PDF file to their initial value.
        /// </summary>
        protected async Task SetInitialNameAsync(ScanResultElement element, string append)
        {
            string baseName = element.ScanFile.Name;
            RemoveNumbering(baseName);

            string baseDisplayName = baseName.Split(".")[0];

            await element.RenameFileAsync(baseDisplayName + "_" + append + element.ScanFile.FileType, NameCollisionOption.GenerateUniqueName);
        }

        protected async Task SetInitialNameAsync(StorageFile file, string append)
        {
            string baseName = file.Name;
            RemoveNumbering(baseName);

            string baseDisplayName = baseName.Split(".")[0];

            await file.RenameAsync(baseDisplayName + "_" + append + file.FileType, NameCollisionOption.GenerateUniqueName);
            RefreshItemDescriptors();
        }

        protected async Task SetInitialNamesAsync()
        {
            string append = DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00");

            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                await SetInitialNameAsync(Pdf, append);
            }
            else
            {
                foreach (ScanResultElement element in _Elements)
                {
                    await SetInitialNameAsync(element, append);
                }
                RefreshItemDescriptors();
            }
        }


        /// <summary>
        ///     Removes all items in <see cref="folderReceivedPagesPDF"/>.
        /// </summary>
        private async Task CleanUpReceivedPagesFolder()
        {
            var files = await folderReceivedPagesPDF.GetFilesAsync();

            foreach (StorageFile file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }


        /// <summary>
        ///     Duplicates the selected page and adds it to the instance (right behind its
        ///     parent page).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Duplicating the page failed.</exception>
        public async Task DuplicatePageAsync(int index)
        {
            AppCenterService?.TrackEvent(AppCenterEvent.DuplicatePage);
            LogService?.Log.Information("Requested duplication for index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Duplication of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for duplication.");
            }

            // duplicate file
            StorageFile file;
            try
            {
                StorageFolder folder = null;
                if (ScanResultFormat == ImageScannerFormat.Pdf) folder = folderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + _Elements[index].FutureAccessListIndex.ToString());

                file = await _Elements[index].ScanFile.CopyAsync(folder, _Elements[index].ScanFile.Name, NameCollisionOption.GenerateUniqueName);
            }
            catch (Exception)
            {
                throw;
            }

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
            {
                _Elements.Insert(index + 1, new ScanResultElement(file, _Elements[index].FutureAccessListIndex));
                NumberOfPages += 1;
            });

            RefreshItemDescriptors();
            await _Elements[index + 1].GetImageAsync();

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                try
                {
                    List<StorageFile> filesNumbering = new List<StorageFile>();
                    for (int i = index + 1; i < _Elements.Count; i++)
                    {
                        await _Elements[i].ScanFile.RenameAsync("_" + _Elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                        filesNumbering.Add(_Elements[i].ScanFile);
                    }
                    await PrepareNewConversionFiles(filesNumbering, index + 1);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to generate PDF after duplicating index {Index}. Attempting to get rid of copy.", index);
                    Crashes.TrackError(exc);
                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.RemoveAt(index + 1));
                    NumberOfPages = _Elements.Count;
                    try { await file.DeleteAsync(); } catch (Exception e) { LogService?.Log.Error(e, "Undo failed as well."); }
                    RefreshItemDescriptors();
                    throw;
                }
                await GeneratePDF();
            }
        }
    }
}
