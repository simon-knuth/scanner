using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

using static Enums;
using static Globals;
using static Utilities;


namespace Scanner
{
    class ScanResult
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ObservableCollection<ScanResultElement> elements = new ObservableCollection<ScanResultElement>();
        public SupportedFormat scanResultFormat;
        public StorageFile pdf = null;
        public readonly StorageFolder originalTargetFolder;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ScanResult(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, int futureAccessListIndexStart, bool displayFolder)
        {
            log.Information("ScanResult constructor [futureAccessListIndexStart={Index}|displayFolder={Folder}]", futureAccessListIndexStart, displayFolder);
            int futureAccessListIndex = futureAccessListIndexStart;
            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;

                if (displayFolder) elements.Add(new ScanResultElement(file, futureAccessListIndex, targetFolder.DisplayName));
                else elements.Add(new ScanResultElement(file, futureAccessListIndex, null));

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                futureAccessListIndex += 1;
            }
            scanResultFormat = (SupportedFormat)ConvertFormatStringToSupportedFormat(elements[0].ScanFile.FileType);
            originalTargetFolder = targetFolder;
            RefreshItemDescriptors();
            elements.CollectionChanged += (x, y) => PagesChanged?.Invoke(this, y);
        }

        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, int futureAccessListIndexStart, bool displayFolder)
        {
            log.Information("Creating a ScanResult without any conversion from {Num} pages.", fileList.Count);
            Task[] moveTasks = new Task[fileList.Count];
            for (int i = 0; i < fileList.Count; i++)
            {
                moveTasks[i] = MoveFileToFolderAsync(fileList[i], targetFolder, RemoveNumbering(fileList[i].Name), false);
            }
            await Task.WhenAll(moveTasks);

            ScanResult result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart, displayFolder);
            if (settingAppendTime) try { await result.SetInitialNamesAsync(); } catch (Exception) { }
            await result.GetImagesAsync();
            log.Information("ScanResult created.");
            return result;
        }

        public async static Task<ScanResult> CreateAsync(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, SupportedFormat targetFormat, int futureAccessListIndexStart, bool displayFolder)
        {
            log.Information("Creating a ScanResult with conversion from {SourceFormat} to {TargetFormat} from {Num} pages.",
                fileList[0].FileType, targetFormat, fileList.Count);
            ScanResult result = new ScanResult(fileList, targetFolder, futureAccessListIndexStart, displayFolder);

            if (targetFormat == SupportedFormat.PDF)
            {
                string pdfName = fileList[0].DisplayName + ".pdf";
                await PrepareNewConversionFiles(fileList, 0);
                await result.GeneratePDFAsync(pdfName);
            }
            else
            {
                Task[] conversionTasks = new Task[result.GetTotalNumberOfPages()];
                for (int i = 0; i < result.GetTotalNumberOfPages(); i++)
                {
                    conversionTasks[i] = result.ConvertScanAsync(i, targetFormat, targetFolder);
                }
                await Task.WhenAll(conversionTasks);
            }

            result.scanResultFormat = targetFormat;
            if (settingAppendTime) try { await result.SetInitialNamesAsync(); } catch (Exception) { }
            await result.GetImagesAsync();
            log.Information("ScanResult created.");
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
        ///     Gets the number of individual scans in this instance.
        /// </summary>
        public int GetTotalNumberOfPages()
        {
            return elements.Count;
        }


        /// <summary>
        ///     Gets all files of the individual scans in this instance.
        /// </summary>
        public List<StorageFile> GetImageFiles()
        {
            log.Information("All image files of the scan result have been requested.");
            List<StorageFile> files = new List<StorageFile>();
            foreach (ScanResultElement element in elements)
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
                log.Error("Image file for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            }
            else return elements.ElementAt(index).ScanFile;
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
                log.Error("Thumbnail for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            }
            else return elements.ElementAt(index).Thumbnail;
        }


        /// <summary>
        ///     Gets an image preview of every individual scan in this instance.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<List<BitmapImage>> GetImagesAsync()
        {
            log.Information("BitmapImages of all pages have been requested.");
            List<BitmapImage> previews = new List<BitmapImage>();
            List<Task<BitmapImage>> tasks = new List<Task<BitmapImage>>();

            // kick off conversion for all files
            for (int i = 0; i < elements.Count; i++)
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
        ///     Parts of this method are required run on the UI thread.
        /// </remarks>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<BitmapImage> GetImageAsync(int index)
        {
            log.Information("Image for index {Index} requested.", index);
            if (!IsValidIndex(index))
            {
                log.Error("Image for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for preview.");
            }

            // use cached image if possible
            if (elements[index].CachedImage != null)
            {
                log.Information("Returning a cached image.");
                return elements[index].CachedImage;
            }

            // create new bitmap
            StorageFile sourceFile = elements[index].ScanFile;
            BitmapImage bmp = null;
            int attempt = 0;
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, async () =>
            {
                using (IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                {
                    switch (ConvertFormatStringToSupportedFormat(sourceFile.FileType))
                    {
                        case SupportedFormat.JPG:
                        case SupportedFormat.PNG:
                        case SupportedFormat.TIF:
                        case SupportedFormat.BMP:
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

                                    log.Warning(e, "Opening the file stream of page at index {Index} failed, retrying in 500ms.", index);
                                    await Task.Delay(500);
                                    attempt++;
                                }
                            }
                            break;

                        case SupportedFormat.PDF:
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

                                    log.Warning(e, "Opening the file stream of page at index {Index} failed, retrying in 500ms.", index);
                                    await Task.Delay(500);
                                    attempt++;
                                }
                            }
                            break;

                        case SupportedFormat.XPS:
                        case SupportedFormat.OpenXPS:
                            throw new NotImplementedException("Can not generate bitmap from (O)XPS.");

                        default:
                            throw new ApplicationException("Could not determine file type for generating a bitmap.");
                    }

                    // save image to cache
                    elements[index].CachedImage = bmp;

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
                            elements[index].Thumbnail = thumbnail;
                            break;
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4)
                            {
                                log.Error(e, "Couldn't generate thumbnail of page at index {Index}", index);
                                return;
                            }

                            log.Warning(e, "Generating the thumbnail of page at index {Index} failed, retrying in 500ms.", index);
                            await Task.Delay(500);
                            attempt++;
                        }
                    }
                }
            });
            log.Information("Returning a newly generated image.");
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
        public async Task ConvertScanAsync(int index, SupportedFormat targetFormat, StorageFolder targetFolder)
        {
            log.Information("Conversion of index {Index} into {TargetFormat} requested.", index, targetFormat);
            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Conversion of index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for conversion.");
            }

            // convert
            StorageFile sourceFile = elements[index].ScanFile;
            string newName, newNameWithoutNumbering;
            switch (targetFormat)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                    // open image file, decode it and prepare an encoder with the target image format
                    using (IRandomAccessStream stream = await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                        Guid encoderId = GetBitmapEncoderId(targetFormat);

                        BitmapEncoder encoder = null;
                        if (targetFormat == SupportedFormat.JPG)
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
                            log.Error(exc, "Conversion of the scan failed.");
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

                case SupportedFormat.PDF:
                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    log.Error("Requested conversion for unsupported format.");
                    throw new NotImplementedException("Can not convert to (O)XPS.");

                default:
                    log.Error("Requested conversion without sepcifying format.");
                    throw new ApplicationException("Could not determine target file type for conversion.");
            }

            // refresh file
            elements[index].ScanFile = await targetFolder.GetFileAsync(newName);
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
            log.Information("Requested to move file to folder. [desiredName={Name}|replaceExisting={Replace}]", desiredName, replaceExisting);
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
            log.Information("Received {@Instructions} for rotations.", instructions);

            // check indices and rotations
            foreach (var instruction in instructions)
            {
                if (!IsValidIndex(instruction.Item1))
                {
                    log.Error("Rotation for index {Index} requested, but there are only {Num} pages. Aborting all rotations.", instruction.Item1, elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index " + instruction.Item1 + " for rotation.");
                }
                if (instruction.Item2 == BitmapRotation.None) return;
            }

            // rotate and make sure that consecutive rotations are lossless
            switch (scanResultFormat)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                case SupportedFormat.PDF:
                    foreach (var instruction in instructions)
                    {
                        try
                        {
                            using (IRandomAccessStream fileStream = await elements[instruction.Item1].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                BitmapDecoder decoder;
                                if (elements[instruction.Item1].ImageWithoutRotation == null)
                                {
                                    elements[instruction.Item1].ImageWithoutRotation = await GetImageFile(instruction.Item1)
                                        .CopyAsync(folderWithoutRotation, GetImageFile(instruction.Item1).Name, NameCollisionOption.ReplaceExisting);
                                    decoder = await BitmapDecoder.CreateAsync(fileStream);
                                }
                                else
                                {
                                    using (IRandomAccessStream bitmapStream = await elements[instruction.Item1].ImageWithoutRotation.OpenReadAsync())
                                    {
                                        decoder = await BitmapDecoder.CreateAsync(bitmapStream);
                                    }
                                }

                                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                                Guid encoderId = GetBitmapEncoderId(elements[instruction.Item1].ScanFile.Name.Split(".")[1]);

                                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, fileStream);
                                encoder.SetSoftwareBitmap(softwareBitmap);

                                encoder.BitmapTransform.Rotation = CombineRotations(instruction.Item2, elements[instruction.Item1].CurrentRotation);

                                await encoder.FlushAsync();
                                elements[instruction.Item1].CurrentRotation = encoder.BitmapTransform.Rotation;
                            }
                        }
                        catch (Exception e)
                        {
                            throw new ApplicationException("Rotation failed.", e);
                        }
                        finally
                        {
                            // delete image from cache
                            elements[instruction.Item1].CachedImage = null;
                        }
                    }

                    if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
                    break;

                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    log.Error("Requested rotation for unsupported format {Format}.", scanResultFormat);
                    throw new ArgumentException("Rotation not supported for PDF, XPS or OXPS.");

                default:
                    log.Error("Requested rotation for unknown file type.");
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
        /// <param name="newName">The desired full file name for the scan.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task RenameScanAsync(int index, string newName)
        {
            Analytics.TrackEvent("Rename page");
            log.Information("Renaming index {Index} to {Name}.", index, newName);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Rename for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for rename.");
            }

            // check name is different
            if (elements[index].ScanFile.Name == newName) return;

            // rename
            await elements[index].RenameFileAsync(newName);
        }


        /// <summary>
        ///     Renames the scan. Only for scans that are combined into a single document (e.g. PDF).
        /// </summary>
        /// <param name="index">The index of the scan that's to be renamed.</param>
        /// <param name="newName">The desired full file name for the scan.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task RenameScanAsync(string newName)
        {
            Analytics.TrackEvent("Rename PDF");

            // check type
            if (scanResultFormat != SupportedFormat.PDF)
            {
                log.Error("Attempted to rename entire file for non-PDF.");
                throw new ApplicationException("ScanResult represents more than one file.");
            }

            // check name is different
            if (pdf.Name == newName) return;

            // rename
            await pdf.RenameAsync(newName, NameCollisionOption.FailIfExists);
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
            log.Information("Requested crop for index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Crop for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for crop.");
            }

            // save changes to original file
            IRandomAccessStream stream = null;
            try
            {
                stream = await elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(elements[index].ScanFile), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            stream.Dispose();

            // delete cached image, delete image without rotation and reset rotation
            elements[index].CachedImage = null;
            if (elements[index].ImageWithoutRotation != null)
            {
                await elements[index].ImageWithoutRotation.DeleteAsync();
            }
            elements[index].ImageWithoutRotation = null;
            elements[index].CurrentRotation = BitmapRotation.None;

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
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
            log.Information("Requested crop as copy for index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Crop as copy index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for crop.");
            }

            // save crop as new file
            StorageFile file;
            IRandomAccessStream stream = null;
            try
            {
                StorageFolder folder = null;
                if (scanResultFormat == SupportedFormat.PDF) folder = folderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + elements[index].FutureAccessListIndex.ToString());

                file = await folder.CreateFileAsync(elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(elements[index].ScanFile), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
            stream.Dispose();

            elements.Insert(index + 1, new ScanResultElement(file, elements[index].FutureAccessListIndex, elements[index].DisplayedFolder));

            RefreshItemDescriptors();

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF)
            {

                try
                {
                    List<StorageFile> filesNumbering = new List<StorageFile>();
                    for (int i = index + 1; i < elements.Count; i++)
                    {
                        await elements[i].ScanFile.RenameAsync("_" + elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                        filesNumbering.Add(elements[i].ScanFile);
                    }
                    await PrepareNewConversionFiles(filesNumbering, index + 1);
                }
                catch (Exception exc)
                {
                    log.Error(exc, "Failed to generate PDF after cropping index {Index} as copy. Attempting to get rid of copy.", index);
                    Crashes.TrackError(exc);
                    elements.RemoveAt(index + 1);
                    try { await file.DeleteAsync(); } catch (Exception e) { log.Error(e, "Undo failed as well."); }
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
            log.Information("Requested Deletion of indices {@Indices} with option {Option}.", indices, deleteOption);

            List<int> sortedIndices = new List<int>(indices);
            sortedIndices.Sort();
            sortedIndices.Reverse();

            foreach (int index in sortedIndices)
            {
                // check index
                if (!IsValidIndex(index))
                {
                    log.Error("Deletion of index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                    throw new ArgumentOutOfRangeException("Invalid index for mass deletion.");
                }

                await elements[index].ScanFile.DeleteAsync(deleteOption);
                elements.RemoveAt(index);
            }

            RefreshItemDescriptors();

            // if necessary, update or delete PDF
            if (scanResultFormat == SupportedFormat.PDF)
            {
                if (GetTotalNumberOfPages() > 0)
                {
                    // assign temporary names and then reinstate the file order prior to generation
                    for (int i = 0; i < elements.Count; i++)
                    {
                        await elements[i].ScanFile.RenameAsync("_" + i + elements[i].ScanFile.FileType);
                    }
                    await PrepareNewConversionFiles(elements.Select(e => e.ScanFile).ToList(), 0);

                    await GeneratePDF();
                }
                else await pdf.DeleteAsync();
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
            log.Information("Requested Deletion of index {Index} with option {Option}.", index, deleteOption);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Deletion of index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for deletion.");
            }

            await elements[index].ScanFile.DeleteAsync(deleteOption);

            elements.RemoveAt(index);
            RefreshItemDescriptors();

            // if necessary, update or delete PDF
            if (scanResultFormat == SupportedFormat.PDF)
            {
                if (GetTotalNumberOfPages() > 0)
                {
                    // assign temporary names and then reinstate the file order prior to generation
                    for (int i = 0; i < elements.Count; i++)
                    {
                        await elements[i].ScanFile.RenameAsync("_" + i + elements[i].ScanFile.FileType);
                    }
                    await PrepareNewConversionFiles(elements.Select(e => e.ScanFile).ToList(), 0);

                    await GeneratePDF();
                }
                else await pdf.DeleteAsync();
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
            log.Information("Drawing on index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Drawing on index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for drawing.");
            }

            // save changes to original file
            IRandomAccessStream fileStream;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96);
                using (fileStream = await elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, fileStream);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.White);

                        ds.DrawImage(canvasBitmap);
                        ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                    }
                    await renderTarget.SaveAsync(fileStream, GetCanvasBitmapFileFormat(elements[index].ScanFile), 1f);
                }
            }
            catch (Exception exc)
            {
                log.Error(exc, "Saving draw changes failed.");
                throw;
            }

            // delete cached image, delete image without rotation and reset rotation
            elements[index].CachedImage = null;
            if (elements[index].ImageWithoutRotation != null)
            {
                await elements[index].ImageWithoutRotation.DeleteAsync();
            }
            elements[index].ImageWithoutRotation = null;
            elements[index].CurrentRotation = BitmapRotation.None;

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
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
            log.Information("Drawing as copy on index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Drawing as copy on index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for drawing.");
            }

            // save changes to original file
            IRandomAccessStream sourceStream;
            StorageFile file;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi);
                using (sourceStream = await elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
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
                if (scanResultFormat == SupportedFormat.PDF) folder = folderConversion;
                else folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Scan_" + elements[index].FutureAccessListIndex.ToString());

                file = await folder.CreateFileAsync(elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                using (IRandomAccessStream targetStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderTarget.SaveAsync(targetStream, GetCanvasBitmapFileFormat(elements[index].ScanFile), 1f);
                }
            }
            catch (Exception exc)
            {
                log.Error(exc, "Saving draw as copy changes failed.");
                throw;
            }

            elements.Insert(index + 1, new ScanResultElement(file, elements[index].FutureAccessListIndex, elements[index].DisplayedFolder));
            RefreshItemDescriptors();

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF)
            {
                try
                {
                    List<StorageFile> filesNumbering = new List<StorageFile>();
                    for (int i = index + 1; i < elements.Count; i++)
                    {
                        await elements[i].ScanFile.RenameAsync("_" + elements[i].ScanFile.Name, NameCollisionOption.ReplaceExisting);
                        filesNumbering.Add(elements[i].ScanFile);
                    }
                    await PrepareNewConversionFiles(filesNumbering, index + 1);
                }
                catch (Exception exc)
                {
                    log.Error(exc, "Failed to generate PDF after drawing on index {Index} as copy. Attempting to get rid of copy.", index);
                    Crashes.TrackError(exc);
                    elements.RemoveAt(index + 1);
                    try { await file.DeleteAsync(); } catch (Exception e) { log.Error(e, "Undo failed as well."); }
                    RefreshItemDescriptors();
                    throw;
                }
                await GeneratePDF();
            }
        }


        private bool IsValidIndex(int index)
        {
            if (0 <= index && index < elements.Count)
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
                log.Error("ImageProperties for index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for getting properties.");
            }

            SupportedFormat? format = ConvertFormatStringToSupportedFormat(elements[index].ScanFile.FileType);
            switch (format)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                case SupportedFormat.PDF:
                    return (await elements[index].ScanFile.Properties.GetImagePropertiesAsync());
                default:
                    throw new ArgumentException("Invalid file format for getting image properties.");
            }
        }


        /// <summary>
        ///     Copies all image files in this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public Task CopyImagesAsync()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < GetTotalNumberOfPages(); i++)
            {
                indices.Add(i);
            }

            return CopyImagesAsync(indices);
        }


        // <summary>
        ///     Copies some image files in this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyImagesAsync(IList<int> indices)
        {
            Analytics.TrackEvent("Copy pages");
            log.Information("Copying indices {@Indices} requested.", indices);

            if (GetTotalNumberOfPages() == 0)
            {
                log.Error("Copying requested, but there are no pages.");
                throw new ApplicationException("No scans left to copy.");
            }

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check indices and whether the corresponding files are still available
            foreach (int index in indices)
            {
                if (index < 0 || index > GetTotalNumberOfPages() - 1)
                {
                    log.Error("Copying index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                    throw new ApplicationException("Invalid index for copying scan file.");
                }

                try { await elements[index].ScanFile.OpenAsync(FileAccessMode.Read); }
                catch (Exception e)
                {
                    log.Error(e, "Copying failed because the element at index {Index} is not available anymore.", index);
                    throw new ApplicationException("At least one scan file is not available anymore.", e);
                }
            }

            // copy desired scans to clipboard
            try
            {
                List<StorageFile> list = new List<StorageFile>();
                foreach (int index in indices)
                {
                    list.Add(elements[index].ScanFile);
                }
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                log.Error(e, "Copying pages failed.");
                throw new ApplicationException("Something went wrong while copying the scans.", e);
            }
        }



        /// <summary>
        ///     Copies the selected image file to the clipboard.
        /// </summary>
        /// <param name="index">The index of the scan that shall be copied.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyImageAsync(int index)
        {
            Analytics.TrackEvent("Copy page");
            log.Information("Copying index {Index} requested.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Copying index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for copying file.");
            }

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await elements[index].ScanFile.OpenAsync(FileAccessMode.Read); }
            catch (Exception e)
            {
                log.Error("Copying index {Index} requested, but the file is not available anymore.", index);
                throw new ApplicationException("Selected scan file is not available anymore.", e);
            }

            // set contents according to file type and copy to clipboard
            SupportedFormat format = scanResultFormat;

            try
            {
                switch (format)
                {
                    case SupportedFormat.JPG:
                    case SupportedFormat.PNG:
                    case SupportedFormat.TIF:
                    case SupportedFormat.BMP:
                        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(elements[index].ScanFile));
                        Clipboard.SetContent(dataPackage);
                        break;
                    default:
                        List<StorageFile> list = new List<StorageFile>();
                        list.Add(elements[index].ScanFile);
                        dataPackage.SetStorageItems(list);
                        Clipboard.SetContent(dataPackage);
                        break;
                }
            }
            catch (Exception e)
            {
                log.Error(e, "Copying the page failed.");
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Copies the file represented by this scanResult to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyAsync()
        {
            Analytics.TrackEvent("Copy document");
            log.Information("Copying document requested.");

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await pdf.OpenAsync(FileAccessMode.Read); }
            catch (Exception e)
            {
                log.Error(e, "Copying document failed.");
                throw new ApplicationException("File is not available anymore.", e);
            }

            try
            {
                List<StorageFile> list = new List<StorageFile>();
                list.Add(pdf);
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                log.Error(e, "Copying document failed.");
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Adds the specified files to this instance and converts them to the targetFormat, if necessary. The final files are
        ///     saved to the targetFolder, unless the result is a PDF document.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public async Task AddFiles(IEnumerable<StorageFile> files, SupportedFormat? targetFormat, StorageFolder targetFolder, int futureAccessListIndexStart, bool displayFolder)
        {
            log.Information("Adding {Num} files, the target format is {Format}.", files.Count(), targetFormat);
            int futureAccessListIndex = futureAccessListIndexStart;

            string append = DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00");
            if (targetFormat == null || targetFormat == SupportedFormat.PDF)
            {
                // no conversion (but perhaps generation later on), just add files for now
                if (targetFormat == SupportedFormat.PDF) await PrepareNewConversionFiles(files, GetTotalNumberOfPages());

                foreach (StorageFile file in files)
                {
                    if (displayFolder && targetFolder != null) elements.Add(new ScanResultElement(file, futureAccessListIndex, targetFolder.DisplayName));
                    else elements.Add(new ScanResultElement(file, futureAccessListIndex, null));

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                    }

                    if (settingAppendTime && targetFormat != SupportedFormat.PDF) await SetInitialNameAsync(elements[elements.Count - 1], append);
                }
            }
            else
            {
                int numberOfPagesOld = GetTotalNumberOfPages();

                // immediate conversion necessary
                foreach (StorageFile file in files)
                {
                    if (file == null) continue;

                    if (displayFolder && targetFolder != null) elements.Add(new ScanResultElement(file, futureAccessListIndex, targetFolder.DisplayName));
                    else elements.Add(new ScanResultElement(file, futureAccessListIndex, null));

                    if (targetFolder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("Scan_" + futureAccessListIndex.ToString(), targetFolder);
                        futureAccessListIndex += 1;
                    }

                    if (settingAppendTime) await SetInitialNameAsync(elements[elements.Count - 1], append);
                }

                Task[] conversionTasks = new Task[files.Count()];
                try
                {
                    for (int i = numberOfPagesOld; i < GetTotalNumberOfPages(); i++)
                    {
                        conversionTasks[i - numberOfPagesOld] = ConvertScanAsync(i, (SupportedFormat)targetFormat, targetFolder);
                    }
                    await Task.WhenAll(conversionTasks);
                }
                catch (Exception exc)
                {
                    log.Error(exc, "Failed to convert at least one new page. All new pages will be discarded.");

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
                    for (int i = numberOfPagesOld; i < GetTotalNumberOfPages(); i++)
                    {
                        indices.Add(i);
                    }
                    await DeleteScansAsync(indices);
                    throw;
                }
            }

            // if necessary, generate PDF now
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();

            // generate new previews and descriptors
            for (int i = GetTotalNumberOfPages() - files.Count(); i < GetTotalNumberOfPages(); i++)
            {
                await GetImageAsync(i);
                elements[i].ItemDescriptor = GetDescriptorForIndex(i);
            }
        }


        /// <summary>
        ///     Adds the specified files to this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public Task AddFiles(IEnumerable<StorageFile> files, SupportedFormat? targetFormat, int futureAccessListIndexStart)
        {
            return AddFiles(files, targetFormat, originalTargetFolder, futureAccessListIndexStart, false);
        }


        /// <summary>
        ///     Checks whether this instance represents an image file (JPG/PNG/TIF/BMP).
        /// </summary>
        public bool IsImage()
        {
            return IsImageFormat(scanResultFormat);
        }


        /// <summary>
        ///     Launches the "Open with" dialog for the selected scan.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task OpenImageWithAsync(int index)
        {
            log.Information("Requested opening with of index {Index}.", index);

            // check index
            if (!IsValidIndex(index))
            {
                log.Error("Opening with of index {Index} requested, but there are only {Num} pages.", index, elements.Count);
                throw new ArgumentOutOfRangeException("Invalid index for copying file.");
            }

            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            await Launcher.LaunchFileAsync(elements[index].ScanFile, options);
        }


        /// <summary>
        ///     Launches the "Open with" dialog for the represented file.
        /// </summary>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task OpenWithAsync()
        {
            Analytics.TrackEvent("Open with");
            log.Information("Requested opening with of document.");

            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            await Launcher.LaunchFileAsync(pdf, options);
        }


        /// <summary>
        ///     Returns the file format of the scan result (assumes that there can't be a mix).
        /// </summary>
        public SupportedFormat? GetFileFormat()
        {
            return scanResultFormat;
        }


        /// <summary>
        ///     Generates the PDF file in the temp folder and then moves it to the <see cref="originalTargetFolder"/> of
        ///     this scanResult. The pages are constructed using all images found in the temp folder's subfolder "conversion".
        /// </summary>
        /// <param name="fileName"></param>
        private async Task GeneratePDFAsync(string fileName)
        {
            log.Information("Requested PDF generation.");

            string newName;
            StorageFile newPdf;

            if (pdf == null)
            {
                log.Information("PDF doesn't exist yet.");
                newPdf = await folderTemp.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                log.Information("PDF already exists.");
                newPdf = pdf;
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
                    log.Information("Launching full trust process.");
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                    await win32ResultAsync.ConfigureAwait(false);
                    log.Information("Full trust process is done.");

                    // get result file and move it to its correct folder
                    try
                    {
                        newPdf = null;
                        newPdf = await folderTemp.GetFileAsync(newName);
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
                if (pdf == null)
                {
                    // PDF generated in target folder for the first time
                    await MoveFileToFolderAsync(newPdf, originalTargetFolder, newName, false);
                }
                else
                {
                    // PDF updated ~> replace old file
                    await MoveFileToFolderAsync(newPdf, originalTargetFolder, newName, true);
                }
                pdf = newPdf;
                return;
            }
            catch (Exception exc)
            {
                log.Error(exc, "Generating the PDF failed. Attempted to generate " + newName);
                var files = await folderTemp.GetFilesAsync();
                log.Information("State of temp folder: {@Folder}", files.Select(f => f.Name).ToList());
                Crashes.TrackError(exc);
                throw;
            }
        }

        public Task GeneratePDF()
        {
            return GeneratePDFAsync(pdf.Name);
        }


        /// <summary>
        ///     Numbers new files.
        /// </summary>
        /// <param name="files">The files to be numbered.</param>
        /// <param name="startIndex">The first number.</param>
        private static async Task PrepareNewConversionFiles(IEnumerable<StorageFile> files, int startIndex)
        {
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
                log.Error(exc, "Preparing conversion files with startIndex {Index} failed.", startIndex);
                throw;
            }
        }


        /// <summary>
        ///     Applies the order of <see cref="elements"/> to the PDF file.
        /// </summary>
        public async Task ApplyElementOrderToFilesAsync()
        {
            if (GetFileFormat() != SupportedFormat.PDF)
            {
                log.Error("Attempted to apply element order to non-PDF file.");
                throw new ApplicationException("Can only reorder source files for PDF.");
            }

            RefreshItemDescriptors();

            int nextNumber = 0;
            List<ScanResultElement> changedElements = new List<ScanResultElement>();

            // first rename all affected files to a temporary name to free up the file names
            foreach (ScanResultElement element in elements)
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
        ///     Refreshes all item descriptors of <see cref="elements"/>.
        /// </summary>
        private void RefreshItemDescriptors()
        {
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].ItemDescriptor = GetDescriptorForIndex(i);
            }
        }


        /// <summary>
        ///     Gets a specific item descriptor.
        /// </summary>
        /// <param name="index"></param>
        public string GetDescriptorForIndex(int index)
        {
            return String.Format(LocalizedString("TextPageListDescriptor"), (index + 1).ToString());
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
        }

        protected async Task SetInitialNamesAsync()
        {
            string append = DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00");

            if (scanResultFormat == SupportedFormat.PDF)
            {
                await SetInitialNameAsync(pdf, append);
            }
            else
            {
                foreach (ScanResultElement element in elements)
                {
                    await SetInitialNameAsync(element, append);
                }
            }
        }


        /// <summary>
        ///     Connects an <see cref="ItemsControl"/>'s source to the ScanResult.
        /// </summary>
        public void SetItemsSourceForControl(ItemsControl itemsControl)
        {
            itemsControl.ItemsSource = elements;
        }


        /// <summary>
        ///     Returns whether a page has a folder that shall be displayed.
        /// </summary>
        public bool HasDisplayedFolder(int index)
        {
            if (!IsValidIndex(index)) throw new ApplicationException("Invalid index " + index + " for HasDisplayedFolder().");

            return !String.IsNullOrEmpty(elements[index].DisplayedFolder);
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
    }
}
