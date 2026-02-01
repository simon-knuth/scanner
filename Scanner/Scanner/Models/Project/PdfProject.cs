using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models.Interfaces;
using Scanner.Models.Project;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

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
        #region Constants
        private const string pdfOutputFileDisplayName = "tessoutput";
        private const double jpegQuality = 0.85;
        #endregion

        public StorageFolder? TargetFolder;

        public TargetFile? TargetFile;

        public FileNameInfo FileNameInfo { get; private set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private PdfProject(IList<IProjectPage> pages, string targetFileName, StorageFolder? targetFolder, ScanOptions initialScanOptions) : base(pages, TargetFormat.PDF, initialScanOptions)
        {
            // folder saved at project level for PDF and page level for all other formats
            TargetFolder = targetFolder;
            FileNameInfo = new FileNameInfo(targetFileName);
            FileNameInfo.NameChanged += FileNameInfo_NameChanged;
            hasFileNameBeenApplied = false;
        }

        public static async Task<ProjectBase> CreateAsync(PdfProjectCreationData creationData, bool keepSourceFiles, DispatcherQueue uiDispatcherQueue)
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
                pages.Add(await CreatePageFromFileAsync(pageData.File, i, null, null, pageData.TargetFolder, keepSourceFiles, AppDataService.ProjectFolder, pageData.BaseFilter, pageData.Filter, pageData.Brightness, pageData.Contrast));
            }

            // create project and update previews
            PdfProject project = new PdfProject(pages, creationData.TargetFileName, creationData.TargetFolder, creationData.InitialScanOptions);
            
            if (Helpers.Helpers.FileExtensionToTargetFormat(pages[0].SourceFile.FileType) == TargetFormat.PDF)
                await project.GeneratePagePreviewsAsync([.. pages.Cast<PdfPage>()], uiDispatcherQueue);
            else
                await project.GeneratePagePreviewsAsync([.. pages.Cast<ImagePage>()], uiDispatcherQueue);

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
                    TargetFile targetFile = TargetFile;
                    TargetFile = null;
                    targetFile.FileStream.Dispose();
                    await targetFile.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
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
                        SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, InitialScanOptions!, this, true, uiDispatcherQueue, saveAs, FileNameInfo.DesiredDisplayName);
                        if (saveOptions == null || saveOptions.TargetFolder == null)
                            return;

                        // get target folder
                        TargetFolder = saveOptions.TargetFolder;
                        if (saveOptions.SubFolderName != null)
                            TargetFolder = await TargetFolder.CreateFolderAsync(saveOptions.SubFolderName, CreationCollisionOption.OpenIfExists);

                        if (saveAs)
                        {
                            TargetFile? targetFile = TargetFile;
                            TargetFile = null;
                            targetFile?.FileStream.Dispose();
                        }

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
                            ImagePage imagePage = (ImagePage)page;
                            if (imagePage.CommitNeeded)
                            {
                                // copy file to project folder
                                StorageFile? fileToDelete = page.SourceFile;
                                StorageFile newSourceFile;
                                if (imagePage.OutOfDateSourceFile != null)
                                {
                                    newSourceFile = await page.SourceFile.CopyAsync(AppDataService.ProjectFolder, imagePage.OutOfDateSourceFile.Name, NameCollisionOption.ReplaceExisting);
                                    imagePage.ClearOutOfDateSourceFile();
                                }
                                else
                                {
                                    newSourceFile = await page.SourceFile.CopyAsync(AppDataService.ProjectFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
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
                        Dictionary<IProjectPage, TargetFile?> pageSaves = await snapshot.TrySaveAsync(uiDispatcherQueue);

                        // process save result
                        if (pageSaves.Count == 0) throw new ApplicationException("Failed to save Project (no files saved)");

                        // update target file
                        await projectObjectSemaphore.WaitAsync();
                        TargetFile = pageSaves.Values.First();
                        projectObjectSemaphore.Release();

                        // update file name
                        await FileNameInfo!.UpdateNamesAsync(FileNameInfo!.DesiredName, TargetFile.File.Name, false, uiDispatcherQueue);

                        projectFolderSemaphore.Release();
                    }

                    // apply file name
                    await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                    {
                        if (TargetFile.File.Name != FileNameInfo!.DesiredName)
                        {
                            TargetFile.FileStream.Dispose();
                            await TargetFile.File.RenameAsync(FileNameInfo!.DesiredName, NameCollisionOption.GenerateUniqueName);
                            TargetFile = new(TargetFile.File, await TargetFile.File.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
                            await FileNameInfo!.UpdateNamesAsync(TargetFile.File.Name, TargetFile.File.Name, false, uiDispatcherQueue);
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
                    dataPackage.SetStorageItems([TargetFile.File], true);
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

        public async Task<bool> TryOpenWithAsync(AppInfo? app)
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
                    return false;
                }

                // get file
                if (TargetFile == null)
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
                return await Windows.System.Launcher.LaunchFileAsync(TargetFile.File, options);
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
                Messenger.Send(new SetShareFilesMessage([TargetFile.File]));

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

        public async Task<SoftwareBitmap> GetSoftwareBitmapForAIFileNameGenerationAsync(DispatcherQueue uiDispatcherQueue)
        {
            // decode bitmap
            using IRandomAccessStream sourceFileStream = await Pages[0].PreviewFile.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceFileStream);

            // scale down
            BitmapTransform transform = new BitmapTransform()
            {
                ScaledWidth = decoder.PixelWidth / 2,
                ScaledHeight = decoder.PixelHeight / 2
            };
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return softwareBitmap;
        }

        public async Task GenerateFileNameWithAIAsync(DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = true);

            try
            {
                SoftwareBitmap softwareBitmap = await GetSoftwareBitmapForAIFileNameGenerationAsync(uiDispatcherQueue);
                await GenerateFileNameWithAIAsync(softwareBitmap, uiDispatcherQueue);
            }
            finally
            {
                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);
            }
        }

        public async Task GenerateFileNameWithAIAsync(SoftwareBitmap bitmap, DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = true);

            try
            {
                // generate name
                FileNameInfo.NameGenerationCts?.Cancel();
                FileNameInfo.NameGenerationCts = new();
                string? fileName = await CopilotRuntimeService.TryGenerateFileNameForImageAsync(bitmap, FileNameInfo.NameGenerationCts);

                // apply result
                if (fileName != null)
                    await FileNameInfo.UpdateNamesAsync(fileName + Helpers.Helpers.TargetFormatToFileExtension(Format), FileNameInfo.ActualName, true, uiDispatcherQueue);
            }
            finally
            {
                bitmap.Dispose();
                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);
            }
        }

        private void FileNameInfo_NameChanged(object? sender, EventArgs e)
        {
            hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
        }

        public static async Task<Dictionary<IProjectPage, TargetFile?>> CreatePdfFromPagesAsync(Dictionary<IProjectPage, IProjectSnapshotPage> pages, TargetFile? targetFile, string? desiredFileName, StorageFolder? targetFolder, bool ocr, DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, TargetFile?> result = new();
            string pdfGenerationFilePath = Path.Combine(AppDataService.PdfOutputFolder.Path, pdfOutputFileDisplayName);

            // generate PDF
            try
            {
                if (ocr)
                {
                    await OcrService.GenerateOcrPdfAsync([.. pages.Values], pdfGenerationFilePath, uiDispatcherQueue);
                    await CreatePdfFromSnapshotPagesAsync(pdfGenerationFilePath + ".pdf", [.. pages.Values], uiDispatcherQueue, pdfGenerationFilePath + ".pdf");
                }
                else
                {
                    await CreatePdfFromSnapshotPagesAsync(pdfGenerationFilePath + ".pdf", [.. pages.Values], uiDispatcherQueue);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to generate PDF");
                return result;
            }

            // save PDF to target folder
            try
            {
                StorageFile generatedFile = await AppDataService.PdfOutputFolder.GetFileAsync($"{pdfOutputFileDisplayName}.pdf");
                if (targetFile != null)
                {
                    targetFile.FileStream.Dispose();
                    await generatedFile.MoveAndReplaceAsync(targetFile.File);

                    if (generatedFile.Name != desiredFileName)
                    {
                        await generatedFile.RenameAsync(desiredFileName, NameCollisionOption.GenerateUniqueName);
                    }
                }
                else
                {
                    await generatedFile.MoveAsync(targetFolder, desiredFileName, NameCollisionOption.GenerateUniqueName);
                }
                result.Add(pages.Keys.First(), new(generatedFile, await generatedFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders)));
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to save PDF to target folder");
            }

            return result;
        }

        private static async Task CreatePdfFromSnapshotPagesAsync(string targetPdfPath, List<IProjectSnapshotPage> snapshotPages, DispatcherQueue uiDispatcherQueue, string? ocrPdfPath = null)
        {
            using PdfDocument document = new();

            XPdfForm? ocrPdf = null;
            if (ocrPdfPath != null)
                ocrPdf = XPdfForm.FromFile(ocrPdfPath);

            for (int i = 0; i < snapshotPages.Count; i++)
            {
                IProjectSnapshotPage snapshotPage = snapshotPages[i];
                PdfSharp.Pdf.PdfPage newPdfPage = document.AddPage();

                // get image
                XImage image;
                if (Helpers.Helpers.FileExtensionToTargetFormat(snapshotPage.SourceFile.FileType) != TargetFormat.JPG)
                {
                    // convert image to JPG to reduce PDF size
                    using InMemoryRandomAccessStream targetStream = new();
                    await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                    {
                        using IRandomAccessStream sourceStream = await snapshotPage.SourceFile.OpenAsync(FileAccessMode.Read);
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);

                        BitmapPropertySet propertySet = new BitmapPropertySet();
                        propertySet.Add("ImageQuality", new BitmapTypedValue(jpegQuality, Windows.Foundation.PropertyType.Single));
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, targetStream, propertySet);

                        using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        await encoder.FlushAsync();
                    });
                    image = XImage.FromStream(targetStream.AsStream());
                }
                else
                {
                    // use original image
                    image = XImage.FromFile(snapshotPage.SourceFile.Path);
                }

                // setup page
                newPdfPage.Width = XUnit.FromInch(image.PixelWidth / image.HorizontalResolution);
                newPdfPage.Height = XUnit.FromInch(image.PixelHeight / image.VerticalResolution);
                using XGraphics gfx = XGraphics.FromPdfPage(newPdfPage);

                // add OCR layer
                if (ocrPdf != null)
                {
                    ocrPdf.PageIndex = i;
                    gfx.DrawImage(ocrPdf, 0, 0);
                }

                // add image layer
                gfx.DrawImage(image, 0, 0);
                image.Dispose();
            }

            ocrPdf?.Dispose();
            await document.SaveAsync(targetPdfPath);
        }
    }
}
