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
    public partial class PdfProject : ProjectBase
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public StorageFolder TargetFolder;

        public StorageFile? TargetFile;

        public FileNameInfo FileNameInfo { get; private set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private PdfProject(IList<IProjectPage> pages, TargetFormat format, string targetFileName, StorageFolder targetFolder) : base(pages, format)
        {
            // folder saved at project level for PDF and page level for all other formats
            TargetFolder = targetFolder;
            FileNameInfo = new FileNameInfo(targetFileName);
            FileNameInfo.NameChanged += FileNameInfo_NameChanged;
            hasFileNameBeenApplied = false;
        }

        public static async Task<ProjectBase> CreateAsync(IList<StorageFile> files, TargetFormat format, string targetFileName, StorageFolder targetFolder, bool keepSourceFiles)
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
            return new PdfProject(pages, format, targetFileName, targetFolder);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public override async Task SaveAsync(DispatcherQueue uiDispatcherQueue)
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
                        await FileNameInfo!.UpdateNamesAsync(FileNameInfo!.DesiredName, pageSaves.Values.First()!.Name, uiDispatcherQueue);

                        projectFolderSemaphore.Release();
                    }

                    // apply file name
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

        private void FileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
        }
    }
}
