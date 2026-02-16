using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models.Interfaces;
using Scanner.Models.Project;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.Models
{
    /// <summary>
    /// A project that produces one file per page.
    /// </summary>
    public partial class MultiFileProject : ProjectBase
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Pages that have been removed from the project and need to have their target files deleted.
        /// </summary>
        public List<IProjectPage> PagesWithTargetFilesToDelete { get; } = [];


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private MultiFileProject(IList<IProjectPage> pages, TargetFormat format, ScanOptions initialScanOptions) : base(pages, format, initialScanOptions)
        {
            foreach (IProjectPage page in pages)
            {
                if (page is ImagePage imagePage && imagePage.FileNameInfo != null)
                {
                    imagePage.FileNameInfo.NameChanged += PageFileNameInfo_NameChanged;
                }
            }
        }

        public static async Task<ProjectBase> CreateAsync(MultiFileProjectCreationData creationData, bool keepSourceFiles, bool isAlreadySaved, DispatcherQueue uiDispatcherQueue)
        {
            // empty project folders
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.ChangesFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
            await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);

            // create pages
            List<IProjectPage> pages = new();
            for (int i = 0; i < creationData.Pages.Count; i++)
            {
                PageCreationData pageData = creationData.Pages[i];
                pages.Add(await CreatePageFromFileAsync(pageData.File, i, pageData.TargetFileName, pageData.TargetFile, pageData.TargetFolder, keepSourceFiles, AppDataService.ProjectFolder, pageData.BaseFilter, pageData.Filter, pageData.Brightness, pageData.Contrast));
            }

            // create project and update previews
            MultiFileProject project = new MultiFileProject(pages, creationData.Format, creationData.InitialScanOptions);
            await project.GeneratePagePreviewsAsync(pages.OfType<ImagePage>().ToList(), uiDispatcherQueue);

            if (isAlreadySaved)
                project.areFilesSaved = true;

            return project;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public override async Task DeleteAsync()
        {
            // wait for save processes to end
            saveProcessWaitingToStart = true;
            if (LatestSaveProcess != null)
            {
                await LatestSaveProcess.Task;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // delete files
            try
            {
                foreach (IProjectPage page in Pages)
                {
                    ImagePage imagePage = (ImagePage)page;
                    if (imagePage.TargetFile != null)
                    {
                        FileHandle targetFile = imagePage.TargetFile;
                        imagePage.TargetFile = null;
                        targetFile.FileStream.Dispose();
                        await targetFile.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
            finally
            {
                projectObjectSemaphore.Release();
                saveSemaphore.Release();
            }
        }

        public async override Task<bool> SaveAsync(bool saveAs, DispatcherQueue uiDispatcherQueue)
        {
            // ensure maximum of one thread waiting to save
            if (saveProcessWaitingToStart)
            {
                if (LatestSaveProcess != null)
                {
                    return await LatestSaveProcess.Task;
                }
                return true;
            }
            saveProcessWaitingToStart = true;

            // enable waiting for save process (even if it is waiting to start)
            TaskCompletionSource<bool> saveProcess = new();
            LatestSaveProcess = saveProcess;

            // save
            return await SaveInternalAsync(saveAs, [.. Pages], saveProcess, uiDispatcherQueue);
        }

        public async Task<bool> SaveAsSinglePageAsync(IProjectPage page, DispatcherQueue uiDispatcherQueue)
        {
            // ensure maximum of one thread waiting to save
            if (saveProcessWaitingToStart)
            {
                if (LatestSaveProcess != null)
                {
                    return await LatestSaveProcess.Task;
                }
                return true;
            }
            saveProcessWaitingToStart = true;

            // enable waiting for save process (even if it is waiting to start)
            TaskCompletionSource<bool> saveProcess = new();
            LatestSaveProcess = saveProcess;

            // save
            return await SaveInternalAsync(true, [page], saveProcess, uiDispatcherQueue);
        }

        private async Task<bool> SaveInternalAsync(bool saveAs, List<IProjectPage> pages, TaskCompletionSource<bool> saveProcess, DispatcherQueue uiDispatcherQueue)
        {
            bool success = false;
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

                    // update target location for every file if needed
                    bool forceSaving = false;
                    if (saveAs || pages.Any(x => x is ImagePage imagePage && imagePage.TargetFile == null && imagePage.TargetFolder == null))
                    {
                        // get desired name
                        string? desiredFileDisplayName = null;
                        if (pages[0] is ImagePage imagePage)
                            desiredFileDisplayName = imagePage.FileNameInfo?.DesiredDisplayName;

                        // get save options
                        SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, InitialScanOptions!, this, true, uiDispatcherQueue, saveAs, desiredFileDisplayName);
                        if (saveOptions == null || saveOptions.TargetFolder == null)
                            return;

                        // get target folder
                        StorageFolder targetFolder = saveOptions.TargetFolder;
                        if (saveOptions.SubFolderName != null)
                            targetFolder = await targetFolder.CreateFolderAsync(saveOptions.SubFolderName, CreationCollisionOption.OpenIfExists);

                        foreach (IProjectPage page in pages)
                        {
                            imagePage = (ImagePage)page;

                            if (saveAs)
                                imagePage.TargetFile = null;

                            if (page is ImagePage imagePageToUpdate)
                            {
                                imagePageToUpdate.TargetFolder = targetFolder;
                                await imagePageToUpdate.FileNameInfo!.UpdateNamesAsync(saveOptions.FileName, saveOptions.FileName, false, uiDispatcherQueue);
                            }
                        }

                        forceSaving = true;
                    }

                    // apply actual file changes
                    if (!areFilesSaved || forceSaving)
                    {
                        // lock data
                        await projectObjectSemaphore.WaitAsync();
                        await projectFolderSemaphore.WaitAsync();
                        await changesFolderSemaphore.WaitAsync();

                        // commit changes
                        foreach (IProjectPage page in pages)
                        {
                            ImagePage imagePage = (ImagePage)page;
                            if (imagePage.CommitNeeded)
                            {
                                // copy file to project folder
                                StorageFile? fileToDelete = imagePage.SourceFile;
                                StorageFile newSourceFile;
                                if (imagePage.OutOfDateSourceFile != null)
                                {
                                    newSourceFile = await imagePage.SourceFile.CopyAsync(AppDataService.ProjectFolder, imagePage.OutOfDateSourceFile.Name, NameCollisionOption.ReplaceExisting);
                                    imagePage.ClearOutOfDateSourceFile();
                                }
                                else
                                {
                                    newSourceFile = await imagePage.SourceFile.CopyAsync(AppDataService.ProjectFolder);
                                }

                                // update page
                                await imagePage.ChangeSourceFileAsync(AppDataService.ProjectFolder, newSourceFile, uiDispatcherQueue);

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

                        // delete target files marked for deletion
                        foreach (IProjectPage page in PagesWithTargetFilesToDelete)
                        {
                            ImagePage imagePage = (ImagePage)page;
                            if (imagePage.TargetFile != null)
                            {
                                FileHandle targetFile = imagePage.TargetFile;
                                imagePage.TargetFile = null;
                                targetFile.FileStream.Dispose();
                                _ = Task.Run(async () => await targetFile.File.DeleteAsync(StorageDeleteOption.PermanentDelete));
                            }
                        }
                        PagesWithTargetFilesToDelete.Clear();

                        // take snapshot
                        MultiFileProjectSnapshot? snapshot = null;
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
                        {
                            snapshot = new MultiFileProjectSnapshot(this);
                        });
                        if (snapshot == null) throw new ApplicationException("Failed to save project (snapshot is null)");

                        // continue processing edits during save process
                        projectObjectSemaphore.Release();
                        changesFolderSemaphore.Release();

                        // save
                        Dictionary<IProjectPage, FileHandle?> pageSaves = await snapshot.TrySaveAsync(uiDispatcherQueue);

                        // process save result
                        if (pageSaves.Count == 0) throw new ApplicationException("Failed to save project (no files saved)");

                        // update target files
                        await projectObjectSemaphore.WaitAsync();
                        foreach (KeyValuePair<IProjectPage, FileHandle?> pageSave in pageSaves)
                        {
                            ((ImagePage)pageSave.Key).TargetFile = pageSave.Value;
                        }
                        projectObjectSemaphore.Release();

                        // update file names
                        foreach (KeyValuePair<IProjectPage, FileHandle?> pageSave in pageSaves)
                        {
                            if (pageSave.Key is ImagePage imagePage && imagePage.FileNameInfo != null)
                            {
                                await imagePage.FileNameInfo.UpdateNamesAsync(imagePage.FileNameInfo.DesiredName, imagePage.TargetFile!.File.Name, false, uiDispatcherQueue);
                            }
                        }

                        projectFolderSemaphore.Release();
                    }

                    // apply file name
                    await projectObjectSemaphore.WaitAsync();

                    await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                    {
                        foreach (IProjectPage page in pages)
                        {
                            if (page is ImagePage imagePage)
                            {
                                if (imagePage.FileNameInfo!.DesiredName != imagePage.FileNameInfo.ActualName)
                                {
                                    imagePage.TargetFile!.FileStream.Dispose();
                                    await imagePage.TargetFile.File.RenameAsync(imagePage.FileNameInfo.DesiredName, NameCollisionOption.GenerateUniqueName);
                                    imagePage.TargetFile = new(imagePage.TargetFile.File, await imagePage.TargetFile.File.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
                                    await imagePage.FileNameInfo.UpdateNamesAsync(imagePage.TargetFile.File.Name, imagePage.TargetFile.File.Name, false, uiDispatcherQueue);
                                    hasFileNameBeenApplied = true;
                                }
                            }
                        }
                    });

                    success = true;
                    projectObjectSemaphore.Release();
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
                        if (success && !saveProcessWaitingToStart && !hasMadeChangesDuringSaveProcess)
                            areFilesSaved = true;

                        hasMadeChangesDuringSaveProcess = false;
                        saveProcess.TrySetResult(success);
                    });
                    saveSemaphore.Release();
                }
            });
            return success;
        }

        public async Task CopyPagesAsync(List<ImagePage> pages)
        {
            // wait for save processes to end
            if (LatestSaveProcess != null && !LatestSaveProcess.Task.IsCompleted)
            {
                await Messenger.Send(new ShowSaveInProgressDialogMessage()).Response;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // copy files
            try
            {
                if (IsSaved)
                {
                    List<StorageFile> files = new();
                    foreach (ImagePage page in pages)
                    {
                        if (page is ImagePage imagePage)
                        {
                            files.Add(imagePage.TargetFile!.File);
                        }
                    }

                    // construct data package
                    DataPackage dataPackage = new DataPackage();
                    dataPackage.RequestedOperation = DataPackageOperation.Copy;
                    dataPackage.SetStorageItems(files, true);

                    Clipboard.SetContent(dataPackage);
                }
                else
                {
                    Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                    {
                        Title = "Project not saved",
                        Message = "The project needs to be saved to complete this action.",
                        Severity = InfoBarSeverity.Error
                    }));
                }
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
            finally
            {
                projectObjectSemaphore.Release();
                saveSemaphore.Release();
            }
        }

        public async Task<bool> TryOpenWithPageAsync(AppInfo? app, ImagePage page)
        {
            // wait for save processes to end
            if (LatestSaveProcess != null && !LatestSaveProcess.Task.IsCompleted)
            {
                await Messenger.Send(new ShowSaveInProgressDialogMessage()).Response;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            try
            {
                if (!IsSaved)
                {
                    Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                    {
                        Title = "Project not saved",
                        Message = "The project needs to be saved to complete this action.",
                        Severity = InfoBarSeverity.Error
                    }));
                    return false;
                }

                // get file
                if (page.TargetFile == null)
                    return false;

                // construct launcher options
                Windows.System.LauncherOptions options = new();
                if (app != null)
                {
                    options.TargetApplicationPackageFamilyName = app.PackageFamilyName;
                }
                else
                {
                    options.DisplayApplicationPicker = true;
                }

                // open with
                return await Windows.System.Launcher.LaunchFileAsync(page.TargetFile.File, options);
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
            finally
            {
                projectObjectSemaphore.Release();
                saveSemaphore.Release();
            }
        }

        public async Task SharePagesAsync(List<ImagePage> pages)
        {
            // wait for save processes to end
            if (LatestSaveProcess != null && !LatestSaveProcess.Task.IsCompleted)
            {
                await Messenger.Send(new ShowSaveInProgressDialogMessage()).Response;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // share files
            try
            {
                if (IsSaved)
                {
                    // collect files
                    List<StorageFile> files = [];
                    foreach (ImagePage page in pages)
                    {
                        if (page.TargetFile == null)
                            throw new ActionFailedAndRolledBackException("Failed to collect files for sharing");

                        files.Add(page.TargetFile.File);
                    }

                    // set share files
                    Messenger.Send(new SetShareFilesMessage(files));

                    // invoke share UI
                    Messenger.Send(new InvokeShareUIMessage());
                }
                else
                {
                    Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                    {
                        Title = "Project not saved",
                        Message = "The project needs to be saved to complete this action.",
                        Severity = InfoBarSeverity.Error
                    }));
                }
            }
            catch (Exception exc)
            {
                throw new ActionFailedAndRolledBackException(exc);
            }
            finally
            {
                projectObjectSemaphore.Release();
                saveSemaphore.Release();
            }
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
}
