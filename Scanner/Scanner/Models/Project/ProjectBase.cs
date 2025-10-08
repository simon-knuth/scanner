using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
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
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.WebUI;
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
        protected static readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetRequiredService<ICopilotRuntimeService>();
        protected static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        protected static readonly ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
        protected static readonly ITesseractService TesseractService = Ioc.Default.GetRequiredService<ITesseractService>();
        #endregion

        #region Events
        public event EventHandler PagesAdded;
        public event EventHandler PagesRemoved;
        #endregion

        [ObservableProperty]
        private bool isSaving;

        public bool IsSaved => areFilesSaved && hasFileNameBeenApplied;

        public TaskCompletionSource<bool>? LatestSaveProcess;

        public ObservableCollection<IProjectPage> Pages
        {
            get;
            private set;
        }

        /// <summary>
        /// The <see cref="ScanOptions"/> in use when this project was created in the first place.
        /// </summary>
        public readonly ScanOptions? InitialScanOptions;
        public readonly TargetFormat Format;

        public bool IsPdf => Format == TargetFormat.PDF;

        private bool _areFilesSaved;
        protected bool areFilesSaved
        {
            get => _areFilesSaved;
            set
            {
                if (!SetProperty(ref _areFilesSaved, value))
                    return;

                OnPropertyChanged(nameof(IsSaved));

                if (!value && LatestSaveProcess != null)
                    hasMadeChangesDuringSaveProcess = true;
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
        protected bool hasMadeChangesDuringSaveProcess;

        protected SemaphoreSlim saveSemaphore = new(1, 1);                // needed to run a save process

        protected SemaphoreSlim projectObjectSemaphore = new(1, 1);       // needed to modify the Project object
        protected SemaphoreSlim projectFolderSemaphore = new(1, 1);       // needed to modify the Project folder
        protected SemaphoreSlim changesFolderSemaphore = new(1, 1);       // needed to modify the Changes folder


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected ProjectBase(IList<IProjectPage> pages, TargetFormat targetFormat, ScanOptions initialScanOptions)
        {
            Pages = new ObservableCollection<IProjectPage>(pages);
            Format = targetFormat;
            InitialScanOptions = initialScanOptions;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public abstract Task DeleteAsync();
        public abstract Task SaveAsync(bool saveAs, DispatcherQueue uiDispatcherQueue);


        public async Task<List<IProjectPage>> AddFilesAsync(List<ProjectFileInsertion> insertions, bool keepSourceFiles, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (insertions.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            await StartEditingAsync();

            try
            {
                return await AddFilesInternalAsync(insertions, keepSourceFiles, uiDispatcherQueue);
            }
            finally
            {
                FinishEditing();
                process.TrySetResult();
            }
        }

        private async Task<List<IProjectPage>> AddFilesInternalAsync(List<ProjectFileInsertion> insertions, bool keepSourceFiles, DispatcherQueue uiDispatcherQueue)
        {
            // keep track of changes in case of error
            List<StorageFile> copiedFiles = new();
            List<KeyValuePair<IProjectPage, int>> preparedInsertions = new();
            List<IProjectPage> insertedPages = new();

            // revertable section
            try
            {
                // add files
                await Task.Run(async () =>
                {
                    foreach (ProjectFileInsertion insertion in insertions)
                    {
                        IProjectPage page = await CreatePageFromFileAsync(insertion.File, insertion.Index, IsPdf ? null : insertion.FileName, insertion.TargetFolder, keepSourceFiles, AppDataService.ChangesFolder, insertion.BaseFilter, insertion.Filter, insertion.Brightness, insertion.Contrast);
                        copiedFiles.Add(page.SourceFile);

                        preparedInsertions.Add(new KeyValuePair<IProjectPage, int>(page, insertion.Index));
                    }
                });

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
                    await GeneratePagePreviewsAsync(imagePages, uiDispatcherQueue);
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

                throw new ActionFailedAndRolledBackException(exc);
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

        public async Task AddPagesAsync(List<IProjectPage> insertions, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (insertions.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

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
                        await insertion.ChangeSourceFileAsync(AppDataService.ChangesFolder, insertion.SourceFile, uiDispatcherQueue);
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
                        await GeneratePagePreviewsAsync(imagePages, uiDispatcherQueue);
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
                        await page.ChangeSourceFileAsync(await page.SourceFile.GetParentAsync(), page.SourceFile, uiDispatcherQueue);
                    }

                    throw new ActionFailedAndRolledBackException(exc);
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
                process.TrySetResult();
            }
        }

        /// <summary>
        /// Removes a given set of pages from the project.
        /// </summary>
        /// <param name="pages">
        /// The pages to remove.
        /// </param>
        /// <param name="isUndoing">
        /// Whether to move the source files of the removed pages to the Redo folder.
        /// </param>
        /// <exception cref="ActionFailedAndRolledBackException"></exception>
        public async Task RemovePagesAsync(List<IProjectPage> pages, bool isUndoing, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (pages.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

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
                                await imagePage.UpdatePreviewFileAsync(null, uiDispatcherQueue);
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
                    throw new ActionFailedAndRolledBackException(exc);
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
                process.TrySetResult();
            }
        }

        protected static async Task<IProjectPage> CreatePageFromFileAsync(StorageFile file, int index, string? targetFileName, StorageFolder? targetFolder, bool keepSourceFile, StorageFolder pagesFolder, ImageFilter baseFilter, ImageFilter filter, int brightness, int contrast)
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
                    return await ImagePage.CreateAsync(file, targetFolder, index, targetFileName, keepSourceFile, pagesFolder, baseFilter, filter, brightness, contrast);
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
        public async Task RotatePagesAsync(Dictionary<IProjectPage, BitmapRotation> instructions, StorageFolder pagesFolder, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (instructions.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

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
                await instruction.Key.ChangeSourceFileAsync(pagesFolder, newFile, uiDispatcherQueue);

                // delete old file
                if (oldFile != newFile)
                {
                    _ = Task.Run(async () =>
                    {
                        await oldFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    });
                }
            }

            process.TrySetResult();
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

                bool isFolderChanging = pagesFolder.Path != (await file.GetParentAsync()).Path;

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
                    using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    // get target file
                    targetFile = await targetFileCreation.Task;
                    using IRandomAccessStream targetFileStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite);

                    // rotate
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(file), targetFileStream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    encoder.BitmapTransform.Rotation = rotation;

                    await encoder.FlushAsync();
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
        /// <param name="uiDispatcherQueue">The UI dispatcher queue.</param>
        /// <returns>The actual rotations performed for each file.</returns>
        public async Task<Dictionary<IProjectPage, BitmapRotation>> RotatePagesAsync(Dictionary<IProjectPage, RotationIntent> instructions, StorageFolder pagesFolder, DispatcherQueue uiDispatcherQueue)
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
            await RotatePagesAsync(mergedInstructions, pagesFolder, uiDispatcherQueue);

            // update dimensions
            foreach (KeyValuePair<IProjectPage, BitmapRotation> instruction in mergedInstructions)
            {
                if (instruction.Value is BitmapRotation.Clockwise90Degrees or BitmapRotation.Clockwise270Degrees)
                {
                    uint width = instruction.Key.Width;
                    instruction.Key.Width = instruction.Key.Height;
                    instruction.Key.Height = width;
                }
            }

            // update previews
            await GeneratePagePreviewsAsync(mergedInstructions.Keys.ToList(), uiDispatcherQueue);

            // update save state
            if (mergedInstructions.Count > 0 && mergedInstructions.Values.Any((x) => x != BitmapRotation.None))
            {
                areFilesSaved = false;
            }

            return mergedInstructions;
        }

        /// <summary>
        /// Applies an <see cref="ImageFilter"/> to pages.
        /// </summary>
        /// <param name="pages">The pages to apply the filter to.</param>
        /// <param name="filter">The filter to apply.</param>
        public async Task ApplyFilterToPagesAsync(List<ImagePage> pages, ImageFilter filter, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (pages.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            await StartEditingAsync();

            try
            {
                foreach (ImagePage page in pages)
                {
                    page.Filter = filter;
                }

                areFilesSaved = false;

                await GeneratePagePreviewsAsync([.. pages.Cast<IProjectPage>()], uiDispatcherQueue);
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
            finally
            {
                FinishEditing();
                process.TrySetResult();
            }
        }

        /// <summary>
        /// Renders a bitmap with effects (<see cref="ImageFilter"/>, brightness, contrast) applied.
        /// </summary>
        /// <param name="sourceStream">The bitmap source stream.</param>
        /// <param name="encoder">The encoder load the resulting pixel data into.</param>
        /// <param name="filter">The filter to render.</param>
        /// <param name="brightness">The brightness adjustment to apply.</param>
        /// <param name="contrast">The contrast adjustment to apply.</param>
        public static async Task ApplyEffectsAsync(IRandomAccessStream sourceStream, BitmapEncoder encoder, ImageFilter filter, int brightness, int contrast)
        {
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            using CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, sourceStream);
            using CanvasRenderTarget renderer = new CanvasRenderTarget(device, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height, bitmap.Dpi);
            using CanvasDrawingSession session = renderer.CreateDrawingSession();

            ICanvasImage effectChain = ImageEffectsHelper.CreateEffectChain(bitmap, filter, brightness, contrast);
            session.DrawImage(effectChain);
            session.Flush();

            // encode result
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                     (uint)renderer.SizeInPixels.Width, (uint)renderer.SizeInPixels.Height,
                                     renderer.Dpi, renderer.Dpi, renderer.GetPixelBytes());
            await encoder.FlushAsync();
        }
        
        protected async Task GeneratePagePreviewsAsync(List<ImagePage> pages, DispatcherQueue uiDispatcherQueue)
        {
            try
            {
                foreach (ImagePage page in pages)
                {
                    if (!page.IsUsingDestructiveEffects)
                    {
                        // page doesn't require separate preview file
                        await page.UpdatePreviewFileAsync(null, uiDispatcherQueue);
                        continue;
                    }

                    // create preview file
                    StorageFile targetFile = await AppDataService.PreviewFolder.CreateFileAsync(page.SourceFile.Name, CreationCollisionOption.GenerateUniqueName);

                    // apply effects
                    using (var sourceStream = await page.SourceFile.OpenAsync(FileAccessMode.Read))
                    using (var targetStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(targetFile), targetStream);
                        await ApplyEffectsAsync(sourceStream, encoder, page.Filter, page.Brightness, page.Contrast);
                    }

                    // update preview file
                    await page.UpdatePreviewFileAsync(targetFile, uiDispatcherQueue);
                }
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
        }

        protected Task GeneratePagePreviewsAsync(List<IProjectPage> pages, DispatcherQueue uiDispatcherQueue)
        {
            List<ImagePage> imagePages = [.. pages.Where((x) => x is ImagePage).Cast<ImagePage>()];
            return GeneratePagePreviewsAsync(imagePages, uiDispatcherQueue);
        }

        public async Task<List<AppliedCrop>> CropPagesAsync(List<IProjectPage> pages, Rect cropRegion, StorageFolder pagesFolder, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (pages.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            List<AppliedCrop> result = [];

            foreach (IProjectPage page in pages)
            {
                StorageFile oldFile = page.SourceFile;
                StorageFile? newFile = null;
                AppliedCrop appliedCrop = new(page, oldFile, page.Width, page.Height);

                await StartEditingAsync();
                try
                {
                    await Task.Run(async () => newFile = await CropFileAsync(page.SourceFile, cropRegion, false, pagesFolder));
                    page.Width = (uint)Math.Round(cropRegion.Width);
                    page.Height = (uint)Math.Round(cropRegion.Height);
                }
                finally
                {
                    FinishEditing();
                }
                await page.ChangeSourceFileAsync(pagesFolder, newFile!, uiDispatcherQueue);

                // move to undo folder
                await oldFile.MoveAsync(AppDataService.UndoFolder, oldFile.Name, NameCollisionOption.GenerateUniqueName);

                result.Add(appliedCrop);
            }

            // update previews
            await GeneratePagePreviewsAsync(pages, uiDispatcherQueue);

            process.TrySetResult();
            return result;
        }

        public async Task<List<IProjectPage>> CropPagesAsCopyAsync(List<IProjectPage> pages, Rect cropRegion, StorageFolder pagesFolder, DispatcherQueue uiDispatcherQueue)
        {
            TaskCompletionSource process = new();
            if (pages.Count > 1)
                Messenger.Send(new ShowMultiEditInProgressDialogMessage(process.Task));

            List<IProjectPage> result = [];

            foreach (IProjectPage page in pages)
            {
                StorageFile newFile;

                await StartEditingAsync();
                try
                {
                    // copy file
                    newFile = await page.SourceFile.CopyAsync(pagesFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                    
                    // crop
                    await Task.Run(async () => await CropFileAsync(newFile, cropRegion, true, pagesFolder));
                    page.Width = (uint)Math.Round(cropRegion.Width);
                    page.Height = (uint)Math.Round(cropRegion.Height);

                    // generate page
                    ImagePage? imagePage = page as ImagePage;
                    string? fileName = imagePage?.FileNameInfo?.DesiredName;
                    StorageFolder? targetFolder = imagePage?.TargetFolder;
                    ProjectFileInsertion insertion = new(newFile, page.Index + 1, fileName, targetFolder,
                        imagePage?.BaseFilter ?? ImageFilter.None, imagePage?.Filter ?? ImageFilter.None,
                        imagePage?.Brightness ?? AppConfig.DefaultBrightness, imagePage?.Contrast ?? AppConfig.DefaultContrast);
                    result.AddRange(await AddFilesInternalAsync([insertion], false, uiDispatcherQueue));
                }
                finally
                {
                    FinishEditing();
                }
            }

            // update previews
            await GeneratePagePreviewsAsync(pages, uiDispatcherQueue);

            process.TrySetResult();
            return result;
        }

        private static async Task<StorageFile> CropFileAsync(StorageFile file, Rect cropRegion, bool overwriteFileDirectly, StorageFolder pagesFolder)
        {
            try
            {
                bool isFolderChanging = pagesFolder.Path != (await file.GetParentAsync()).Path;

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
                cropRegion.X = Math.Max(cropRegion.X, 0);
                cropRegion.Y = Math.Max(cropRegion.Y, 0);
                uint x = (uint)Math.Floor(cropRegion.X);
                uint y = (uint)Math.Floor(cropRegion.Y);
                uint width = (uint)Math.Floor(cropRegion.Width);
                uint height = (uint)Math.Floor(cropRegion.Height);

                using (IRandomAccessStream sourceStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                    using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    targetFile = await targetFileCreation.Task;
                    using IRandomAccessStream targetStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite);
                    
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(file), targetStream);
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

                return targetFile;
            }
            catch (Exception e)
            {
                throw new ApplicationException("Cropping page failed", e);
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
    public record ProjectFileInsertion(StorageFile File, int Index, string? FileName, StorageFolder? TargetFolder, ImageFilter BaseFilter, ImageFilter Filter, int Brightness, int Contrast);
    public record AppliedCrop(IProjectPage Page, StorageFile PreviousFile, uint PreviousWidth, uint PreviousHeight);
}
