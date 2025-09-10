using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models.Interfaces;
using Scanner.Services;
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
    /// A project that produces a PDF file.
    /// </summary>
    public partial class PdfProject : ProjectBase
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public StorageFolder? TargetFolder;

        public StorageFile? TargetFile;

        public FileNameInfo FileNameInfo { get; private set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private PdfProject(IList<IProjectPage> pages, TargetFormat format, string targetFileName, StorageFolder? targetFolder, ScanOptions initialScanOptions) : base(pages, format, initialScanOptions)
        {
            // folder saved at project level for PDF and page level for all other formats
            TargetFolder = targetFolder;
            FileNameInfo = new FileNameInfo(targetFileName);
            FileNameInfo.NameChanged += FileNameInfo_NameChanged;
            hasFileNameBeenApplied = false;
        }

        public static async Task<ProjectBase> CreateAsync(IList<StorageFile> files, TargetFormat format, string targetFileName, StorageFolder? targetFolder, bool keepSourceFiles, ImageFilter baseFilter, ImageFilter filter, ScanOptions initialScanOptions)
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
                pages.Add(await CreatePageFromFileAsync(files[i], i, null, targetFolder, keepSourceFiles, AppDataService.ProjectFolder, baseFilter, filter));
            }

            // create project and update previews
            PdfProject project = new PdfProject(pages, format, targetFileName, targetFolder, initialScanOptions);
            await project.UpdatePagePreviewsAsync(pages.OfType<ImagePage>().ToList());
            return project;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public override async Task DeleteAsync()
        {
            // wait for save processes to end
            if (LatestSaveProcess != null)
            {
                await LatestSaveProcess.Task;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // delete file
            try
            {
                if (TargetFile != null)
                {
                    await TargetFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
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

        public override async Task<bool> SaveAsync(bool saveAs, DispatcherQueue uiDispatcherQueue)
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
            return await SaveInternalAsync(saveAs, saveProcess, uiDispatcherQueue);
        }

        private async Task<bool> SaveInternalAsync(bool saveAs, TaskCompletionSource<bool> saveProcess, DispatcherQueue uiDispatcherQueue)
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

                    // update target location if needed
                    bool forceSaving = false;
                    if (saveAs || (TargetFile == null && TargetFolder == null))
                    {
                        // get save options
                        SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(uiDispatcherQueue, ((App)Application.Current).MainWindow, InitialScanOptions!, this, true, saveAs, FileNameInfo.DesiredDisplayName);
                        if (saveOptions == null || saveOptions.TargetFolder == null)
                            return;

                        if (saveAs)
                            TargetFile = null;

                        TargetFolder = saveOptions.TargetFolder;
                        await FileNameInfo.UpdateNamesAsync(saveOptions.FileName, null, false, uiDispatcherQueue);

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
                        PdfProjectSnapshot? snapshot = null;
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
                        {
                            snapshot = new PdfProjectSnapshot(this);
                        });
                        if (snapshot == null) throw new ApplicationException("Failed to save Project (snapshot is null)");

                        // continue processing edits during save process
                        projectObjectSemaphore.Release();
                        changesFolderSemaphore.Release();

                        // save
                        Dictionary<IProjectPage, StorageFile?> pageSaves = await snapshot.TrySaveAsync(uiDispatcherQueue);

                        // process save result
                        if (pageSaves.Count == 0) throw new ApplicationException("Failed to save Project (no files saved)");

                        // update target file
                        await projectObjectSemaphore.WaitAsync();
                        TargetFile = pageSaves.Values.First();
                        projectObjectSemaphore.Release();

                        // update file name
                        await FileNameInfo!.UpdateNamesAsync(FileNameInfo!.DesiredName, pageSaves.Values.First()!.Name, false, uiDispatcherQueue);

                        projectFolderSemaphore.Release();
                    }

                    // apply file name
                    await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                    {
                        if (TargetFile!.Name != FileNameInfo!.DesiredName)
                        {
                            await TargetFile.RenameAsync(FileNameInfo!.DesiredName, NameCollisionOption.GenerateUniqueName);
                            await FileNameInfo!.UpdateNamesAsync(TargetFile.Name, TargetFile.Name, false, uiDispatcherQueue);
                            hasFileNameBeenApplied = true;
                        }
                    });

                    success = true;
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

        public async Task CopyAsync()
        {
            // wait for save processes to end
            if (LatestSaveProcess != null && !LatestSaveProcess.Task.IsCompleted)
            {
                await Messenger.Send(new ShowSaveInProgressDialogMessage()).Response;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // copy file
            try
            {
                if (IsSaved && TargetFile != null)
                {
                    DataPackage dataPackage = new DataPackage();
                    dataPackage.RequestedOperation = DataPackageOperation.Copy;
                    dataPackage.SetStorageItems([TargetFile], true);
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

        public async Task TryOpenWithAsync(AppInfo? app)
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
                if (TargetFile == null) return;

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
                await Windows.System.Launcher.LaunchFileAsync(TargetFile, options);
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

        public async Task ShareAsync()
        {
            // wait for save processes to end
            if (LatestSaveProcess != null && !LatestSaveProcess.Task.IsCompleted)
            {
                await Messenger.Send(new ShowSaveInProgressDialogMessage()).Response;
            }
            await saveSemaphore.WaitAsync();
            await projectObjectSemaphore.WaitAsync();

            // share file
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

                // set file for sharing
                Messenger.Send(new SetShareFilesMessage([TargetFile]));

                // invoke share UI
                Messenger.Send(new InvokeShareUIMessage());
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

        public async Task<ImageBuffer> GetImageBufferForAIFileNameGenerationAsync(DispatcherQueue uiDispatcherQueue)
        {
            // decode bitmap
            using IRandomAccessStream sourceFileStream = await Pages[0].PreviewFile.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceFileStream);

            // scale down
            BitmapTransform transform = new BitmapTransform()
            {
                ScaledWidth = decoder.PixelWidth / 4,
                ScaledHeight = decoder.PixelHeight / 4
            };
            using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            // generate ImageBuffer for AI
            using ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

            return imageBuffer;
        }

        public async Task GenerateFileNameWithAIAsync(DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = true);

            try
            {
                using ImageBuffer imageBuffer = await GetImageBufferForAIFileNameGenerationAsync(uiDispatcherQueue);
                await GenerateFileNameWithAIAsync(imageBuffer, uiDispatcherQueue);
            }
            finally
            {
                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);
            }
        }

        public async Task GenerateFileNameWithAIAsync(ImageBuffer imageBuffer, DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = true);

            try
            {
                // generate name
                FileNameInfo.NameGenerationCts?.Cancel();
                FileNameInfo.NameGenerationCts = new();
                string? fileName = await CopilotRuntimeService.TryGenerateFileNameForImageAsync(imageBuffer, FileNameInfo.NameGenerationCts);

                // apply result
                if (fileName != null)
                    await FileNameInfo.UpdateNamesAsync(fileName, FileNameInfo.ActualName, true, uiDispatcherQueue);
            }
            finally
            {
                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);
            }
        }

        private void FileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
        }
    }
}
