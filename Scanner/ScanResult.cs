using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
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
        private List<StorageFile> scanFiles = new List<StorageFile>();              // all files generated with a single scan
        private List<BitmapImage> imageCache = new List<BitmapImage>();             // images of files
        private List<StorageFile> imagesWithoutRotation = new List<StorageFile>();  // images of files before they were rotated
        private List<BitmapRotation> currentRotations = new List<BitmapRotation>(); // current rotation per file


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ScanResult(IReadOnlyList<StorageFile> fileList)
        {
            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;
                
                scanFiles.Add(file);
                imageCache.Add(null);
                imagesWithoutRotation.Add(null);
                currentRotations.Add(BitmapRotation.None);
            }
        }


        private ScanResult(IReadOnlyList<StorageFile> fileList, SupportedFormat targetFormat, StorageFolder targetFolder)
        {
            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;

                scanFiles.Add(file);
                imageCache.Add(null);
                imagesWithoutRotation.Add(null);
                currentRotations.Add(BitmapRotation.None);
            }

            for (int i = 0; i < scanFiles.Count; i++)
            {
                if (scanFiles[i] == null) continue;

                SupportedFormat? sourceFormat = ConvertFormatStringToSupportedFormat(scanFiles[i].FileType);
                if (sourceFormat == null) throw new ApplicationException("Could not determine source format of file for intial conversion.");
                if (sourceFormat == targetFormat) continue;

                
                ConvertScanAsync(i, targetFormat, targetFolder).Wait();
            }
        }


        public async static Task<ScanResult> Create(IReadOnlyList<StorageFile> fileList)
        {
            await ClearTempFolder();
            return new ScanResult(fileList);
        }

        public async static Task<ScanResult> Create(IReadOnlyList<StorageFile> fileList, SupportedFormat targetFormat, StorageFolder targetFolder)
        {
            await ClearTempFolder();
            return new ScanResult(fileList, targetFormat, targetFolder);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the number of individual scans in this instance.
        /// </summary>
        public int GetTotalNumberOfScans()
        {
            return scanFiles.Count;
        }


        /// <summary>
        ///     Gets all files of the individual scans in this instance.
        /// </summary>
        public List<StorageFile> GetFiles()
        {
            List<StorageFile> files = new List<StorageFile>(scanFiles);
            return files;
        }


        /// <summary>
        ///     Gets the file of a single scan in this instance.
        /// </summary>
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public StorageFile GetFile(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            else return scanFiles.ElementAt(index);
        }


        /// <summary>
        ///     Gets an image preview of every individual scan in this instance.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<List<BitmapImage>> GetImagesAsync()
        {
            List<BitmapImage> previews = new List<BitmapImage>();
            List<Task<BitmapImage>> tasks = new List<Task<BitmapImage>>();

            // kick off conversion for all files
            for (int i = 0; i < scanFiles.Count; i++)
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
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<BitmapImage> GetImageAsync(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for preview.");

            // use cached image if possible
            if (imageCache[index] != null)
            {
                return imageCache[index];
            }

            // create new preview
            StorageFile sourceFile = scanFiles[index];
            BitmapImage bmp = null;
            int attempt = 0;
            IRandomAccessStream sourceStream;
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
                            using (sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                            {
                                bmp = new BitmapImage();
                                await bmp.SetSourceAsync(sourceStream);
                                attempt = -1;
                            }
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating preview of image.", e);

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
                            using (sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                            {
                                PdfDocument doc = await PdfDocument.LoadFromStreamAsync(sourceStream);
                                PdfPage page = doc.GetPage(0);
                                BitmapImage imageOfPdf = new BitmapImage();

                                using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                                {
                                    await page.RenderToStreamAsync(stream);
                                    await imageOfPdf.SetSourceAsync(stream);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating preview of PDF.", e);

                            await Task.Delay(500);
                            attempt++;
                        }
                    }
                    break;

                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    throw new NotImplementedException("Can not generate preview from (O)XPS.");

                default:
                    throw new ApplicationException("Could not determine file type for generating a preview.");
            }

            // save image to cache
            imageCache[index] = bmp;

            return bmp;
        }


        /// <summary>
        ///     Converts a scan to the <paramref name="targetFormat"/>. Also moves it to the <paramref name="targetFolder"/>
        ///     if converted to PDF.
        /// </summary>
        /// <param name="index">The index of the scan that's to be converted.</param>
        /// <param name="targetFormat">The format that the scan shall be converted to.</param>
        /// <param name="targetFolder">The folder that the conversion result shall be moved to. Only applied when converting to PDF.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="NotImplementedException">Attempted to convert from (O)XPS.</exception>  
        /// <exception cref="ApplicationException">Could not determine file type of scan.</exception>
        public async Task ConvertScanAsync(int index, SupportedFormat targetFormat, StorageFolder targetFolder)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for conversion.");

            // convert
            StorageFile sourceFile = scanFiles[index];
            string newName, newNameWithoutNumbering;
            switch (targetFormat)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                    // open image file, decode it and prepare an encoder with the target image format
                    IRandomAccessStream stream = await sourceFile.OpenAsync(FileAccessMode.ReadWrite);
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    Guid encoderId = GetBitmapEncoderId(targetFormat);
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                    encoder.SetSoftwareBitmap(softwareBitmap);

                    // save/encode the file in the target format
                    try { await encoder.FlushAsync(); }
                    catch (Exception)
                    {
                        throw;
                    }
                    stream.Dispose();

                    // rename file to make the extension match the new format and watch out for name collisions
                    newNameWithoutNumbering = RemoveNumbering(sourceFile.Name
                        .Replace("." + sourceFile.Name.Split(".")[1], "." + targetFormat));
                    newName = newNameWithoutNumbering;

                    try { await sourceFile.RenameAsync(newName, NameCollisionOption.FailIfExists); }
                    catch (Exception)
                    {
                        // cycle through file numberings until one is not occupied
                        for (int i = 1; true; i++)
                        {
                            try
                            {
                                await sourceFile.RenameAsync(newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString()
                                    + ")." + newNameWithoutNumbering.Split(".")[1], NameCollisionOption.FailIfExists);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            newName = newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString() + ")." + newNameWithoutNumbering.Split(".")[1];
                        }
                    }
                    break;

                case SupportedFormat.PDF:
                    // convert to PDF
                    try
                    {
                        taskCompletionSource = new TaskCompletionSource<bool>();
                        var win32ResultAsync = taskCompletionSource.Task;

                        // save the source and target name
                        ApplicationData.Current.LocalSettings.Values["pdfSourceFileName"] = sourceFile.Name;
                        ApplicationData.Current.LocalSettings.Values["targetFileName"] = sourceFile.DisplayName + ".pdf";

                        // save measurements, which determine PDF file size
                        var imageProperties = await GetImagePropertiesAsync(index);
                        ApplicationData.Current.LocalSettings.Values["sourceFileWidth"] = imageProperties.Width;
                        ApplicationData.Current.LocalSettings.Values["sourceFileHeight"] = imageProperties.Height;

                        // call win32 app and wait for result
                        if (appServiceConnection == null)
                        {
                            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                        }
                        else
                        {
                            ValueSet message = new ValueSet();
                            message.Add("REQUEST", "CONVERT");
                            var sendMessageAsync = appServiceConnection.SendMessageAsync(message);
                        }
                        await Task.WhenAny(win32ResultAsync, Task.Delay(15000));

                        // get result file and move it to its correct folder
                        StorageFile convertedFile = null;
                        convertedFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(sourceFile.DisplayName + ".pdf");

                        // move PDF file to target folder
                        try
                        {
                            await convertedFile.MoveAsync(targetFolder, convertedFile.Name, NameCollisionOption.FailIfExists);
                            newName = convertedFile.Name;
                        }
                        catch (Exception)
                        {
                            if (convertedFile.DisplayName[convertedFile.DisplayName.Length - 1] == ')')
                            {
                                newNameWithoutNumbering = RemoveNumbering(convertedFile.Name);
                            }
                            else
                            {
                                newNameWithoutNumbering = convertedFile.Name;
                            }

                            // cycle through file numberings until one is not occupied
                            for (int i = 1; true; i++)
                            {
                                try
                                {
                                    await convertedFile.MoveAsync(targetFolder, newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString()
                                        + ")." + newNameWithoutNumbering.Split(".")[1], NameCollisionOption.FailIfExists);
                                }
                                catch (Exception)
                                {
                                    continue;
                                }
                                newName = newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString() + ")." + newNameWithoutNumbering.Split(".")[1];
                                break;
                            }
                        }

                        // delete the source image
                        await sourceFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch (Exception) { throw; }
                    break;

                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    throw new NotImplementedException("Can not convert from (O)XPS.");

                default:
                    throw new ApplicationException("Could not determine source file type for conversion.");
            }

            // refresh file
            scanFiles[index] = await targetFolder.GetFileAsync(newName);
        }


        /// <summary>
        ///     Rotates a scan. Consecutive calls are lossless if the scan isn't edited in-between. Only supports JPG, PNG, TIF and BMP.
        /// </summary>
        /// <param name="index">The index of the scan that's to be rotated.</param>
        /// <param name="rotation">The rotation that shall be performed.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ArgumentException">The selected scan's file type is not supported for rotation.</exception>
        /// <exception cref="ApplicationException">Something went wrong while rotating. Perhaps the scan format isn't supported.</exception>
        public async Task RotateScanAsync(int index, BitmapRotation rotation)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for rotation.");

            // check rotation
            if (rotation == BitmapRotation.None) return;

            // rotate and make sure that consecutive rotations are lossless
            switch (ConvertFormatStringToSupportedFormat(scanFiles[index].FileType))
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                    try
                    {
                        using (IRandomAccessStream fileStream = await scanFiles[index].OpenAsync(FileAccessMode.ReadWrite))
                        {
                            BitmapDecoder decoder;
                            if (imagesWithoutRotation[index] == null)
                            {
                                imagesWithoutRotation[index] = await GetFile(index)
                                    .CopyAsync(ApplicationData.Current.TemporaryFolder, GetFile(index).Name, NameCollisionOption.ReplaceExisting);
                                decoder = await BitmapDecoder.CreateAsync(fileStream);
                            }
                            else
                            {
                                using (IRandomAccessStream bitmapStream = await imagesWithoutRotation[index].OpenReadAsync())
                                {
                                    decoder = await BitmapDecoder.CreateAsync(bitmapStream);
                                }
                            }

                            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                            Guid encoderId = GetBitmapEncoderId(scanFiles[index].Name.Split(".")[1]);

                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, fileStream);
                            encoder.SetSoftwareBitmap(softwareBitmap);

                            encoder.BitmapTransform.Rotation = CombineRotations(rotation, currentRotations[index]);

                            await encoder.FlushAsync();
                            currentRotations[index] = encoder.BitmapTransform.Rotation;
     
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException("Rotation failed.", e);
                    }
                    break;
                case SupportedFormat.PDF:
                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    throw new ArgumentException("Rotation not supported for PDF, XPS or OXPS.");
                default:
                    throw new ApplicationException("Could not determine source file type for rotation.");
            }

            // delete image from cache
            imageCache[index] = null;
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for rename.");

            // check name is different
            if (scanFiles[index].Name == newName) return;

            // rename
            await scanFiles[index].RenameAsync(newName, NameCollisionOption.FailIfExists);
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for crop.");

            // save changes to original file
            IRandomAccessStream stream = null;
            try
            {
                stream = await scanFiles[index].OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(scanFiles[index]), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            stream.Dispose();

            // delete cached image, delete image without rotation and reset rotation
            imageCache[index] = null;
            if (imagesWithoutRotation[index] != null)
            {
                await imagesWithoutRotation[index].DeleteAsync();
                imagesWithoutRotation[index] = null;
            }
            currentRotations[index] = BitmapRotation.None;
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for crop.");

            // save crop as new file
            StorageFile file;
            IRandomAccessStream stream = null;
            try
            {
                StorageFolder folder = await scanFiles[index].GetParentAsync();
                file = await folder.CreateFileAsync(scanFiles[index].Name, CreationCollisionOption.GenerateUniqueName);
                stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                await imageCropper.SaveAsync(stream, GetBitmapFileFormat(scanFiles[index]), true);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
            stream.Dispose();

            scanFiles.Insert(index, file);
            imageCache.Insert(index, null);
            imagesWithoutRotation.Insert(index, null);
            currentRotations.Insert(index, BitmapRotation.None);
        }


        /// <summary>
        ///     Deletes all scans in this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while deleting the scans.</exception>
        public async Task DeleteScansAsync(StorageDeleteOption deleteOption)
        {
            for (int i = 0; i < scanFiles.Count; i++)
            {
                await DeleteScanAsync(i, deleteOption);
            }
        }


        /// <summary>
        ///     Deletes a single scan in this instance.
        /// </summary>
        /// <param name="index">The index of the scan that shall be deleted.</param>
        /// <exception cref="Exception">Something went wrong while delting the scan.</exception>
        public async Task DeleteScanAsync(int index, StorageDeleteOption deleteOption)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for deletion.");

            await scanFiles[index].DeleteAsync(StorageDeleteOption.Default);

            scanFiles.RemoveAt(index);
            imageCache.RemoveAt(index);
            if (imagesWithoutRotation[index] != null)
            {
                await imagesWithoutRotation[index].DeleteAsync();
            }
            imagesWithoutRotation.RemoveAt(index);
            currentRotations.RemoveAt(index);
        }


        /// <summary>
        ///     Deletes a single scan in this instance.
        /// </summary>
        /// <param name="index">The index of the scan that shall be deleted.</param>
        /// <exception cref="Exception">Something went wrong while delting the scan.</exception>
        public async Task DeleteScan(int index)
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for drawing.");

            // save changes to original file
            IRandomAccessStream fileStream;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96);
                using (fileStream = await scanFiles[index].OpenAsync(FileAccessMode.ReadWrite))
                {
                    CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, fileStream);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.White);

                        ds.DrawImage(canvasBitmap);
                        ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                    }
                    await renderTarget.SaveAsync(fileStream, GetCanvasBitmapFileFormat(scanFiles[index]), 1f);
                }
            }
            catch (Exception)
            {
                throw;
            }

            // delete cached image, delete image without rotation and reset rotation
            imageCache[index] = null;
            if (imagesWithoutRotation[index] != null)
            {
                await imagesWithoutRotation[index].DeleteAsync();
                imagesWithoutRotation[index] = null;
            }
            imagesWithoutRotation[index] = null;
            currentRotations[index] = BitmapRotation.None;
        }


        /// <summary>
        ///     Add ink strokes from a canvas to a copy of the selected scan. The copy is then added to this instance.
        /// </summary>
        /// <param name="index">The index of the scan whose copy the strokes shall be added to.</param>
        /// <param name="inkCanvas">The canvas that holds the strokes.</param>
        /// <exception cref="Exception">Something went wrong while applying the strokes.</exception>
        public async Task DrawOnScanAsCopyAsync(int index, InkCanvas inkCanvas)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for drawing.");

            // save changes to original file
            IRandomAccessStream sourceStream;
            StorageFile file;
            try
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi);
                using (sourceStream = await scanFiles[index].OpenAsync(FileAccessMode.ReadWrite))
                {
                    CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, sourceStream);
                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.White);

                        ds.DrawImage(canvasBitmap);
                        ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                    }
                }

                StorageFolder folder = await scanFiles[index].GetParentAsync();
                file = await folder.CreateFileAsync(scanFiles[index].Name, CreationCollisionOption.GenerateUniqueName);
                using (IRandomAccessStream targetStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderTarget.SaveAsync(targetStream, GetCanvasBitmapFileFormat(scanFiles[index]), 1f);
                }
            }
            catch (Exception)
            {
                throw;
            }

            scanFiles.Insert(index, file);
            imageCache.Insert(index, null);
            imagesWithoutRotation.Insert(index, null);
            currentRotations.Insert(index, BitmapRotation.None);
        }


        private bool IsValidIndex(int index)
        {
            if (0 <= index && index < scanFiles.Count)
            {
                return true;
            } else
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
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for getting properties.");

            SupportedFormat? format = ConvertFormatStringToSupportedFormat(scanFiles[index].FileType);
            switch (format)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                    return (await scanFiles[index].Properties.GetImagePropertiesAsync());
                default:
                    throw new ArgumentException("Invalid file format for getting image properties.");
            }
        }


        /// <summary>
        ///     Copies all scan files in this instance to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyScansAsync()
        {
            if (GetTotalNumberOfScans() == 0) throw new ApplicationException("No scans left to copy."); 
            if (GetTotalNumberOfScans() == 1)
            {
                await CopyScanAsync(0);
                return;
            }
            
            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the files are still available
            foreach (StorageFile file in scanFiles)
            {
                try { await file.OpenAsync(FileAccessMode.Read); }
                catch (Exception e) { throw new ApplicationException("At least one scan file is not available anymore.", e); }
            }

            // copy all to clipboard
            try
            {
                List<StorageFile> list = new List<StorageFile>(scanFiles);
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Something went wrong while copying the scans.", e);
            }
        }


        /// <summary>
        ///     Copies the selected scan file to the clipboard.
        /// </summary>
        /// <param name="index">The index of the scan that shall be copied.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyScanAsync(int index)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for copying file.");

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await scanFiles[index].OpenAsync(FileAccessMode.Read); }
            catch (Exception e) { throw new ApplicationException("Selected scan file is not available anymore.", e); }

            // set contents according to file type and copy to clipboard
            SupportedFormat? format = ConvertFormatStringToSupportedFormat(scanFiles[index].FileType);
            
            try
            {
                switch (format)
                {
                    case SupportedFormat.JPG:
                    case SupportedFormat.PNG:
                    case SupportedFormat.TIF:
                    case SupportedFormat.BMP:
                        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(scanFiles[index]));
                        Clipboard.SetContent(dataPackage);
                        break;
                    default:
                        List<StorageFile> list = new List<StorageFile>();
                        list.Add(scanFiles[index]);
                        dataPackage.SetStorageItems(list);
                        Clipboard.SetContent(dataPackage);
                        break;
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Checks whether the selected scan is an image file (JPG/PNG/TIF/BMP).
        /// </summary>
        public bool IsImage(int index)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index.");

            switch (ConvertFormatStringToSupportedFormat(scanFiles[index].FileType))
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                    return true;
                default:
                    return false;
            }
        }


        /// <summary>
        ///     Launches the "Open with" dialog for the selected scan.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="Exception">Something went wrong.</exception>
        public async Task OpenScanWithAsync(int index)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for copying file.");

            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            await Launcher.LaunchFileAsync(scanFiles[index], options);
        }
    }
}
