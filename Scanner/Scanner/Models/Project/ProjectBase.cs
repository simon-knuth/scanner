using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Models.ImagePage;

namespace Scanner.Models
{
    public abstract partial class ProjectBase : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        protected static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        protected static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        protected static readonly ITesseractService TesseractService = Ioc.Default.GetRequiredService<ITesseractService>();
        #endregion

        #region Events
        public event EventHandler PagesAdded;
        public event EventHandler PagesRemoved;
        #endregion

        [ObservableProperty]
        private bool isSaving;

        public bool IsSaved => areFilesSaved && hasFileNameBeenApplied;

        public TaskCompletionSource? LatestSaveProcess;

        public ObservableCollection<IProjectPage> Pages
        {
            get;
            private set;
        }

        public TargetFormat Format;

        public bool IsPdf => Format == TargetFormat.PDF;

        private bool _areFilesSaved;
        protected bool areFilesSaved
        {
            get => _areFilesSaved;
            set
            {
                SetProperty(ref _areFilesSaved, value);
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        private bool _hasFileNameBeenApplied = true;
        protected bool hasFileNameBeenApplied
        {
            get => _hasFileNameBeenApplied;
            set
            {
                SetProperty(ref _hasFileNameBeenApplied, value);
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        protected bool saveProcessWaitingToStart;

        protected SemaphoreSlim saveSemaphore = new(1, 1);                // needed to run a save process

        protected SemaphoreSlim projectObjectSemaphore = new(1, 1);       // needed to modify the Project object
        protected SemaphoreSlim projectFolderSemaphore = new(1, 1);       // needed to modify the Project folder
        protected SemaphoreSlim changesFolderSemaphore = new(1, 1);       // needed to modify the Changes folder


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected ProjectBase(IList<IProjectPage> pages, TargetFormat format)
        {
            Pages = new ObservableCollection<IProjectPage>(pages);
            Format = format;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public abstract Task DeleteAsync();
        public abstract Task SaveAsync(DispatcherQueue uiDispatcherQueue);


        public async Task<List<IProjectPage>> AddFilesAsync(List<ProjectFileInsertion> insertions, bool keepSourceFiles)
        {
            await StartEditingAsync();

            try
            {
                // keep track of changes in case of error
                List<StorageFile> copiedFiles = new();
                List<KeyValuePair<IProjectPage, int>> preparedInsertions = new();
                List<IProjectPage> insertedPages = new();

                // revertable section
                try
                {
                    // add files
                    foreach (ProjectFileInsertion insertion in insertions)
                    {
                        IProjectPage page = await CreatePageFromFileAsync(insertion.File, insertion.Index, insertion.FileName, insertion.TargetFolder, keepSourceFiles, AppDataService.ChangesFolder, insertion.BaseFilter, insertion.Filter);
                        copiedFiles.Add(page.SourceFile);

                        preparedInsertions.Add(new KeyValuePair<IProjectPage, int>(page, insertion.Index));
                    }

                    // add pages
                    foreach (KeyValuePair<IProjectPage, int> insertion in preparedInsertions)
                    {
                        Pages.Insert(insertion.Value, insertion.Key);
                        insertedPages.Add(insertion.Key);
                    }

                    // update previews
                    List<ImagePage> imagePages = insertedPages.OfType<ImagePage>().ToList();
                    if (imagePages.Any())
                    {
                        await UpdatePagePreviewsAsync(imagePages);
                    }
                }
                catch (Exception exc)
                {
                    // roll back changes
                    foreach (StorageFile file in copiedFiles)
                    {
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }

                    foreach (IProjectPage page in insertedPages)
                    {
                        Pages.Remove(page);
                    }

                    throw new ProjectException(exc);
                }

                // update indices
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].Index = i;
                }

                PagesAdded?.Invoke(this, EventArgs.Empty);

                areFilesSaved = false;
                return insertedPages;
            }
            finally
            {
                FinishEditing();
            }
        }

        public async Task AddPagesAsync(List<IProjectPage> insertions)
        {
            await StartEditingAsync();

            try
            {
                // keep track of changes in case of error
                List<KeyValuePair<StorageFile, StorageFolder>> moves = new();
                List<IProjectPage> insertedPages = new();

                // revertable section
                try
                {
                    // move files
                    foreach (IProjectPage insertion in insertions)
                    {
                        StorageFolder previousFolder = await insertion.SourceFile.GetParentAsync();
                        await insertion.SourceFile.MoveAsync(AppDataService.ChangesFolder, insertion.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                        insertion.ChangeSourceFile(AppDataService.ChangesFolder, insertion.SourceFile);
                        moves.Add(new KeyValuePair<StorageFile, StorageFolder>(insertion.SourceFile, previousFolder));
                    }

                    // add pages
                    foreach (IProjectPage insertion in insertions.OrderBy(x => x.Index))
                    {
                        Pages.Insert(insertion.Index, insertion);
                        insertedPages.Add(insertion);
                    }

                    // update previews
                    List<ImagePage> imagePages = insertedPages.OfType<ImagePage>().ToList();
                    if (imagePages.Any())
                    {
                        await UpdatePagePreviewsAsync(imagePages);
                    }
                }
                catch (Exception exc)
                {
                    // roll back changes
                    foreach (KeyValuePair<StorageFile, StorageFolder> move in moves)
                    {
                        await move.Key.MoveAsync(move.Value, move.Key.Name, NameCollisionOption.GenerateUniqueName);
                    }

                    foreach (IProjectPage page in insertedPages)
                    {
                        Pages.Remove(page);
                        page.ChangeSourceFile(await page.SourceFile.GetParentAsync(), page.SourceFile);
                    }

                    throw new ProjectException(exc);
                }

                // update indices
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].Index = i;
                }

                PagesAdded?.Invoke(this, EventArgs.Empty);

                areFilesSaved = false;
            }
            finally
            {
                FinishEditing();
            }
        }

        public async Task RemovePagesAsync(List<IProjectPage> pages, bool isUndoing)
        {
            await StartEditingAsync();

            try
            {
                // keep track of changes in case of error
                List<StorageFile> deletedFiles = new();
                List<int> deletedIndices = new();

                // revertable section
                try
                {
                    // remove pages
                    foreach (IProjectPage page in pages)
                    {
                        deletedFiles.Add(page.SourceFile);
                        deletedIndices.Add(page.Index);

                        if (page is ImagePage imagePage)
                        {
                            if (isUndoing)
                            {
                                // move to redo folder
                                await page.SourceFile.MoveAsync(AppDataService.RedoFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                            }
                            else
                            {
                                // move to undo folder
                                await page.SourceFile.MoveAsync(AppDataService.UndoFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                            }

                            if (page.PreviewFile != null && page.PreviewFile != page.SourceFile)
                            {
                                await imagePage.ChangeAndCleanUpPreviewFileAsync(null);
                            }
                        }
                        
                        Pages.Remove(page);
                    }
                }
                catch (Exception exc)
                {
                    // roll back changes
                    foreach (StorageFile file in deletedFiles)
                    {
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    for (int i = 0; i < deletedIndices.Count; i++)
                    {
                        Pages.Insert(deletedIndices[i], pages[i]);
                    }
                    throw new ProjectException(exc);
                }

                // update indices
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].Index = i;
                }

                PagesRemoved?.Invoke(this, EventArgs.Empty);

                areFilesSaved = false;
            }
            finally
            {
                FinishEditing();
            }
        }

        protected static async Task<IProjectPage> CreatePageFromFileAsync(StorageFile file, int index, string? targetFileName, StorageFolder? targetFolder, bool keepSourceFile, StorageFolder pagesFolder, ImageFilter baseFilter, ImageFilter filter)
        {
            if (file == null) throw new ArgumentException("Can't create IProjectPage from null file");

            switch (file.FileType.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".tif":
                case ".tiff":
                    return await ImagePage.CreateAsync(file, targetFolder, index, targetFileName, keepSourceFile, pagesFolder, baseFilter, filter);
                case ".pdf":
                    throw new NotImplementedException();
                default:
                    throw new ArgumentException("Failed to create IProjectPage due to incompatible file format");
            }
        }

        /// <summary>
        /// Rotates image files.
        /// </summary>
        /// <param name="instructions">Which file to rotate how much.</param>
        /// <param name="overwriteFilesDirectly">Whether to overwrite files directly or create a separate file first and then delete the old one.</param>
        /// <param name="pagesFolder">Where to save the result to. Overrides <paramref name="overwriteFileDirectly"/> if set to a folder different from <paramref name="file"/>'s.</param>
        public static async Task RotateFilesAsync(Dictionary<StorageFile, BitmapRotation> instructions, bool overwriteFilesDirectly, StorageFolder pagesFolder)
        {
            foreach (KeyValuePair<StorageFile, BitmapRotation> instruction in instructions)
            {
                StorageFile oldFile = instruction.Key;
                await RotateFileAsync(instruction.Key, instruction.Value, overwriteFilesDirectly, pagesFolder);

                if (!overwriteFilesDirectly)
                {
                    // delete old file
                    _ = Task.Run(async () =>
                    {
                        await oldFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    });
                }
            }
        }

        /// <summary>
        /// Rotates pages.
        /// </summary>
        /// <param name="instructions">Which page to rotate how much.</param>
        /// <returns></returns>
        public async Task RotatePagesAsync(Dictionary<IProjectPage, BitmapRotation> instructions, StorageFolder pagesFolder)
        {
            foreach (KeyValuePair<IProjectPage, BitmapRotation> instruction in instructions)
            {
                if (instruction.Value == BitmapRotation.None) continue;

                StorageFile oldFile = instruction.Key.SourceFile;
                StorageFile newFile;

                await StartEditingAsync();
                try
                {
                    newFile = await RotateFileAsync(instruction.Key.SourceFile, instruction.Value, false, pagesFolder);
                }
                finally
                {
                    FinishEditing();
                }
                instruction.Key.ChangeSourceFile(pagesFolder, newFile);

                // delete old file
                if (oldFile != newFile)
                {
                    _ = Task.Run(async () =>
                    {
                        await oldFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    });
                }
            }
        }

        /// <summary>
        /// Rotates a file.
        /// </summary>
        /// <param name="file">The file to rotate.</param>
        /// <param name="rotation">The amount to rotate the file by.</param>
        /// <param name="overwriteFileDirectly">Whether to overwrite the file directly or create a separate file first and then delete the old one.</param>
        /// <param name="pagesFolder">Where to save the result to. Overrides <paramref name="overwriteFileDirectly"/> if set to a folder different from <paramref name="file"/>'s.</param>
        /// <returns>The resulting file. If <paramref name="overwriteFileDirectly"/> is true, <paramref name="file"/> is returned.</returns>
        private static async Task<StorageFile> RotateFileAsync(StorageFile file, BitmapRotation rotation, bool overwriteFileDirectly, StorageFolder pagesFolder)
        {
            try
            {
                if (rotation == BitmapRotation.None) return file;

                bool isFolderChanging = pagesFolder.Path == (await file.GetParentAsync()).Path;

                // create empty file to save to
                TaskCompletionSource<StorageFile> targetFileCreation = new();
                StorageFile targetFile = file;
                if (isFolderChanging || !overwriteFileDirectly)
                {
                    _ = Task.Run(async () =>
                    {
                        targetFileCreation.TrySetResult(await pagesFolder.CreateFileAsync(file.Name, CreationCollisionOption.GenerateUniqueName));
                    });
                }
                else
                {
                    targetFileCreation.TrySetResult(targetFile);
                }

                // perform edit
                using (IRandomAccessStream sourceFileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // load bitmap
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceFileStream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    // get target file
                    targetFile = await targetFileCreation.Task;
                    using (IRandomAccessStream targetFileStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        // rotate
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(file), targetFileStream);
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        encoder.BitmapTransform.Rotation = rotation;

                        await encoder.FlushAsync();
                    }
                }

                return targetFile;
            }
            catch (Exception e)
            {
                throw new ApplicationException("Rotating page failed", e);
            }
        }

        /// <summary>
        /// Rotates files.
        /// </summary>
        /// <param name="instructions">Which file to rotate how much.</param>
        /// <param name="overwriteFilesDirectly">Whether to overwrite the files directly or create a separate file first and then delete the old one.</param>
        /// <param name="pagesFolder">Where to save the result to. Overrides <paramref name="overwriteFileDirectly"/> if set to a folder different from <paramref name="file"/>'s.</param>
        public static async Task RotateFilesAsync(Dictionary<StorageFile, RotationIntent> instructions, bool overwriteFilesDirectly, StorageFolder pagesFolder)
        {
            // split instructions
            Dictionary<StorageFile, RotationIntent> autos = instructions.Where((x) => x.Value == RotationIntent.Automatic).ToDictionary();
            Dictionary<StorageFile, RotationIntent> predetermined = instructions.Where((x) => x.Value != RotationIntent.Automatic).ToDictionary();
            Dictionary<StorageFile, BitmapRotation> mergedInstructions = new();

            // get recommended rotations first
            if (autos.Count > 0)
            {
                foreach (KeyValuePair<StorageFile, RotationIntent> auto in autos)
                {
                    BitmapRotation? rotation = TesseractService.GetRecommendedRotation(auto.Key);
                    if (rotation != null)
                    {
                        mergedInstructions.Add(auto.Key, (BitmapRotation)rotation);
                    }
                }
            }

            // add predetermined instructions
            foreach (KeyValuePair<StorageFile, RotationIntent> instruction in predetermined)
            {
                mergedInstructions.Add(instruction.Key, RotationIntentToBitmapRotation(instruction.Value));
            }

            // process instructions
            await RotateFilesAsync(mergedInstructions, overwriteFilesDirectly, pagesFolder);
        }

        /// <summary>
        /// Rotates pages.
        /// </summary>
        /// <param name="instructions">Which page to rotate how much.</param>
        /// <param name="pagesFolder">Where to save the result to. Overrides <paramref name="overwriteFileDirectly"/> if set to a folder different from <paramref name="file"/>'s.</param>
        /// <returns>The actual rotations performed for each file.</returns>
        public async Task<Dictionary<IProjectPage, BitmapRotation>> RotatePagesAsync(Dictionary<IProjectPage, RotationIntent> instructions, StorageFolder pagesFolder)
        {
            // split instructions
            Dictionary<IProjectPage, RotationIntent> autos = instructions.Where((x) => x.Value == RotationIntent.Automatic).ToDictionary();
            Dictionary<IProjectPage, RotationIntent> predetermined = instructions.Where((x) => x.Value != RotationIntent.Automatic).ToDictionary();
            Dictionary<IProjectPage, BitmapRotation> mergedInstructions = new();

            // get recommended rotations first
            if (autos.Count > 0)
            {
                foreach (KeyValuePair<IProjectPage, RotationIntent> auto in autos)
                {
                    BitmapRotation? rotation = TesseractService.GetRecommendedRotation(auto.Key.SourceFile);
                    if (rotation != null)
                    {
                        mergedInstructions.Add(auto.Key, (BitmapRotation)rotation);
                    }
                }
            }

            // add predetermined instructions
            foreach (KeyValuePair<IProjectPage, RotationIntent> instruction in predetermined)
            {
                mergedInstructions.Add(instruction.Key, RotationIntentToBitmapRotation(instruction.Value));
            }

            // process instructions
            await RotatePagesAsync(mergedInstructions, pagesFolder);

            // update save state
            if (mergedInstructions.Count > 0 && mergedInstructions.Values.Any((x) => x != BitmapRotation.None))
            {
                areFilesSaved = false;
            }

            return mergedInstructions;
        }

        public async Task ApplyFilterToPagesAsync(List<ImagePage> pages, ImageFilter filter)
        {
            await StartEditingAsync();

            try
            {
                foreach (ImagePage page in pages)
                {
                    page.Filter = filter;
                }

                areFilesSaved = false;

                await UpdatePagePreviewsAsync(pages);
            }
            catch (Exception exc)
            {
                throw new ProjectException(exc);
            }
            finally
            {
                FinishEditing();
            }
        }

        public static async Task ApplyFilterAsync(IRandomAccessStream sourceStream, BitmapEncoder encoder, ImageFilter filter)
        {
            using (CanvasDevice device = CanvasDevice.GetSharedDevice())
            using (CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, sourceStream))
            using (CanvasRenderTarget renderer = new CanvasRenderTarget(device, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height, bitmap.Dpi))
            using (CanvasDrawingSession session = renderer.CreateDrawingSession())
            {
                switch (filter)
                {
                    case ImageFilter.Grayscale:
                        GrayscaleEffect grayscaleEffect = new GrayscaleEffect
                        {
                            Source = bitmap,
                        };

                        session.DrawImage(grayscaleEffect);
                        session.Flush();
                        break;
                    case ImageFilter.Monochrome:
                        grayscaleEffect = new GrayscaleEffect
                        {
                            Source = bitmap,
                        };

                        DiscreteTransferEffect thresholdEffect = new DiscreteTransferEffect
                        {
                            Source = grayscaleEffect,
                            RedTable = [0, 1],
                            GreenTable = [0, 1],
                            BlueTable = [0, 1]
                        };

                        session.DrawImage(thresholdEffect);
                        session.Flush();
                        break;
                    default:
                        throw new ArgumentException("Can't apply filter for " + filter);
                }

                // encode result
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                         (uint)renderer.SizeInPixels.Width, (uint)renderer.SizeInPixels.Height,
                                         renderer.Dpi, renderer.Dpi, renderer.GetPixelBytes());
                await encoder.FlushAsync();
            }
        }
        
        protected async Task UpdatePagePreviewsAsync(List<ImagePage> pages)
        {
            try
            {
                foreach (ImagePage page in pages)
                {
                    if (!page.IsUsingDestructiveEffects)
                    {
                        // page doesn't require separate preview file
                        await page.ChangeAndCleanUpPreviewFileAsync(null);
                        continue;
                    }

                    // create preview file
                    StorageFile targetFile = await AppDataService.PreviewFolder.CreateFileAsync(page.SourceFile.Name, CreationCollisionOption.GenerateUniqueName);

                    // apply effects
                    using (var sourceStream = await page.SourceFile.OpenAsync(FileAccessMode.Read))
                    using (var targetStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(targetFile), targetStream);
                        await ApplyFilterAsync(sourceStream, encoder, page.Filter);
                    }

                    // update preview file
                    await page.ChangeAndCleanUpPreviewFileAsync(targetFile);
                }
            }
            catch (Exception exc)
            {
                throw new ProjectException(exc);
            }
        }

        public static Guid GetBitmapEncoderIdForFile(StorageFile file)
        {
            switch (file.FileType.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    return BitmapEncoder.JpegEncoderId;
                case ".png":
                    return BitmapEncoder.PngEncoderId;
                case ".tif":
                case ".tiff":
                    return BitmapEncoder.TiffEncoderId;
                case ".bmp":
                    return BitmapEncoder.BmpEncoderId;
                default:
                    throw new ArgumentException($"Failed to get BitmapEncoder ID for file");
            }
        }

        private async Task StartEditingAsync()
        {
            await projectObjectSemaphore.WaitAsync();
            await changesFolderSemaphore.WaitAsync();
        }

        private void FinishEditing()
        {
            projectObjectSemaphore.Release();
            changesFolderSemaphore.Release();
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record ProjectFileInsertion(StorageFile File, int Index, string? FileName, StorageFolder? TargetFolder, ImageFilter BaseFilter, ImageFilter Filter);
}
