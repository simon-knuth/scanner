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
    /// A project that produces one or more image file(s).
    /// </summary>
    public partial class ImageProject : ProjectBase
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImageProject(IList<IProjectPage> pages, TargetFormat format, ScanOptions initialScanOptions) : base(pages, format, initialScanOptions)
        {
            foreach (IProjectPage page in pages)
            {
                if (page is ImagePage imagePage && imagePage.FileNameInfo != null)
                {
                    imagePage.FileNameInfo.NameChanged += PageFileNameInfo_NameChanged;
                }
            }
        }

        public static async Task<ProjectBase> CreateAsync(IList<StorageFile> files, TargetFormat format, string? targetFileName, StorageFolder? targetFolder, bool keepSourceFiles, ImageFilter baseFilter, ImageFilter filter, ScanOptions initialScanOptions)
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
                pages.Add(await CreatePageFromFileAsync(files[i], i, targetFileName, targetFolder, keepSourceFiles, AppDataService.ProjectFolder, baseFilter, filter));
            }

            // create project and update previews
            ImageProject project = new ImageProject(pages, format, initialScanOptions);
            await project.UpdatePagePreviewsAsync(pages.OfType<ImagePage>().ToList());
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
                    if (page.TargetFile != null)
                    {
                        await page.TargetFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
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
                        SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(uiDispatcherQueue, ((App)Application.Current).MainWindow, InitialScanOptions!, this, true, saveAs, desiredFileDisplayName);
                        if (saveOptions == null || saveOptions.TargetFolder == null)
                            return;

                        foreach (IProjectPage page in pages)
                        {
                            if (saveAs)
                                page.TargetFile = null;

                            if (page is ImagePage imagePageToUpdate)
                            {
                                imagePageToUpdate.TargetFolder = saveOptions.TargetFolder;
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
                                await page.ChangeSourceFileAsync(AppDataService.ProjectFolder, newSourceFile, uiDispatcherQueue);

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
                        ImageProjectSnapshot? snapshot = null;
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
                        {
                            snapshot = new ImageProjectSnapshot(this);
                        });
                        if (snapshot == null) throw new ApplicationException("Failed to save Project (snapshot is null)");

                        // continue processing edits during save process
                        projectObjectSemaphore.Release();
                        changesFolderSemaphore.Release();

                        // save
                        Dictionary<IProjectPage, StorageFile?> pageSaves = await snapshot.TrySaveAsync(uiDispatcherQueue);

                        // process save result
                        if (pageSaves.Count == 0) throw new ApplicationException("Failed to save Project (no files saved)");

                        // update target files
                        await projectObjectSemaphore.WaitAsync();
                        foreach (KeyValuePair<IProjectPage, StorageFile?> pageSave in pageSaves)
                        {
                            pageSave.Key.TargetFile = pageSave.Value;
                        }
                        projectObjectSemaphore.Release();

                        // update file names
                        foreach (KeyValuePair<IProjectPage, StorageFile?> pageSave in pageSaves)
                        {
                            if (pageSave.Key is ImagePage imagePage && imagePage.FileNameInfo != null)
                            {
                                await imagePage.FileNameInfo.UpdateNamesAsync(imagePage.FileNameInfo.DesiredName, imagePage.TargetFile!.Name, false, uiDispatcherQueue);
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
                                    await imagePage.TargetFile!.RenameAsync(imagePage.FileNameInfo.DesiredName, NameCollisionOption.GenerateUniqueName);
                                    await imagePage.FileNameInfo.UpdateNamesAsync(imagePage.TargetFile.Name, imagePage.TargetFile.Name, false, uiDispatcherQueue);
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

        public async Task CopyPagesAsync(List<IProjectPage> pages)
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
                    foreach (IProjectPage page in pages)
                    {
                        if (page is ImagePage imagePage)
                        {
                            files.Add(imagePage.TargetFile!);
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
                    Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
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

        public async Task TryOpenWithPageAsync(AppInfo? app, IProjectPage page)
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
                    Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                    {
                        Title = "Project not saved",
                        Message = "The project needs to be saved to complete this action.",
                        Severity = InfoBarSeverity.Error
                    }));
                    return;
                }

                // get file
                if (page.TargetFile == null) return;

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
                await Windows.System.Launcher.LaunchFileAsync(page.TargetFile, options);
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

        public async Task SharePagesAsync(List<IProjectPage> pages)
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
                    foreach (IProjectPage page in pages)
                    {
                        if (page.TargetFile == null)
                            throw new ActionFailedAndRolledBackException("Failed to collect files for sharing");

                        files.Add(page.TargetFile);
                    }

                    // set share files
                    Messenger.Send(new SetShareFilesMessage(files));

                    // invoke share UI
                    Messenger.Send(new InvokeShareUIMessage());
                }
                else
                {
                    Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
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
