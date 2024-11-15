﻿using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.FileNaming;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
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
        private readonly IHelperService HelperService = Ioc.Default.GetRequiredService<IHelperService>();
        private readonly IPdfService PdfService = Ioc.Default.GetService<IPdfService>();

        public static event EventHandler PerformedAutomaticRotation;

        private ObservableCollection<ScanResultElement> _Elements = new ObservableCollection<ScanResultElement>();
        public ObservableCollection<ScanResultElement> Elements
        {
            get => _Elements;
        }

        private ImageScannerFormat _ScanResultFormat;
        public ImageScannerFormat ScanResultFormat
        {
            get => _ScanResultFormat;
            set
            {
                SetProperty(ref _ScanResultFormat, value);
                ScanResultFormatString = ConvertImageScannerFormatToString(value);
            }
        }

        private string _ScanResultFormatString;
        public string ScanResultFormatString
        {
            get => _ScanResultFormatString;
            set => SetProperty(ref _ScanResultFormatString, value);
        }

        public ImageScannerFormat PagesFormat;
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
        private ScanResult(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, int futureAccessListIndexStart,
            bool isDocument, ImageScannerFormat format)
        {
            LogService?.Log.Information("ScanResult constructor [futureAccessListIndexStart={Index}]", futureAccessListIndexStart);
            int futureAccessListIndex = futureAccessListIndexStart;

            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;

                _Elements.Add(new ScanResultElement(file, futureAccessListIndex, isDocument));
                NumberOfPages = _Elements.Count;

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                futureAccessListIndex += 1;
            }

            ScanResultFormat = format;
            OriginalTargetFolder = targetFolder;
            RefreshItemDescriptors();
            _Elements.CollectionChanged += (x, y) => PagesChanged?.Invoke(this, y);
        }

        /// <summary>
        ///     Create a <see cref="ScanResult"/> without conversion.
        /// </summary>
        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList,
            StorageFolder targetFolder, int futureAccessListIndexStart, ScanOptions scanOptions, DiscoveredScanner scanner,
            ScanAndEditingProgress progress)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
            IAutoRotatorService autoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();
            IHelperService helperService = Ioc.Default.GetService<IHelperService>();
            IAppCenterService appCenterService = Ioc.Default.GetService<IAppCenterService>();

            logService?.Log.Information("Creating a ScanResult without any conversion from {Num} pages.", fileList.Count);
            progress.State = ProgressState.Processing;

            // construct ScanResult
            progress.Progress = 0;
            List<StorageFile> files = fileList.ToList();
            for (int i = 0; i < files.Count; i++)
            {
                files[i] = await helperService.MoveFileToFolderAsync(fileList[i], targetFolder, GetInitialName(scanOptions, scanner), false);
                progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / files.Count * 100.0));
            }
            progress.Progress = null;

            ScanResult result = new ScanResult(files, targetFolder, futureAccessListIndexStart, false,
                (ImageScannerFormat)ConvertFormatStringToImageScannerFormat(fileList[0].FileType));
            result.PagesFormat = result.ScanResultFormat;

            // automatic rotation
            if ((bool)settingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                progress.State = ProgressState.AutomaticRotation;

                // collect recommendations
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < result.Elements.Count; i++)
                {
                    ScanResultElement element = result.Elements[i];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    BitmapRotation recommendation = await autoRotatorService.TryGetRecommendedRotationAsync(element.ScanFile, (ImageScannerFormat)format);
                    if (recommendation != BitmapRotation.None)
                    {
                        instructions.Add(new Tuple<int, BitmapRotation>(i, recommendation));
                    }
                    GC.Collect();
                    progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / result.Elements.Count * 100.0));
                }

                // apply recommendations
                progress.Progress = 100;
                if (instructions.Count > 0)
                {
                    await result.RotateScansAsync(instructions);
                    PerformedAutomaticRotation?.Invoke(result, EventArgs.Empty);
                }
                progress.Progress = null;

                // analytics
                foreach (var instruction in instructions)
                {
                    result.Elements[instruction.Item1].IsAutoRotatedAndUnchanged = true;
                    appCenterService.TrackEvent(AppCenterEvent.AutoRotatedPage, new Dictionary<string, string> {
                            { "Rotation", instruction.Item2.ToString() },
                        });
                }
            }

            // create previews
            progress.State = ProgressState.Finishing;
            await result.GetImagesAsync();
            logService?.Log.Information("ScanResult created.");
            GC.Collect();
            return result;
        }

        /// <summary>
        ///     Create a <see cref="ScanResult"/> with conversion to <paramref name="targetFormat"/>.
        /// </summary>
        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder,
            ImageScannerFormat targetFormat, int futureAccessListIndexStart, ScanOptions scanOptions, DiscoveredScanner scanner,
            ScanAndEditingProgress progress)
        {
            ILogService logService = Ioc.Default.GetService<ILogService>();
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
            IAutoRotatorService autoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();
            IAppCenterService appCenterService = Ioc.Default.GetService<IAppCenterService>();

            logService?.Log.Information("Creating a ScanResult with conversion from {SourceFormat} to {TargetFormat} from {Num} pages.",
                fileList[0].FileType, targetFormat, fileList.Count);

            // construct ScanResult
            ScanResult result;
            if (targetFormat == ImageScannerFormat.Pdf)
            {
                result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart, true, targetFormat);
                string pdfName = GetInitialName(scanOptions, scanner);

                // convert all source files to JPG for optimized size
                progress.State = ProgressState.PdfGeneration;
                IAppDataService appDataService = Ioc.Default.GetService<IAppDataService>();
                for (int i = 0; i < result.Elements.Count; i++)
                {
                    if (ConvertFormatStringToImageScannerFormat(result.Elements[i].ScanFile.FileType) != ImageScannerFormat.Jpeg)
                    {
                        await result.ConvertPageAsync(i, ImageScannerFormat.Jpeg, appDataService.FolderConversion);
                    }
                }

                await PrepareNewConversionFiles(result.GetImageFiles(), 0);
                await result.GeneratePDFAsync(pdfName);
            }
            else
            {
                progress.State = ProgressState.Processing;
                progress.Progress = 0;
                result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart, false, targetFormat);
                for (int i = 0; i < result.NumberOfPages; i++)
                {
                    await result.ConvertPageAsync(i, targetFormat, targetFolder, GetInitialName(scanOptions, scanner));
                    progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / result.NumberOfPages * 100.0));
                }
                result.RefreshItemDescriptors();
                progress.Progress = null;
            }
            result.ScanResultFormat = targetFormat;
            result.PagesFormat = (ImageScannerFormat)ConvertFormatStringToImageScannerFormat(result.GetImageFile(0).FileType);

            // automatic rotation
            if ((bool)settingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                progress.State = ProgressState.AutomaticRotation;

                // collect recommendations
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < result.Elements.Count; i++)
                {
                    ScanResultElement element = result.Elements[i];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    BitmapRotation recommendation = await autoRotatorService.TryGetRecommendedRotationAsync(element.ScanFile, (ImageScannerFormat)format);
                    if (recommendation != BitmapRotation.None)
                    {
                        instructions.Add(new Tuple<int, BitmapRotation>(i, recommendation));
                    }
                    progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / result.Elements.Count * 100.0));
                }

                // apply recommendations
                progress.Progress = 100;
                if (instructions.Count > 0)
                {
                    await result.RotateScansAsync(instructions);
                    PerformedAutomaticRotation?.Invoke(result, EventArgs.Empty);
                }
                progress.Progress = null;

                // analytics
                foreach (var instruction in instructions)
                {
                    result.Elements[instruction.Item1].IsAutoRotatedAndUnchanged = true;
                    appCenterService.TrackEvent(AppCenterEvent.AutoRotatedPage, new Dictionary<string, string> {
                            { "Rotation", instruction.Item2.ToString() },
                        });
                }
            }

            // create previews
            progress.State = ProgressState.Finishing;
            await result.GetImagesAsync();

            logService?.Log.Information("ScanResult created.");
            GC.Collect();
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
        public async Task ConvertPageAsync(int index, ImageScannerFormat targetFormat, StorageFolder targetFolder, string desiredName)
        {
            LogService?.Log.Information("Conversion of index {Index} into {TargetFormat} with folder requested.", index, targetFormat);
            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Conversion of index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for conversion.");
            }

            StorageFile sourceFile = _Elements[index].ScanFile;

            // check desired name
            if (desiredName == null)
            {
                desiredName = sourceFile.DisplayName + ConvertImageScannerFormatToString(targetFormat);
            }

            // convert
            string newName;
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

                        BitmapEncoder encoder = await HelperService.CreateOptimizedBitmapEncoderAsync(targetFormat, stream);
                        encoder.SetSoftwareBitmap(softwareBitmap);

                        // save/encode the file in the target format
                        try { await encoder.FlushAsync(); }
                        catch (Exception exc)
                        {
                            AppCenterService.TrackError(exc);
                            LogService?.Log.Error(exc, "Conversion of the scan failed.");
                            throw;
                        }
                    }

                    // move file to the correct folder
                    newName = (await HelperService.MoveFileToFolderAsync(sourceFile, targetFolder, desiredName, false)).Name;
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


        public Task ConvertPageAsync(int index, ImageScannerFormat targetFormat, StorageFolder targetFolder)
        {
            return ConvertPageAsync(index, targetFormat, targetFolder, null);
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
            AppCenterService.TrackEvent(AppCenterEvent.RotatePages, new Dictionary<string, string> {
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

                                BitmapEncoder encoder = await HelperService.CreateOptimizedBitmapEncoderAsync(PagesFormat, fileStream);
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

                        // analytics
                        if (Elements[instruction.Item1].IsAutoRotatedAndUnchanged)
                        {
                            Elements[instruction.Item1].IsAutoRotatedAndUnchanged = false;
                            AppCenterService.TrackEvent(AppCenterEvent.CorrectedAutoRotation);
                        }
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
        ///     Crops the selected scans.
        /// </summary>
        /// <param name="indices">The indices of the scan that the crop is to be applied to.</param>
        /// <param name="cropRegion">The desired crop region.</param>
        /// <param name="asCopy">Whether to save the result as a copy.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Applying the crop failed.</exception>
        public async Task CropScansAsync(List<int> indices, Rect cropRegion, bool asCopy)
        {
            if (asCopy)
            {
                AppCenterService?.TrackEvent(AppCenterEvent.CropAsCopy);
            }
            else
            {
                AppCenterService?.TrackEvent(indices.Count == 1 ? AppCenterEvent.Crop : AppCenterEvent.CropMultiple);
            }
            LogService?.Log.Information("Requested crop for indices {@Indices} with {AsCopy}.", indices, asCopy);

            // check indices
            foreach (int index in indices)
            {
                if (!IsValidIndex(index))
                {
                    LogService?.Log.Error("Crop for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index for crop.");
                }
            }

            // crop images
            foreach (int index in indices)
            {
                // loosely based on CropImageAsync(...) used by the ImageCropper control
                LogService?.Log.Information("Cropping at {Index}", index);

                cropRegion.X = Math.Max(cropRegion.X, 0);
                cropRegion.Y = Math.Max(cropRegion.Y, 0);
                var x = (uint)Math.Floor(cropRegion.X);
                var y = (uint)Math.Floor(cropRegion.Y);
                var width = (uint)Math.Floor(cropRegion.Width);
                var height = (uint)Math.Floor(cropRegion.Height);

                // get source file
                StorageFile sourceFile = GetImageFile(index);

                // get target file
                StorageFile targetFile;
                if (asCopy)
                {
                    if (ScanResultFormat == ImageScannerFormat.Pdf)
                    {
                        targetFile = await AppDataService.FolderConversion.CreateFileAsync(
                            _Elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                    }
                    else
                    {
                        StorageFolder targetFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + _Elements[index].FutureAccessListIndex.ToString());
                        targetFile = await targetFolder.CreateFileAsync(
                            _Elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                    }
                }
                else
                {
                    targetFile = sourceFile;
                }

                using (IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    using (IRandomAccessStream targetStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await HelperService.CreateOptimizedBitmapEncoderAsync(PagesFormat, targetStream);
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
                }


                if (asCopy)
                {
                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        _Elements.Insert(index + 1, new ScanResultElement(targetFile, _Elements[index].FutureAccessListIndex,
                            _Elements[index].IsPartOfDocument));
                        NumberOfPages += 1;
                    });

                    RefreshItemDescriptors();
                    await _Elements[index + 1].GetImageAsync();
                }
                else
                {
                    // refresh cached image, delete image without rotation and reset rotation
                    _Elements[index].CachedImage = null;
                    await _Elements[index].GetImageAsync();
                    if (_Elements[index].ImageWithoutRotation != null)
                    {
                        await _Elements[index].ImageWithoutRotation.DeleteAsync();
                    }
                    _Elements[index].ImageWithoutRotation = null;
                    _Elements[index].CurrentRotation = BitmapRotation.None;
                }
            }

            // if necessary, generate PDF
            if (ScanResultFormat == ImageScannerFormat.Pdf)
            {
                if (asCopy)
                {
                    // prepare data for conversion
                    int firstIndex = indices.OrderBy((x) => x).ElementAt(0);
                    try
                    {
                        List<StorageFile> filesNumbering = new List<StorageFile>();
                        for (int i = firstIndex + 1; i < _Elements.Count; i++)
                        {
                            await _Elements[i].ScanFile.RenameAsync("_" + _Elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                            filesNumbering.Add(_Elements[i].ScanFile);
                        }
                        await PrepareNewConversionFiles(filesNumbering, firstIndex + 1);
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Error(exc, "Failed to generate PDF after cropping as copy.");
                        AppCenterService.TrackError(exc);
                        throw;
                    }
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
            AppCenterService.TrackEvent(AppCenterEvent.DeletePages);
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
            AppCenterService.TrackEvent(AppCenterEvent.DeletePage);
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
            AppCenterService.TrackEvent(AppCenterEvent.DrawOnPage);
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
            AppCenterService.TrackEvent(AppCenterEvent.DrawOnPageAsCopy);
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
                if (ScanResultFormat == ImageScannerFormat.Pdf) folder = AppDataService.FolderConversion;
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
                _Elements.Insert(index + 1, new ScanResultElement(file, _Elements[index].FutureAccessListIndex,
                    _Elements[index].IsPartOfDocument));
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
                    AppCenterService.TrackError(exc);
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
            ImageScannerFormat format = PagesFormat;

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
        public async Task AddFiles(IEnumerable<StorageFile> files, ImageScannerFormat? targetFormat, StorageFolder targetFolder,
            int futureAccessListIndexStart, ScanMergeConfig mergeConfig, ScanOptions scanOptions, DiscoveredScanner scanner,
            ScanAndEditingProgress progress)
        {
            LogService?.Log.Information("Adding {Num} files, the target format is {Format}.", files.Count(), targetFormat);
            int futureAccessListIndex = futureAccessListIndexStart;
            progress.State = ProgressState.Processing;

            if (targetFormat == null || targetFormat == ImageScannerFormat.Pdf)
            {
                // no conversion (but perhaps generation later on), just add files for now
                if (targetFormat == ImageScannerFormat.Pdf)
                {
                    // number files
                    await PrepareNewConversionFiles(files, NumberOfPages);
                }

                progress.Progress = 0;
                for (int i = 0; i < files.Count(); i++)
                {
                    StorageFile file = files.ElementAt(i);
                    int insertIndex;

                    if (mergeConfig == null)
                    {
                        insertIndex = _Elements.Count;
                    }
                    else
                    {
                        insertIndex = GetNewIndexAccordingToMergeConfig(mergeConfig, i, files.Count(), true);
                    }

                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.Insert(
                        insertIndex,
                        new ScanResultElement(file, futureAccessListIndex, targetFormat == ImageScannerFormat.Pdf)));

                    NumberOfPages = _Elements.Count;

                    if (targetFormat == ImageScannerFormat.Pdf && ConvertFormatStringToImageScannerFormat(file.FileType) != ImageScannerFormat.Jpeg)
                    {
                        // convert to JPG for optimized size
                        await ConvertPageAsync(insertIndex, ImageScannerFormat.Jpeg, AppDataService.FolderConversion);
                    }

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                        if (targetFormat != ImageScannerFormat.Pdf)
                        {
                            // move file to the correct folder
                            await HelperService.MoveFileToFolderAsync(file, targetFolder, GetInitialName(scanOptions, scanner), false);
                        }
                    }
                    progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / files.Count() * 100.0));
                }
            }
            else
            {
                int numberOfPagesOld = NumberOfPages;

                // immediate conversion necessary
                foreach (StorageFile file in files)
                {
                    if (file == null) continue;

                    await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, () => _Elements.Add(
                        new ScanResultElement(file, futureAccessListIndex, false)));
                    NumberOfPages = _Elements.Count;

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                    }
                }

                Task[] conversionTasks = new Task[files.Count()];
                try
                {
                    for (int i = numberOfPagesOld; i < NumberOfPages; i++)
                    {
                        await ConvertPageAsync(i, (ImageScannerFormat)targetFormat, targetFolder, GetInitialName(scanOptions, scanner));
                    }
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Failed to convert at least one new page. All new pages will be discarded.");

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
            progress.Progress = null;

            // if necessary, finalize file numbering and generate PDF now
            if (ScanResultFormat == ImageScannerFormat.Pdf) await ApplyElementOrderToFilesAsync();

            // automatic rotation
            if ((bool)SettingsService.GetSetting(AppSetting.SettingAutoRotate))
            {
                progress.State = ProgressState.AutomaticRotation;

                // collect recommendations
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                for (int i = 0; i < files.Count(); i++)
                {
                    int indexInResult;
                    if (mergeConfig == null)
                    {
                        indexInResult = i + NumberOfPages - files.Count();
                    }
                    else
                    {
                        indexInResult = GetNewIndexAccordingToMergeConfig(mergeConfig, i, files.Count());
                    }

                    ScanResultElement element = Elements[indexInResult];
                    ImageScannerFormat? format = ConvertFormatStringToImageScannerFormat(element.ScanFile.FileType);
                    BitmapRotation recommendation = await AutoRotatorService.TryGetRecommendedRotationAsync(element.ScanFile, (ImageScannerFormat)format);
                    if (recommendation != BitmapRotation.None)
                    {
                        instructions.Add(new Tuple<int, BitmapRotation>(indexInResult, recommendation));
                    }
                    progress.Progress = Convert.ToInt32(Math.Ceiling((double)i / files.Count() * 100.0));
                }

                // apply recommendations
                progress.Progress = 100;
                if (instructions.Count > 0)
                {
                    await RotateScansAsync(instructions);
                    PerformedAutomaticRotation?.Invoke(this, EventArgs.Empty);
                }
                progress.Progress = null;

                // analytics
                foreach (var instruction in instructions)
                {
                    Elements[instruction.Item1].IsAutoRotatedAndUnchanged = true;
                    AppCenterService.TrackEvent(AppCenterEvent.AutoRotatedPage, new Dictionary<string, string> {
                            { "Rotation", instruction.Item2.ToString() },
                        });
                }
            }

            // generate new previews and descriptors
            progress.State = ProgressState.Finishing;
            if (mergeConfig == null)
            {
                for (int i = NumberOfPages - files.Count(); i < NumberOfPages; i++)
                {
                    await GetImageAsync(i);
                    _Elements[i].ItemDescriptor = GetDescriptorForIndex(i);
                }
            }
            else
            {
                for (int i = 0; i < files.Count(); i++)
                {
                    int mergeIndex = GetNewIndexAccordingToMergeConfig(mergeConfig, i, files.Count());

                    await GetImageAsync(mergeIndex);
                    _Elements[mergeIndex].ItemDescriptor = GetDescriptorForIndex(mergeIndex);
                }
            }

            GC.Collect();
        }


        /// <summary>
        ///     Adds the specified files to this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public Task AddFiles(IEnumerable<StorageFile> files, ImageScannerFormat? targetFormat, int futureAccessListIndexStart,
            ScanMergeConfig mergeConfig, ScanOptions scanOptions, DiscoveredScanner scanner, ScanAndEditingProgress progress)
        {
            return AddFiles(files, targetFormat, OriginalTargetFolder, futureAccessListIndexStart, mergeConfig, scanOptions,
                scanner, progress);
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
            LogService?.Log.Information("Requested opening with of index {Index} with app '{DisplayName}'.", index, appInfo.DisplayInfo.DisplayName);

            // check index
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Opening with of index {Index} requested, but there are only {Count} pages.", index, _Elements.Count);
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
            LogService?.Log.Information("Requested opening with of document with {App}.", appInfo.DisplayInfo.DisplayName);

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

            if (Pdf == null)
            {
                LogService?.Log.Information("PDF doesn't exist yet.");
                Pdf = await PdfService.GeneratePdfAsync(fileName, OriginalTargetFolder, false);
            }
            else
            {
                LogService?.Log.Information("PDF already exists.");
                Pdf = await PdfService.GeneratePdfAsync(Pdf.Name, OriginalTargetFolder, true);
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
            IAppDataService appDataService = Ioc.Default.GetService<IAppDataService>();

            try
            {
                int nextNumber = startIndex;
                foreach (StorageFile file in files)
                {
                    await file.MoveAsync(appDataService.FolderConversion, nextNumber + file.FileType);
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
        ///     Calculates the index where a new page will be placed in the <see cref="ScanResult"/>. If the insertion is
        ///     reversed, the index is shifted accordingly to accomodate inserting pages starting from the back unless
        ///     <paramref name="forInsertion"/> is false.
        /// </summary>
        /// <param name="indexOfNewPage">Index of the new page among all new pages.</param>
        /// <param name="totalNewPages">The number of pages being added in total.</param>
        internal static int GetNewIndexAccordingToMergeConfig(ScanMergeConfig mergeConfig, int indexOfNewPage, int totalNewPages,
            bool forInsertion = false)
        {
            if (!mergeConfig.InsertReversed)
            {
                // insert normally
                if (indexOfNewPage < mergeConfig.InsertIndices.Count)
                {
                    return mergeConfig.InsertIndices[indexOfNewPage];
                }
                else
                {
                    // surplus page
                    return mergeConfig.SurplusPagesIndex + indexOfNewPage - mergeConfig.InsertIndices.Count;
                }
            }
            else
            {
                // insert reversed
                if (forInsertion)
                {
                    if (totalNewPages - 1 - indexOfNewPage < mergeConfig.InsertIndices.Count)
                    {
                        return mergeConfig.InsertIndices[totalNewPages - 1 - indexOfNewPage] - (totalNewPages - 1 - indexOfNewPage);
                    }
                    else
                    {
                        // surplus page
                        return mergeConfig.SurplusPagesIndex - mergeConfig.InsertIndices.Count;
                    }
                }
                else
                {
                    if (totalNewPages - 1 - indexOfNewPage < mergeConfig.InsertIndices.Count)
                    {
                        return mergeConfig.InsertIndices[totalNewPages - 1 - indexOfNewPage];
                    }
                    else
                    {
                        // surplus page
                        return mergeConfig.SurplusPagesIndex + totalNewPages - 1 - indexOfNewPage - mergeConfig.InsertIndices.Count;
                    }
                }
            }
        }


        /// <summary>
        ///     Applies the order of <see cref="_Elements"/> to the PDF file.
        /// </summary>
        public async Task ApplyElementOrderToFilesAsync()
        {
            LogService?.Log.Information("Applying element order to files.");

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

            // generate the updated PDF file
            await GeneratePDF();
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
        ///     Constructs the initial name for a file according to the file naming settings.
        /// </summary>
        protected static string GetInitialName(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();

            try
            {
                FileNamingPattern pattern;
                switch ((SettingFileNamingPattern)settingsService.GetSetting(AppSetting.SettingFileNamingPattern))
                {
                    default:
                    case SettingFileNamingPattern.DateTime:
                        pattern = FileNamingStatics.DateTimePattern;
                        break;
                    case SettingFileNamingPattern.Date:
                        pattern = FileNamingStatics.DatePattern;
                        break;
                    case SettingFileNamingPattern.Custom:
                        pattern = new FileNamingPattern((string)settingsService.GetSetting(AppSetting.CustomFileNamingPattern));
                        break;
                }

                return pattern.GenerateResult(scanOptions, scanner);
            }
            catch (Exception)
            {
                return FileNamingStatics.DateTimePattern.GenerateResult(scanOptions, scanner);
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
                if (ScanResultFormat == ImageScannerFormat.Pdf) folder = AppDataService.FolderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + _Elements[index].FutureAccessListIndex.ToString());

                file = await _Elements[index].ScanFile.CopyAsync(folder, _Elements[index].ScanFile.Name, NameCollisionOption.GenerateUniqueName);
            }
            catch (Exception)
            {
                throw;
            }

            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
            {
                _Elements.Insert(index + 1, new ScanResultElement(file, _Elements[index].FutureAccessListIndex,
                    _Elements[index].IsPartOfDocument));
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
                    AppCenterService.TrackError(exc);
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
        ///     Exports a single page of the scan.
        /// </summary>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task ExportScanAsync(int index, StorageFolder targetFolder, string name)
        {
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Export for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for exporting file.");
            }

            await _Elements[index].ScanFile.CopyAsync(targetFolder, name, NameCollisionOption.ReplaceExisting);
        }

        /// <summary>
        ///     Exports a single page of the scan.
        /// </summary>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task ExportScanAsync(int index, StorageFile targetFile, string name)
        {
            if (!IsValidIndex(index))
            {
                LogService?.Log.Error("Export for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for exporting file.");
            }

            await _Elements[index].ScanFile.CopyAndReplaceAsync(targetFile);
        }

        /// <summary>
        ///     Exports a multiple page of the scan.
        /// </summary>
        /// <param name="index">The desired scan's indices.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task ExportScansAsync(List<int> indices, StorageFolder targetFolder)
        {
            // check indices
            foreach (int index in indices)
            {
                if (!IsValidIndex(index))
                {
                    LogService?.Log.Error("Export for index {Index} requested, but there are only {Num} pages.", index, _Elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index for export.");
                }
            }

            // export files
            foreach (int index in indices)
            {
                await _Elements[index].ScanFile.CopyAsync(targetFolder, Pdf.DisplayName + _Elements[index].ScanFile.FileType, NameCollisionOption.GenerateUniqueName);
            }
        }
    }
}