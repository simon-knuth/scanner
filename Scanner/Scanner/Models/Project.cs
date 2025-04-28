using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI.Helpers;
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
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.Models
{
    public partial class Project : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private static readonly ITesseractService TesseractService = Ioc.Default.GetRequiredService<ITesseractService>();
        #endregion

        #region Events
        public event EventHandler PagesAdded;
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

        public StorageFolder? TargetFolder;

        public StorageFile? TargetFile;

        public FileNameInfo? FileNameInfo { get; private set; }

        public bool IsPdf => Format == TargetFormat.PDF;

        private bool _areFilesSaved;
        private bool areFilesSaved
        {
            get => _areFilesSaved;
            set
            {
                SetProperty(ref _areFilesSaved, value);
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        private bool _hasFileNameBeenApplied = true;
        private bool hasFileNameBeenApplied
        {
            get => _hasFileNameBeenApplied;
            set
            {
                SetProperty(ref _hasFileNameBeenApplied, value);
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        private bool saveProcessWaitingToStart;

        private SemaphoreSlim saveSemaphore = new(1, 1);                // needed to run a save process

        private SemaphoreSlim projectObjectSemaphore = new(1, 1);       // needed to modify the Project object
        private SemaphoreSlim projectFolderSemaphore = new(1, 1);       // needed to modify the Project folder
        private SemaphoreSlim changesFolderSemaphore = new(1, 1);       // needed to modify the Changes folder


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private Project(IList<IProjectPage> pages, TargetFormat format, string targetFileName, StorageFolder targetFolder)
        {
            Pages = new ObservableCollection<IProjectPage>(pages);
            Format = format;
            
            if (IsPdf)
            {
                // folder saved at project level for PDF and page level for all other formats
                TargetFolder = targetFolder;
                FileNameInfo = new FileNameInfo(targetFileName);
                FileNameInfo.NameChanged += FileNameInfo_NameChanged;
                hasFileNameBeenApplied = false;
            }
            else
            {
                foreach (IProjectPage page in pages)
                {
                    if (page is ImagePage imagePage)
                    {
                        imagePage.FileNameInfo.NameChanged += PageFileNameInfo_NameChanged;
                    }
                }
            }
        }

        public static async Task<Project> CreateAsync(IList<StorageFile> files, TargetFormat format, string targetFileName, StorageFolder targetFolder, bool keepSourceFiles)
        {
            // empty project folders
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.ChangesFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);

            // create pages
            List<IProjectPage> pages = new();
            for (int i = 0; i < files.Count; i++)
            {
                pages.Add(await CreatePageFromFileAsync(files[i], i, targetFileName, targetFolder, keepSourceFiles, AppDataService.ProjectFolder));
            }

            // create project
            return new Project(pages, format, targetFileName, targetFolder);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
                        IProjectPage page = await CreatePageFromFileAsync(insertion.File, insertion.Index, insertion.FileName, insertion.TargetFolder, keepSourceFiles, AppDataService.ChangesFolder);
                        copiedFiles.Add(page.SourceFile);

                        preparedInsertions.Add(new KeyValuePair<IProjectPage, int>(page, insertion.Index));
                    }

                    // add pages
                    foreach (KeyValuePair<IProjectPage, int> insertion in preparedInsertions)
                    {
                        Pages.Insert(insertion.Value, insertion.Key);
                        insertedPages.Add(insertion.Key);
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
                    foreach (IProjectPage insertion in insertions)
                    {
                        Pages.Insert(insertion.Index, insertion);
                        insertedPages.Add(insertion);
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

                        if (page is ImagePage)
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

                areFilesSaved = false;
            }
            finally
            {
                FinishEditing();
            }
        }

        private static async Task<IProjectPage> CreatePageFromFileAsync(StorageFile file, int index, string? fileName, StorageFolder? targetFolder, bool keepSourceFile, StorageFolder pagesFolder)
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
                    return await ImagePage.CreateAsync(file, index, fileName, targetFolder, keepSourceFile, pagesFolder);
                case ".pdf":
                    throw new NotImplementedException();
                default:
                    throw new ArgumentException("Failed to create IProjectPage due to incompatible file format");
            }
        }

        public async Task SaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            // ensure maximum of one thread waiting to save
            if (saveProcessWaitingToStart)
            {
                if (LatestSaveProcess != null)
                {
                    await LatestSaveProcess.Task;
                }
                return;
            }
            saveProcessWaitingToStart = true;

            // enable waiting for save process (even if it is waiting to start)
            TaskCompletionSource saveProcess = new();
            LatestSaveProcess = saveProcess;

            // save
            await Task.Run(async () =>
            {
                await saveSemaphore.WaitAsync();

                try
                {
                    uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
                    {
                        IsSaving = true;
                        saveProcessWaitingToStart = false;
                    });

                    // apply actual file changes
                    if (!areFilesSaved)
                    {
                        // lock data
                        await projectObjectSemaphore.WaitAsync();
                        await projectFolderSemaphore.WaitAsync();
                        await changesFolderSemaphore.WaitAsync();

                        // commit changes
                        foreach (IProjectPage page in Pages)
                        {
                            if (page.CommitNeeded)
                            {
                                // copy file to project folder
                                StorageFile? fileToDelete = page.SourceFile;
                                StorageFile newSourceFile;
                                if (page.OutOfDateSourceFile != null)
                                {
                                    newSourceFile = await page.SourceFile.CopyAsync(AppDataService.ProjectFolder, page.OutOfDateSourceFile.Name, NameCollisionOption.ReplaceExisting);
                                    page.ClearOutOfDateSourceFile();
                                }
                                else
                                {
                                    newSourceFile = await page.SourceFile.CopyAsync(AppDataService.ProjectFolder);
                                }

                                // update page
                                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
                                {
                                    page.ChangeSourceFile(AppDataService.ProjectFolder, newSourceFile);
                                });

                                // delete old file
                                if (fileToDelete != null)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        await fileToDelete.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                    });
                                }
                            }
                        }

                        // take snapshot
                        IProjectSnapshot? snapshot = null;
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
                        {
                            if (IsPdf)
                            {
                                snapshot = new PdfProjectSnapshot(this);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        });
                        if (snapshot == null) throw new ApplicationException("Failed to save Project (snapshot is null)");

                        // continue processing edits during save process
                        projectObjectSemaphore.Release();
                        changesFolderSemaphore.Release();

                        // save
                        bool saveResult = false;
                        if (snapshot is PdfProjectSnapshot pdfSnapshot)
                        {
                            (saveResult, StorageFile? savedFile) = await pdfSnapshot.TrySaveAsync();
                            TargetFile = savedFile;

                            // update file name
                            if (saveResult && savedFile != null)
                            {
                                await FileNameInfo!.UpdateNamesAsync(FileNameInfo!.DesiredName, savedFile.Name, uiDispatcherQueue);
                            }
                        }
                        projectFolderSemaphore.Release();

                        if (!saveResult) throw new ApplicationException("Failed to save Project");
                    }

                    // apply file name
                    if (IsPdf)
                    {
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                        {
                            if (TargetFile!.Name != FileNameInfo!.DesiredName)
                            {
                                await TargetFile.RenameAsync(FileNameInfo!.DesiredName, NameCollisionOption.GenerateUniqueName);
                                await FileNameInfo!.UpdateNamesAsync(TargetFile.Name, TargetFile.Name, uiDispatcherQueue);
                                hasFileNameBeenApplied = true;
                            }
                        });
                    }
                    else
                    {
                        await projectObjectSemaphore.WaitAsync();

                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                        {
                            foreach (IProjectPage page in Pages)
                            {
                                if (page is ImagePage imagePage)
                                {
                                    if (imagePage.FileNameInfo!.DesiredName != imagePage.FileNameInfo.ActualName)
                                    {
                                        await imagePage.TargetFile!.RenameAsync(imagePage.FileNameInfo.DesiredName, NameCollisionOption.GenerateUniqueName);
                                        await FileNameInfo!.UpdateNamesAsync(imagePage.TargetFile.Name, imagePage.TargetFile.Name, uiDispatcherQueue);
                                        hasFileNameBeenApplied = true;
                                    }
                                }
                            }
                        });

                        projectObjectSemaphore.Release();
                    }
                }
                catch (Exception exc)
                {
                    saveProcess.TrySetException(exc);
                    throw;
                }
                finally
                {
                    uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
                    {
                        IsSaving = false;

                        // update saved state
                        if (!saveProcessWaitingToStart)
                        {
                            areFilesSaved = true;
                        }

                        saveProcess.TrySetResult();
                    });
                    saveSemaphore.Release();
                }
            });
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

        private static Guid GetBitmapEncoderIdForFile(StorageFile file)
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

        private void FileName_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Models.FileNameInfo.DesiredName):
                case nameof(Models.FileNameInfo.ActualName):
                    hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
                    break;
            }
        }

        private void FileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
        }

        private void PageFileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            if (sender == null) return;

            if (((FileNameInfo)sender).DesiredName != ((FileNameInfo)sender).ActualName)
            {
                hasFileNameBeenApplied = false;
            }
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record ProjectFileInsertion(StorageFile File, int Index, string? FileName, StorageFolder? TargetFolder);
}
