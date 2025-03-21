using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
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

        [ObservableProperty]
        private bool isSaved;

        public TaskCompletionSource? LatestSaveProcess;

        private bool saveProcessWaitingToStart;
        private SemaphoreSlim saveSemaphore = new SemaphoreSlim(1, 1);

        public ObservableCollection<IProjectPage> Pages
        {
            get;
            private set;
        }

        public TargetFormat Format;

        public bool IsPdf => Format == TargetFormat.PDF;

        public StorageFolder TargetFolder;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private Project(IList<IProjectPage> pages, TargetFormat format)
        {
            Pages = new ObservableCollection<IProjectPage>(pages);
            Format = format;
        }

        public static async Task<Project> CreateAsync(IList<StorageFile> files, TargetFormat format)
        {
            // empty folder
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);

            // create pages
            List<IProjectPage> pages = new();
            for (int i = 0; i < files.Count; i++)
            {
                pages.Add(await CreatePageFromFileAsync(files[i], i));
            }

            // create project
            return new Project(pages, format);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<List<IProjectPage>> AddFilesAsync(Dictionary<StorageFile, int> insertions)
        {
            // keep track of changes in case of error
            List<StorageFile> copiedFiles = new();
            List<KeyValuePair<IProjectPage, int>> preparedInsertions = new();
            List<IProjectPage> insertedPages = new();

            try
            {
                // add files
                foreach (KeyValuePair<StorageFile, int> insertion in insertions)
                {
                    IProjectPage page = await CreatePageFromFileAsync(insertion.Key, insertion.Value);
                    copiedFiles.Add(page.SourceFile);

                    preparedInsertions.Add(new KeyValuePair<IProjectPage, int>(page, insertion.Value));
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

            IsSaved = false;
            return insertedPages;
        }

        public async Task RemovePagesAsync(List<IProjectPage> pages, bool isUndoing)
        {
            // keep track of changes in case of error
            List<StorageFile> deletedFiles = new();
            List<int> deletedIndices = new();

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

            IsSaved = false;
        }

        private static async Task<IProjectPage> CreatePageFromFileAsync(StorageFile file, int index)
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
                    return await ImagePage.CreateAsync(file, index);
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
                    await Task.Delay(3000);
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
                            IsSaved = true;
                        }

                        saveProcess.TrySetResult();
                    });
                    saveSemaphore.Release();
                }
            });
        }

        public async Task RotatePagesAsync(Dictionary<IProjectPage, BitmapRotation> instructions)
        {
            foreach (KeyValuePair<IProjectPage, BitmapRotation> instruction in instructions)
            {
                try
                {
                    if (instruction.Value == BitmapRotation.None) continue;

                    // create empty file to save to
                    TaskCompletionSource<StorageFile> targetFileCreation = new();
                    StorageFile targetFile;
                    _ = Task.Run(async () =>
                    {
                        targetFileCreation.TrySetResult(await AppDataService.ProjectFolder.CreateFileAsync(instruction.Key.SourceFile.Name, CreationCollisionOption.GenerateUniqueName));
                    });

                    // perform edit
                    using (IRandomAccessStream sourceFileStream = await instruction.Key.SourceFile.OpenAsync(FileAccessMode.Read))
                    {
                        // load bitmap
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceFileStream);
                        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                        // get target file
                        targetFile = await targetFileCreation.Task;
                        using (IRandomAccessStream targetFileStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            // rotate
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderIdForFile(instruction.Key.SourceFile), targetFileStream);
                            encoder.SetSoftwareBitmap(softwareBitmap);
                            encoder.BitmapTransform.Rotation = instruction.Value;

                            await encoder.FlushAsync();
                        }
                    }

                    // delete old page
                    StorageFile oldFile = instruction.Key.SourceFile;
                    _ = Task.Run(async () =>
                    {
                        await oldFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    });

                    // update page
                    instruction.Key.ChangeSourceFile(targetFile, new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.ProjectFolder, targetFile.Name)));
                    instruction.Key.Rotation = CombineRotations(instruction.Key.Rotation, instruction.Value);
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Rotating page failed", e);
                }
            }
        }

        public async Task RotatePagesAsync(Dictionary<IProjectPage, RotationIntent> instructions)
        {
            // split instructions
            Dictionary<IProjectPage, RotationIntent> autos = instructions.Where((x) => x.Key.RecommendedRotation == null && x.Value == RotationIntent.Automatic).ToDictionary();
            Dictionary<IProjectPage, RotationIntent> predetermined = instructions.Where((x) => x.Key.RecommendedRotation != null || x.Value != RotationIntent.Automatic).ToDictionary();
            Dictionary<IProjectPage, BitmapRotation> mergedInstructions = new();

            // get recommended rotations first
            if (autos.Count > 0)
            {
                List<Task<KeyValuePair<IProjectPage, BitmapRotation?>>> tasks = new();
                foreach (KeyValuePair<IProjectPage, RotationIntent> auto in autos)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        return new KeyValuePair<IProjectPage, BitmapRotation?>(auto.Key, TesseractService.GetRecommendedRotation(auto.Key.SourceFile));
                    }));
                }
                KeyValuePair<IProjectPage, BitmapRotation?>[] recommendations = await Task.WhenAll(tasks);

                // add instructions
                foreach (KeyValuePair<IProjectPage, BitmapRotation?> recommendation in recommendations)
                {
                    if (recommendation.Value != null)
                    {
                        mergedInstructions.Add(recommendation.Key, (BitmapRotation)recommendation.Value);
                    }

                    // save recommended rotation for page
                    recommendation.Key.RecommendedRotation = recommendation.Value;
                }
            }

            // add predetermined instructions
            foreach (KeyValuePair<IProjectPage, RotationIntent> instruction in predetermined)
            {
                if (instruction.Value == RotationIntent.Automatic && instruction.Key.RecommendedRotation != null)
                {
                    // already got recommendation earlier
                    mergedInstructions.Add(instruction.Key, SubtractRotations(instruction.Key.Rotation, (BitmapRotation)instruction.Key.RecommendedRotation));
                }
                else
                {
                    mergedInstructions.Add(instruction.Key, RotationIntentToBitmapRotation(instruction.Value));
                }
            }

            // process instructions
            await RotatePagesAsync(mergedInstructions);
        }

        private Guid GetBitmapEncoderIdForFile(StorageFile file)
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
    }
}
