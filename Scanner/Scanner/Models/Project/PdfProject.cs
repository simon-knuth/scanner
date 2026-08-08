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
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Scanner.Models;

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

    public FileHandle? SourceFile;
    public FileHandle? TargetFile;

    public FileNameInfo FileNameInfo { get; private set; }

    public override bool HasSaveLocation => TargetFile != null || TargetFolder != null;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private PdfProject(Guid? id, IList<IProjectPage> pages, string targetFileName, StorageFolder? targetFolder, ScanOptions creationScanOptions) : base(id, pages, TargetFormat.PDF, creationScanOptions)
    {
        // folder saved at project level for PDF and page level for all other formats
        TargetFolder = targetFolder;
        FileNameInfo = new FileNameInfo(targetFileName);
        FileNameInfo.NameChanged += FileNameInfo_NameChanged;

        HasBeenCreatedFromPdf = pages.Any(x => x is PdfPage);
    }

    public static async Task<ProjectBase> CreateAsync(PdfProjectCreationData creationData, bool keepSourceFiles, bool isAlreadySaved, DispatcherQueue uiDispatcherQueue)
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
        PdfProject project = new(creationData.Id, pages, creationData.TargetFileName, creationData.TargetFolder, creationData.CreationScanOptions);

        if (isAlreadySaved)
            project.MarkRevisionSaved(project.CaptureContentRevision());

        if (pages[0] is PdfPage)
        {
            project.SourceFile = project.TargetFile = new(creationData.Pages[0].File, await creationData.Pages[0].File.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
            await project.GeneratePagePreviewsAsync([.. pages.Cast<PdfPage>()], uiDispatcherQueue);
        }
        else
        {
            await project.GeneratePagePreviewsAsync([.. pages.Cast<ImagePage>()], uiDispatcherQueue);
        }

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
                FileHandle targetFile = TargetFile;
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

    public override async Task<bool> SaveAsync(bool saveAs, DispatcherQueue uiDispatcherQueue, bool isUserInitiated = false)
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
        bool success = await SaveInternalAsync(saveAs, saveProcess, uiDispatcherQueue);

        // analytics (user-initiated saves only)
        if (success && isUserInitiated)
            TrackSaveAnalytics(saveAs);

        return success;
    }

    private async Task<bool> SaveInternalAsync(bool saveAs, TaskCompletionSource<bool> saveProcess, DispatcherQueue uiDispatcherQueue)
    {
        bool success = false;
        await Task.Run(async () =>
        {
            await saveSemaphore.WaitAsync();

            // revision at snapshot time (-1 means no bytes were written this pass)
            long revisionAtSnapshot = -1;

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
                    SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, CreationScanOptions!, this, true, uiDispatcherQueue, saveAs, FileNameInfo.DesiredDisplayName);
                    if (saveOptions == null || saveOptions.TargetFolder == null)
                        return;

                    // get target folder
                    TargetFolder = saveOptions.TargetFolder;
                    if (saveOptions.SubfolderName != null)
                        TargetFolder = await TargetFolder.CreateFolderAsync(saveOptions.SubfolderName, CreationCollisionOption.OpenIfExists);

                    if (saveAs)
                    {
                        FileHandle? targetFile = TargetFile;
                        TargetFile = null;
                        targetFile?.FileStream.Dispose();
                    }

                    await FileNameInfo.UpdateNamesAsync(saveOptions.FileName, null, false, uiDispatcherQueue);

                    forceSaving = true;
                }

                // apply actual file changes
                if (HasUnsavedContent || forceSaving)
                {
                    // lock data
                    await projectObjectSemaphore.WaitAsync();
                    await projectFolderSemaphore.WaitAsync();
                    await changesFolderSemaphore.WaitAsync();

                    // commit changes
                    foreach (IProjectPage page in Pages)
                    {
                        if (page is ImagePage imagePage && imagePage.CommitNeeded)
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
                                newSourceFile = await imagePage.SourceFile.CopyAsync(AppDataService.ProjectFolder, imagePage.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
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

                    // capture the revision the snapshot represents before releasing object lock
                    revisionAtSnapshot = CaptureContentRevision();

                    // continue processing edits during save process
                    projectObjectSemaphore.Release();
                    changesFolderSemaphore.Release();

                    // save
                    Dictionary<IProjectPage, FileHandle?> pageSaves = await snapshot.TrySaveAsync(uiDispatcherQueue);

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

                    // only set IsSaved to true if no newer revision was created while saving
                    if (success && revisionAtSnapshot >= 0)
                        MarkRevisionSaved(revisionAtSnapshot);

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
                Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
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
                Messenger.Send(new ShowInAppNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
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
            await GenerateFileNameWithAIAsync(softwareBitmap, uiDispatcherQueue, false);
        }
        finally
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);
        }
    }

    /// <param name="isAutomatic">
    /// Whether the generation was triggered automatically (e.g. after a scan) rather than invoked manually by the user.
    /// </param>
    public async Task GenerateFileNameWithAIAsync(SoftwareBitmap bitmap, DispatcherQueue uiDispatcherQueue, bool isAutomatic)
    {
        // analytics
        SentryService?.TrackEvent(AnalyticsEvent.AIFileNameGenerationStarted, new Dictionary<string, string>
        {
            { "automatic", isAutomatic.ToString() }
        });
        Stopwatch stopwatch = Stopwatch.StartNew();

        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = true);

        bool successful = false;
        try
        {
            // generate name
            FileNameInfo.NameGenerationCts?.Cancel();
            FileNameInfo.NameGenerationCts = new();
            string? fileName = await CopilotRuntimeService.TryGenerateFileNameForImageAsync(bitmap, FileNameInfo.NameGenerationCts);

            // apply result
            if (fileName != null)
            {
                string newName = fileName + Helpers.Helpers.TargetFormatToFileExtension(Format);
                if (newName != FileNameInfo.DesiredName)
                    await ProjectService.ApplyActionAsync(new RenameAction(null, newName, true));

                successful = true;
            }
        }
        finally
        {
            bitmap.Dispose();
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () => FileNameInfo.IsNameGenerationInProgress = false);

            // analytics
            stopwatch.Stop();
            SentryService?.TrackEvent(AnalyticsEvent.AIFileNameGenerationStopped, new Dictionary<string, string>
            {
                { "successful", successful.ToString() }
            });
            SentryService?.TrackDistributionMetric(AnalyticsMetric.AIFileNameGenerationDuration, stopwatch.Elapsed.TotalMilliseconds,
                MeasurementUnit.Duration.Millisecond, new Dictionary<string, string>
                {
                    { "successful", successful.ToString() }
                });
        }
    }

    private void FileNameInfo_NameChanged(object? sender, EventArgs e)
    {
        hasFileNameBeenApplied = FileNameInfo!.DesiredName == FileNameInfo.ActualName;
    }

    public static async Task<Dictionary<IProjectPage, FileHandle?>> CreatePdfFromPagesAsync(Dictionary<IProjectPage, IProjectSnapshotPage> pages, FileHandle? targetFile, string? desiredFileName, StorageFolder? targetFolder, bool ocr, DispatcherQueue uiDispatcherQueue)
    {
        Dictionary<IProjectPage, FileHandle?> result = new();
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
            LogService?.Log.Error(exc, "Failed to generate PDF");
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
            LogService?.Log.Error(exc, "Failed to save PDF to target folder");
        }

        return result;
    }

    private static async Task CreatePdfFromSnapshotPagesAsync(string targetPdfPath, List<IProjectSnapshotPage> snapshotPages, DispatcherQueue uiDispatcherQueue, string? ocrPdfPath = null)
    {
        using PdfDocument document = new();

        // load source PDF
        XPdfForm? sourcePdf = null;

        // load OCR PDF
        XPdfForm? ocrPdf = null;
        int ocrIndex = 0;
        if (ocrPdfPath != null)
            ocrPdf = XPdfForm.FromFile(ocrPdfPath);

        for (int i = 0; i < snapshotPages.Count; i++)
        {
            IProjectSnapshotPage snapshotPage = snapshotPages[i];
            PdfSharp.Pdf.PdfPage newPdfPage = document.AddPage();

            if (snapshotPage is not PdfProjectSnapshotPage pdfSnapshotPage || pdfSnapshotPage.IndexInSourceFile == null)
            {
                // page from image (OCR or not)
                XImage image;
                bool hasDestructiveEffects = snapshotPage.Filter != ImageFilter.None || snapshotPage.Brightness != 0 || snapshotPage.Contrast != 0;
                if (hasDestructiveEffects)
                {
                    // bake the filter/brightness/contrast into the image (encoded as JPG to reduce PDF size)
                    using InMemoryRandomAccessStream targetStream = new();
                    await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                    {
                        using IRandomAccessStream sourceStream = await snapshotPage.SourceFile.OpenAsync(FileAccessMode.Read);

                        BitmapPropertySet propertySet = new BitmapPropertySet();
                        propertySet.Add("ImageQuality", new BitmapTypedValue(jpegQuality, Windows.Foundation.PropertyType.Single));
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, targetStream, propertySet);

                        await ApplyEffectsAsync(sourceStream, encoder, snapshotPage.Filter, snapshotPage.Brightness, snapshotPage.Contrast);
                    });
                    image = XImage.FromStream(targetStream.AsStream());
                }
                else if (Helpers.Helpers.FileExtensionToTargetFormat(snapshotPage.SourceFile.FileType) != TargetFormat.JPG)
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
                    ocrPdf.PageIndex = ocrIndex;
                    ocrIndex++;
                    gfx.DrawImage(ocrPdf, 0, 0);
                }

                // add image layer
                gfx.DrawImage(image, 0, 0);
                image.Dispose();
            }
            else
            {
                // page from PDF
                using (IRandomAccessStream sourceStream = await snapshotPage.SourceFile.OpenAsync(FileAccessMode.Read))
                sourcePdf ??= XPdfForm.FromStream(sourceStream.AsStream());

                // append PDF page
                sourcePdf.PageIndex = (int)pdfSnapshotPage.IndexInSourceFile;
                newPdfPage.Width = XUnit.FromPoint(sourcePdf.PointWidth);
                newPdfPage.Height = XUnit.FromPoint(sourcePdf.PointHeight);
                using XGraphics gfx = XGraphics.FromPdfPage(newPdfPage);
                gfx.DrawImage(sourcePdf, 0, 0);
            }
        }

        ocrPdf?.Dispose();
        await document.SaveAsync(targetPdfPath);
    }
}
