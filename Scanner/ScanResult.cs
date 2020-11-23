using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
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
        public ObservableCollection<ScanResultElement> elements = new ObservableCollection<ScanResultElement>();
        public SupportedFormat scanResultFormat;
        public StorageFile pdf = null;
        private StorageFolder originalTargetFolder;

        private static StorageFolder conversionFolder;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ScanResult(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder)
        {
            foreach (StorageFile file in fileList)
            {
                if (file == null) continue;

                elements.Add(new ScanResultElement(file));
            }
            scanResultFormat = (SupportedFormat) ConvertFormatStringToSupportedFormat(elements[0].ScanFile.FileType);
            originalTargetFolder = targetFolder;
        }


        //private ScanResult(IReadOnlyList<StorageFile> fileList, SupportedFormat targetFormat, StorageFolder targetFolder)
        //{
        //    foreach (StorageFile file in fileList)
        //    {
        //        if (file == null) continue;

        //        elements.Add(new ScanResultElement(file));
        //    }

        //    if (targetFormat == SupportedFormat.PDF)
        //    {
        //        string pdfName = fileList[0].DisplayName + ".pdf";
        //        //Task.WaitAll(GeneratePDF(pdfName));
        //        GeneratePDF(pdfName);
        //    } 
        //    else
        //    {
        //        Task[] convertTasks = new Task[elements.Count];

        //        for (int i = 0; i < elements.Count; i++)
        //        {
        //            if (elements[i] == null) continue;

        //            SupportedFormat? sourceFormat = ConvertFormatStringToSupportedFormat(elements[i].ScanFile.FileType);
        //            if (sourceFormat == null) throw new ApplicationException("Could not determine source format of file for intial conversion.");
        //            if (sourceFormat == targetFormat) continue;


        //            convertTasks[i] = ConvertScanAsync(i, targetFormat, targetFolder);
        //        }

        //        originalTargetFolder = targetFolder;

        //        Task.WaitAll(convertTasks);
        //    }
        //    scanResultFormat = targetFormat;
        //}


        public async static Task<ScanResult> Create(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder)
        {
            conversionFolder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync("conversion");
            Task[] moveTasks = new Task[fileList.Count];
            for (int i = 0; i < fileList.Count; i++)
            {
                moveTasks[i] = MoveFileToFolderAsync(fileList[i], targetFolder, RemoveNumbering(fileList[i].Name), false);
            }
            await Task.WhenAll(moveTasks);

            ScanResult result = new ScanResult(fileList, targetFolder);
            await result.GetImagesAsync();
            return result;
        }

        public async static Task<ScanResult> Create(IReadOnlyList<StorageFile> fileList, StorageFolder targetFolder, SupportedFormat targetFormat)
        {
            conversionFolder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync("conversion");
            ScanResult result = new ScanResult(fileList, targetFolder);

            if (targetFormat == SupportedFormat.PDF)
            {
                string pdfName = fileList[0].DisplayName + ".pdf";
                await result.NumberNewConversionFiles(fileList);
                await result.GeneratePDF(pdfName);
            }
            else
            {
                Task[] conversionTasks = new Task[result.GetTotalNumberOfScans()];
                for (int i = 0; i < result.GetTotalNumberOfScans(); i++)
                {
                    conversionTasks[i] = result.ConvertScanAsync(i, targetFormat, targetFolder);
                }
                await Task.WhenAll(conversionTasks);
            }

            result.scanResultFormat = targetFormat;
            await result.GetImagesAsync();
            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the number of individual scans in this instance.
        /// </summary>
        public int GetTotalNumberOfScans()
        {
            return elements.Count;
        }


        /// <summary>
        ///     Gets all files of the individual scans in this instance.
        /// </summary>
        public List<StorageFile> GetImageFiles()
        {
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
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for getting file.");
            else return elements.ElementAt(index).ScanFile;
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
        /// <param name="index">The desired scan's index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<BitmapImage> GetImageAsync(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for preview.");

            // use cached image if possible
            if (elements[index].CachedImage != null)
            {
                return elements[index].CachedImage;
            }

            // create new bitmap
            StorageFile sourceFile = elements[index].ScanFile;
            BitmapImage bmp = null;
            int attempt = 0;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, async () => {
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
                                    PdfPage page = doc.GetPage((uint) index);
                                    BitmapImage imageOfPdf = new BitmapImage();

                                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                                    {
                                        await page.RenderToStreamAsync(stream);
                                        await imageOfPdf.SetSourceAsync(stream);
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating bitmap of PDF.", e);

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
                    BitmapDecoder bitmapDecoder = await BitmapDecoder.CreateAsync(sourceStream);
                    SoftwareBitmap softwareBitmap = await bitmapDecoder.GetSoftwareBitmapAsync();
                    Guid encoderId = GetBitmapEncoderId(sourceFile.FileType);
                    var imageStream = new InMemoryRandomAccessStream();
                    BitmapEncoder bitmapEncoder = await BitmapEncoder.CreateAsync(encoderId, imageStream);
                    bitmapEncoder.SetSoftwareBitmap(softwareBitmap);
                    bitmapEncoder.BitmapTransform.ScaledWidth = bitmapDecoder.PixelWidth / 10;                   // reduce quality by 90%
                    bitmapEncoder.BitmapTransform.ScaledHeight = bitmapDecoder.PixelHeight / 10;                 //
                    await bitmapEncoder.FlushAsync();
                    await thumbnail.SetSourceAsync(imageStream);
                    elements[index].Thumbnail = thumbnail;
                }
            });
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for conversion.");

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
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                        encoder.SetSoftwareBitmap(softwareBitmap);

                        // save/encode the file in the target format
                        try { await encoder.FlushAsync(); }
                        catch (Exception)
                        {
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
                    throw new NotImplementedException("Can not convert to (O)XPS.");

                default:
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
            switch (scanResultFormat)
            {
                case SupportedFormat.JPG:
                case SupportedFormat.PNG:
                case SupportedFormat.TIF:
                case SupportedFormat.BMP:
                case SupportedFormat.PDF:
                    try
                    {
                        using (IRandomAccessStream fileStream = await elements[index].ScanFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            BitmapDecoder decoder;
                            if (elements[index].ImageWithoutRotation == null)
                            {
                                elements[index].ImageWithoutRotation = await GetImageFile(index)
                                    .CopyAsync(ApplicationData.Current.TemporaryFolder, GetImageFile(index).Name, NameCollisionOption.ReplaceExisting);
                                decoder = await BitmapDecoder.CreateAsync(fileStream);
                            }
                            else
                            {
                                using (IRandomAccessStream bitmapStream = await elements[index].ImageWithoutRotation.OpenReadAsync())
                                {
                                    decoder = await BitmapDecoder.CreateAsync(bitmapStream);
                                }
                            }

                            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                            Guid encoderId = GetBitmapEncoderId(elements[index].ScanFile.Name.Split(".")[1]);

                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, fileStream);
                            encoder.SetSoftwareBitmap(softwareBitmap);

                            encoder.BitmapTransform.Rotation = CombineRotations(rotation, elements[index].CurrentRotation);

                            await encoder.FlushAsync();
                            elements[index].CurrentRotation = encoder.BitmapTransform.Rotation;
     
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException("Rotation failed.", e);
                    }

                    if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
                    break;

                case SupportedFormat.XPS:
                case SupportedFormat.OpenXPS:
                    throw new ArgumentException("Rotation not supported for PDF, XPS or OXPS.");

                default:
                    throw new ApplicationException("Could not determine source file type for rotation.");
            }

            // delete image from cache
            elements[index].CachedImage = null;
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
            if (elements[index].ScanFile.Name == newName) return;

            // rename
            await elements[index].ScanFile.RenameAsync(newName, NameCollisionOption.FailIfExists);
        }


        /// <summary>
        ///     Renames the scan. Only for scans that are combined into a single document (e.g. PDF).
        /// </summary>
        /// <param name="index">The index of the scan that's to be renamed.</param>
        /// <param name="newName">The desired full file name for the scan.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid index.</exception>
        public async Task RenameScanAsync(string newName)
        {
            // check type
            if (scanResultFormat != SupportedFormat.PDF) throw new ApplicationException("ScanResult represents more than one file.");

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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for crop.");

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
                elements[index].ImageWithoutRotation = null;
            }
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for crop.");

            // save crop as new file
            StorageFile file;
            IRandomAccessStream stream = null;
            try
            {
                StorageFolder folder = await elements[index].ScanFile.GetParentAsync();
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

            elements.Insert(index, new ScanResultElement(file));

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
        }


        /// <summary>
        ///     Deletes all scans in this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while deleting the scans.</exception>
        public async Task DeleteScansAsync(StorageDeleteOption deleteOption)
        {
            if (scanResultFormat == SupportedFormat.PDF)
            {
                await pdf.DeleteAsync(deleteOption);
            }
            else
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    await DeleteScanAsync(i, deleteOption);
                }
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

            await elements[index].ScanFile.DeleteAsync(deleteOption);

            elements.RemoveAt(index);

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
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
            catch (Exception)
            {
                throw;
            }

            // delete cached image, delete image without rotation and reset rotation
            elements[index].CachedImage = null;
            if (elements[index].ImageWithoutRotation != null)
            {
                await elements[index].ImageWithoutRotation.DeleteAsync();
                elements[index].ImageWithoutRotation = null;
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for drawing.");

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

                StorageFolder folder = await elements[index].ScanFile.GetParentAsync();
                file = await folder.CreateFileAsync(elements[index].ScanFile.Name, CreationCollisionOption.GenerateUniqueName);
                using (IRandomAccessStream targetStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderTarget.SaveAsync(targetStream, GetCanvasBitmapFileFormat(elements[index].ScanFile), 1f);
                }
            }
            catch (Exception)
            {
                throw;
            }

            elements.Insert(index, new ScanResultElement(file));

            // if necessary, generate PDF
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();
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
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for getting properties.");

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
        public async Task CopyImagesAsync()
        {
            if (GetTotalNumberOfScans() == 0) throw new ApplicationException("No scans left to copy."); 
            if (GetTotalNumberOfScans() == 1)
            {
                await CopyImageAsync(0);
                return;
            }
            
            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the files are still available
            foreach (ScanResultElement element in elements)
            {
                try { await element.ScanFile.OpenAsync(FileAccessMode.Read); }
                catch (Exception e) { throw new ApplicationException("At least one scan file is not available anymore.", e); }
            }

            // copy all to clipboard
            try
            {
                List<StorageFile> list = new List<StorageFile>();
                foreach (ScanResultElement element in elements)
                {
                    list.Add(element.ScanFile);
                }
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
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
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for copying file.");

            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await elements[index].ScanFile.OpenAsync(FileAccessMode.Read); }
            catch (Exception e) { throw new ApplicationException("Selected scan file is not available anymore.", e); }

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
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Copies the file represented by this scanResult to the clipboard.
        /// </summary>
        /// <exception cref="ApplicationException">Something went wrong while copying.</exception>
        public async Task CopyAsync()
        {
            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // check whether the file is still available
            try { await pdf.OpenAsync(FileAccessMode.Read); }
            catch (Exception e) { throw new ApplicationException("File is not available anymore.", e); }

            try
            {
                List<StorageFile> list = new List<StorageFile>();
                list.Add(pdf);
                dataPackage.SetStorageItems(list);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Something went wrong while copying the scan.", e);
            }
        }


        /// <summary>
        ///     Adds the specified files to this instance.
        /// </summary>
        /// <exception cref="Exception">Something went wrong while adding the files.</exception>
        public async Task AddFiles(IReadOnlyList<StorageFile> files, SupportedFormat? targetFormat)
        {
            if (targetFormat == null || targetFormat == SupportedFormat.PDF)
            {
                // no conversion (but perhaps generation later on), just add files for now
                foreach (StorageFile file in files)
                {
                    if (file == null) continue;

                    if (targetFormat == SupportedFormat.PDF) await NumberNewConversionFiles(files);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        elements.Add(new ScanResultElement(file));
                    });
                }
            } 
            else
            {
                // immediate conversion necessary
                foreach (StorageFile file in files)
                {
                    if (file == null) continue;

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        elements.Add(new ScanResultElement(file));
                    });
                }

                Task[] conversionTasks = new Task[GetTotalNumberOfScans()];
                for (int i = 0; i < GetTotalNumberOfScans(); i++)
                {
                    conversionTasks[i] = ConvertScanAsync(i, (SupportedFormat) targetFormat, originalTargetFolder);
                }
                await Task.WhenAll(conversionTasks);
            }

            // if necessary, generate PDF now
            if (scanResultFormat == SupportedFormat.PDF) await GeneratePDF();

            // generate new previews
            for (int i = GetTotalNumberOfScans() - files.Count; i < GetTotalNumberOfScans(); i++)
            {
                await GetImageAsync(i);
            }
        }


        /// <summary>
        ///     Checks whether this instance represents an image file (JPG/PNG/TIF/BMP).
        /// </summary>
        public bool IsImage()
        {
            switch (scanResultFormat)
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
        public async Task OpenImageWithAsync(int index)
        {
            // check index
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException("Invalid index for copying file.");

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
        private async Task GeneratePDF(string fileName)
        {
            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            //{
                string newName;
                StorageFile newPdf;

                if (pdf == null)
                {
                    newPdf = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                } else
                {
                    newPdf = pdf;
                }

                try
                {
                    taskCompletionSource = new TaskCompletionSource<bool>();
                    var win32ResultAsync = taskCompletionSource.Task;

                    // save the source and target name
                    ApplicationData.Current.LocalSettings.Values["targetFileName"] = newPdf.Name;

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
                    await win32ResultAsync.ConfigureAwait(false);

                    // get result file and move it to its correct folder
                    newPdf = null;
                    newPdf = await ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);

                    // move PDF file to target folder
                    newName = newPdf.Name;
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
                catch (Exception) { throw; }
            //});
        }

        private Task GeneratePDF()
        {
            return GeneratePDF(pdf.Name);
        }

        private async Task NumberNewConversionFiles(IReadOnlyList<StorageFile> files)
        {
            int nextNumber = GetTotalNumberOfScans() - 1;
            foreach (StorageFile file in files)
            {
                await file.RenameAsync(nextNumber + file.FileType);
            }
        }
    }
}
