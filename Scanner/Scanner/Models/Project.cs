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
using Windows.Storage;
using WinRT.Interop;

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
    }
}
