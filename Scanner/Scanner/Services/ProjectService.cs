using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.Storage.Pickers;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Sentry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Shell;
using Windows.Win32;
using Windows.Win32.UI.Shell;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.Services;

internal partial class ProjectService : ObservableRecipient, IProjectService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
    private readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetRequiredService<ICopilotRuntimeService>();
    private readonly IKnownScannersService KnownScannersService = Ioc.Default.GetRequiredService<IKnownScannersService>();
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    private readonly IProjectHistoryService ProjectHistoryService = Ioc.Default.GetRequiredService<IProjectHistoryService>();
    private readonly ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Events
    public event EventHandler? ScanCompletedSuccessfully;
    #endregion

    private ProjectBase? currentProject;
    public ProjectBase? CurrentProject
    {
        get => currentProject;
        private set
        {
            if (currentProject != value)
            {
                if (currentProject != null)
                {
                    currentProject.PagesAdded -= CurrentProject_PagesAdded;
                    currentProject.PagesRemoved -= CurrentProject_PagesRemoved;
                    currentProject.PropertyChanged -= CurrentProject_PropertyChanged;
                }

                if (SetProperty(ref currentProject, value))
                {
                    OnPropertyChanged(nameof(CanSelectPreviousPage));
                    OnPropertyChanged(nameof(CanSelectNextPage));
                    OnPropertyChanged(nameof(CanSaveProject));
                    OnPropertyChanged(nameof(CanSaveAsProject));
                    OnPropertyChanged(nameof(TotalNumberOfPages));
                }

                if (value != null)
                {
                    value.PagesAdded += CurrentProject_PagesAdded;
                    value.PagesRemoved += CurrentProject_PagesRemoved;
                    value.PropertyChanged += CurrentProject_PropertyChanged;

                    UiDispatcherQueue?.RunOnThread(DispatcherQueuePriority.Low, () =>
                    {
                        ProjectHistoryService.AddOrUpdateEntryAsync(value);
                    });
                }
                else
                {
                    ResetAutoSaveTimer();
                }
            }
        }
    }

    public int TotalNumberOfPages => CurrentProject?.Pages?.Count ?? 0;

    private IProjectPage? selectedPage;
    public IProjectPage? SelectedPage
    {
        get => selectedPage;
        set
        {
            if (SetProperty(ref selectedPage, value))
            {
                OnPropertyChanged(nameof(CanSelectPreviousPage));
                OnPropertyChanged(nameof(CanSelectNextPage));
                OnPropertyChanged(nameof(SelectedPagesCount));
            }
        }
    }

    private ObservableCollection<IProjectPage>? selectedPages;
    public ObservableCollection<IProjectPage>? SelectedPages
    {
        get => selectedPages;
        set
        {
            if (SetProperty(ref selectedPages, value))
            {
                OnPropertyChanged(nameof(SelectedPagesCount));

                if (value != null)
                    value.CollectionChanged += SelectedPages_CollectionChanged;
            }
        }
    }

    public int SelectedPagesCount => SelectedPages?.Count ?? (SelectedPage != null ? 1 : 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
    [NotifyPropertyChangedFor(nameof(IsProcessRunningOrEditing))]
    [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
    [NotifyPropertyChangedFor(nameof(CanSaveProject))]
    [NotifyPropertyChangedFor(nameof(CanSaveAsProject))]
    private bool isActionRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
    [NotifyPropertyChangedFor(nameof(IsProcessRunningOrEditing))]
    [NotifyPropertyChangedFor(nameof(CanSelectPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanSelectNextPage))]
    [NotifyPropertyChangedFor(nameof(CanSaveProject))]
    [NotifyPropertyChangedFor(nameof(CanSaveAsProject))]
    private bool isEditing;

    public bool IsProcessRunning => IsScanProcessRunning || IsActionRunning;
    public bool IsProcessRunningOrEditing => IsScanProcessRunning || IsActionRunning || IsEditing;

    private bool isScanProcessRunning;
    public bool IsScanProcessRunning
    {
        get => isScanProcessRunning;
        private set
        {
            SetProperty(ref isScanProcessRunning, value);
            OnPropertyChanged(nameof(IsProcessRunning));
            OnPropertyChanged(nameof(IsProcessRunningOrEditing));
            OnPropertyChanged(nameof(CanSaveProject));
            OnPropertyChanged(nameof(CanSaveAsProject));
            UpdateTaskbarProgress();
        }
    }

    private ScanState currentScanState;
    public ScanState CurrentScanState
    {
        get => currentScanState;
        private set
        {
            SetProperty(ref currentScanState, value);
            OnPropertyChanged(nameof(FriendlyCurrentScanState));
        }
    }

    public string FriendlyCurrentScanState => GetFriendlyCurrentScanState();


    // TODO: Update properties if selected page is moved
    public bool CanSelectPreviousPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index > 0;
    public bool CanSelectNextPage => !IsProcessRunningOrEditing && CurrentProject != null && SelectedPage != null && SelectedPage.Index < CurrentProject.Pages.Count - 1;

    public Stack<IProjectAction> UndoStack { get; private set; } = new();
    public Stack<IProjectAction> RedoStack { get; private set; } = new();
    public bool CanUndo => UndoStack.Count > 0;
    public bool CanRedo => RedoStack.Count > 0;

    public bool CanSaveProject => CurrentProject != null && !IsProcessRunning && !CurrentProject.IsSaving && !CurrentProject.IsSaved;
    public bool CanSaveAsProject => CurrentProject != null && !IsProcessRunning && !CurrentProject.IsSaving;

    public DispatcherQueue? UiDispatcherQueue { get; set; }

    private ThreadPoolTimer? autoSaveTimer;

    private CancellationTokenSource? scanCancellationTokenSource;
    private IScanningDevice? activeScanningDevice;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ProjectService()
    {
        SettingsService.PropertyChanged += SettingsService_PropertyChanged;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<bool> TryCreateProjectAsync(IProjectCreationData creationData, bool keepSourceFiles, bool isAlreadySaved, DispatcherQueue uiDispatcherQueue)
    {
        try
        {
            // close project
            if (await TryCloseProjectAsync() == false)
                return false;

            // create project
            CurrentScanState = ScanState.Processing;
            ProjectBase? project = null;
            await Task.Run(async () =>
            {
                project = await creationData.CreateProjectAsync(keepSourceFiles, uiDispatcherQueue);
            });
            CurrentProject = project;

            // save if needed
            if (SettingsService.SettingAutoSave)
            {
                // save
                await Task.Run(async () =>
                {
                    await project.SaveAsync(false, UiDispatcherQueue!);
                    ResetAutoSaveTimer();
                });
            }

            // free up space
            _ = Task.Run(() => _ = AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder));
            return true;
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to create project from data");
            SentryService?.TrackError(exc);
            Messenger.Send(new ShowInAppNotificationMessage(new Notification()
            {
                Title = "Something went wrong",
                Message = string.IsNullOrEmpty(exc.Message) ? exc.Message : "Please try again later."
            }));
            return false;
        }
        finally
        {
            IsActionRunning = IsScanProcessRunning = false;
        }
    }

    public async Task<bool> TryCreateProjectFromScanAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue)
    {
        scanCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = scanCancellationTokenSource.Token;

        try
        {
            LogService?.Log.Information("Creating a new project from scan with {@ScanOptions}", scanOptions);
            CurrentScanState = ScanState.Scanning;

            // close project
            if (await TryCloseProjectAsync() == false)
                return false;

            // get save options
            IsActionRunning = true;
            SaveOptions? saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, scanOptions, CurrentProject, false, UiDispatcherQueue!, false, null);
            if (saveOptions == null)
                return false;

            // preheat AI models
            if (saveOptions.GenerateAIFileName)
                _ = Task.Run(CopilotRuntimeService.PreheatFileNameGenerationModelsAsync);

            // scan
            IReadOnlyList<StorageFile>? files = await ScanAsync(scanOptions, uiDispatcherQueue, cancellationToken);
            if (files == null)
                return false;

            // automatic rotation
            if (SettingsService.SettingAutoRotate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentScanState = ScanState.AutomaticRotation;

                await Task.Run(async () =>
                {
                    Dictionary<StorageFile, RotationIntent> instructions = new();
                    foreach (StorageFile file in files)
                    {
                        instructions.Add(file, RotationIntent.Automatic);
                    }

                    await ProjectBase.RotateFilesAsync(instructions, true, AppDataService.IncomingFolder);
                }, cancellationToken);
            }

            // create project
            cancellationToken.ThrowIfCancellationRequested();
            CurrentScanState = ScanState.Processing;
            ProjectBase? project = null;
            await Task.Run(async () =>
            {
                switch (scanOptions.TargetFormat)
                {
                    case TargetFormat.PDF:
                        PdfProjectCreationData pdfCreationData = new(null, files, saveOptions.FileName, saveOptions.TargetFolder, scanOptions, false);
                        project = await pdfCreationData.CreateProjectAsync(false, uiDispatcherQueue);
                        break;
                    case TargetFormat.JPG:
                    case TargetFormat.PNG:
                    case TargetFormat.BMP:
                    case TargetFormat.SinglePagePDF:
                    case TargetFormat.TIFF:
                    case TargetFormat.RAW:
                        List<(StorageFile SourceFile, string? TargetFileName, StorageFile? TargetFile)> fileData = [];
                        foreach (StorageFile file in files)
                        {
                            fileData.Add((file, saveOptions.FileName, null));
                        }

                        MultiFileProjectCreationData projectCreationData = new(null,fileData, scanOptions.TargetFormat, saveOptions.TargetFolder, scanOptions, false);
                        project = await projectCreationData.CreateProjectAsync(false, uiDispatcherQueue);
                        break;
                    default:
                        throw new ArgumentException($"Can't create project for format {scanOptions.TargetFormat}");
                }
            });
            CurrentProject = project;

            // kick off AI file name generation
            if (saveOptions.GenerateAIFileName)
            {
                if (CurrentProject is PdfProject pdfProject)
                {
                    // load bitmap
                    SoftwareBitmap softwareBitmap = await pdfProject.GetSoftwareBitmapForAIFileNameGenerationAsync(uiDispatcherQueue);

                    // generate name in the background
                    _ = Task.Run(async () => await pdfProject.GenerateFileNameWithAIAsync(softwareBitmap, uiDispatcherQueue, true));
                }
                else if (CurrentProject is MultiFileProject imageProject)
                {
                    throw new NotImplementedException();
                }
            }

            // save if needed
            if (SettingsService.SettingAutoSave)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // save
                if (scanOptions.TargetFormat == TargetFormat.PDF)
                {
                    CurrentScanState = ScanState.GeneratingPDF;
                }
                else
                {
                    CurrentScanState = ScanState.Saving;
                }
                await Task.Run(async () =>
                {
                    await CurrentProject.SaveAsync(false, UiDispatcherQueue!);
                    ResetAutoSaveTimer();
                }, cancellationToken);
            }

            // free up space
            _ = Task.Run(() => _ = AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder));

            ShowScanCompleteToastNotification();

            return true;
        }
        catch (OperationCanceledException)
        {
            LogService?.Log.Information("Scan cancelled");
            SentryService?.TrackEvent(AnalyticsEvent.ScanCanceled);

            // discard any project that was already created from the cancelled scan
            if (CurrentProject != null)
                await TryCloseProjectAsync(ignoreUnsavedChanges: true);

            return false;
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to create project from scan");
            SentryService?.TrackError(exc);
            Messenger.Send(new ShowInAppNotificationMessage(new Notification()
            {
                Title = "Something went wrong",
                Message = string.IsNullOrEmpty(exc.Message) ? exc.Message : "Please try again later."
            }));
            return false;
        }
        finally
        {
            IsActionRunning = IsScanProcessRunning = false;
            activeScanningDevice = null;
            scanCancellationTokenSource?.Dispose();
            scanCancellationTokenSource = null;
            _ = Task.Run(CopilotRuntimeService.StopPreheatingFileNameGenerationModelsAsync);
        }
    }

    public async Task<bool> TryScanToProjectAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue)
    {
        scanCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = scanCancellationTokenSource.Token;

        try
        {
            LogService?.Log.Information("Scanning to add to the current project with {@ScanOptions}", scanOptions);
            CurrentScanState = ScanState.Scanning;

            if (CurrentProject == null)
                return false;
            if (scanOptions.Scanner == null)
                return false;

            // get save options
            IsActionRunning = true;
            SaveOptions? saveOptions = null;
            if (scanOptions.TargetFormat != TargetFormat.PDF)
            {
                saveOptions = await SaveLocationService.GetSaveOptionsAsync(((App)Application.Current).MainWindow, scanOptions, CurrentProject, false, UiDispatcherQueue!, false, null);
                if (saveOptions == null)
                    return false;
            }

            // scan
            IReadOnlyList<StorageFile>? files = await ScanAsync(scanOptions, uiDispatcherQueue, cancellationToken);
            if (files == null)
                return false;

            // automatic rotation
            if (SettingsService.SettingAutoRotate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentScanState = ScanState.AutomaticRotation;

                await Task.Run(async () =>
                {
                    Dictionary<StorageFile, RotationIntent> instructions = new();
                    foreach (StorageFile file in files)
                    {
                        instructions.Add(file, RotationIntent.Automatic);
                    }

                    await ProjectBase.RotateFilesAsync(instructions, true, AppDataService.IncomingFolder);
                }, cancellationToken);
            }

            // add files
            cancellationToken.ThrowIfCancellationRequested();
            CurrentScanState = ScanState.Processing;
            List<ProjectFileInsertion> insertions = new();
            for (int i = 0; i < files.Count; i++)
            {
                if (scanOptions.ScanMergeConfig == null)
                {
                    insertions.Add(new ProjectFileInsertion(files[i], CurrentProject.Pages.Count + i, saveOptions?.FileName, saveOptions?.TargetFolder, scanOptions.GetBaseFilter(), scanOptions.GetFilter(), scanOptions.Brightness, scanOptions.Contrast));
                }
                else
                {
                    insertions.Add(new ProjectFileInsertion(files[i], GetNewIndexAccordingToMergeConfig(scanOptions.ScanMergeConfig, i, files.Count, true),
                        saveOptions?.FileName, saveOptions?.TargetFolder, scanOptions.GetBaseFilter(), scanOptions.GetFilter(), scanOptions.Brightness, scanOptions.Contrast));
                }
            }
            IProjectAction action = new AddFilesAction(insertions, false);

            await ApplyActionAsync(action);

            ShowScanCompleteToastNotification();

            return true;
        }
        catch (OperationCanceledException)
        {
            LogService?.Log.Information("Scan cancelled");
            SentryService?.TrackEvent(AnalyticsEvent.ScanCanceled);
            return false;
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to scan to project");
            SentryService?.TrackError(exc);
            Messenger.Send(new ShowInAppNotificationMessage(new Notification()
            {
                Title = "Something went wrong",
                Message = string.IsNullOrEmpty(exc.Message) ? exc.Message : "Please try again later."
            }));
            return false;
        }
        finally
        {
            IsActionRunning = IsScanProcessRunning = false;
            activeScanningDevice = null;
            scanCancellationTokenSource?.Dispose();
            scanCancellationTokenSource = null;
        }
    }

    public async Task<bool> TryOpenProjectFromFilesAsync(string[] filePaths, Guid? id, DispatcherQueue uiDispatcherQueue)
    {
        TaskCompletionSource openTcs = new();

        try
        {
            // ensure single file type
            string extension = Path.GetExtension(filePaths[0]).ToLowerInvariant();
            LogService?.Log.Information("Opening a project from {FileCount} {Extension} file(s)", filePaths.Length, extension);
            if (filePaths.Any(f => Path.GetExtension(f).ToLowerInvariant() != extension))
            {
                Messenger.Send(new ShowInAppNotificationMessage(new Notification
                {
                    Title = "Can not open multiple file types",
                    Message = "Please select files of the same type to open a project.",
                    Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                }));
                return false;
            }
            else if (filePaths.Length > 1 && filePaths.Any(f => Path.GetExtension(f).ToLowerInvariant() == ".pdf"))
            {
                Messenger.Send(new ShowInAppNotificationMessage(new Notification
                {
                    Title = "Can not open multiple PDF files",
                    Message = "Please select just one PDF file to open a project.",
                    Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                }));
                return false;
            }

            // close project
            if (await TryCloseProjectAsync() == false)
                return false;

            Messenger.Send(new ShowIndeterminateProgressDialogMessage("Opening project", openTcs.Task));

            // get files
            List<(StorageFile SourceFile, string? TargetFileName, StorageFile? TargetFile)> files = [];
            foreach (string filePath in filePaths)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                files.Add((file, Path.GetFileName(filePath), file));
            }

            // get target folder
            StorageFolder? targetFolder = await files[0].SourceFile.GetParentAsync();

            // create project data
            TargetFormat targetFormat = Helpers.Helpers.FileExtensionToTargetFormat(extension);
            IProjectCreationData projectCreationData;
            ScanOptions scanOptions = new(null)
            {
                TargetFormat = targetFormat
            };

            if (targetFormat == TargetFormat.PDF)
            {
                Windows.Data.Pdf.PdfDocument document = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(files[0].SourceFile);
                projectCreationData = new PdfProjectCreationData(id, files[0].SourceFile, document.PageCount, targetFolder, scanOptions, true);
            }
            else
            {
                projectCreationData = new MultiFileProjectCreationData(id, files, targetFormat, targetFolder, scanOptions, true);
            }

            return await TryCreateProjectAsync(projectCreationData, true, true, uiDispatcherQueue!);
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to open files");
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong",
                Message = "Failed to open files.",
                Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
            }));
            return false;
        }
        finally
        {
            openTcs.TrySetResult();
        }
    }

    private async Task<IReadOnlyList<StorageFile>?> ScanAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue, CancellationToken cancellationToken)
    {
        if (scanOptions.Scanner == null)
            return null;

        IsScanProcessRunning = true;
        activeScanningDevice = scanOptions.Scanner;

        if (SettingsService.SettingRememberScanOptions)
            await KnownScannersService.RecordScannerUsageAsync(scanOptions.Scanner, scanOptions);

        await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);
        IReadOnlyList<StorageFile> files = [];
        Stopwatch scanStopwatch = Stopwatch.StartNew();
        await Task.Run(async () => files = await scanOptions.Scanner.GetScanAsync(scanOptions, AppDataService.IncomingFolder, uiDispatcherQueue), cancellationToken);
        scanStopwatch.Stop();

        // a cancellation requested right as the scan finished should still abort before processing
        cancellationToken.ThrowIfCancellationRequested();

        if (files.Count == 0)
        {
            LogService?.Log.Warning("Scan produced no pages (source {SourceMode})", scanOptions.SourceMode);

            // this should only happen if the feeder is empty
            if (scanOptions.SourceMode == ScannerSource.Feeder)
            {
                Messenger.Send(new ShowInAppNotificationMessage(new Notification()
                {
                    Title = "Feeder empty",
                    Message = "There are no pages in the feeder. Please make sure you have inserted them correctly for the scanner to detect them."
                }));
            }
            else
            {
                Messenger.Send(new ShowInAppNotificationMessage(new Notification()
                {
                    Title = "Something went wrong",
                    Message = "Please try again later."
                }));
            }
            return null;
        }

        LogService?.Log.Information("Scan completed with {PageCount} page(s) in {DurationMs:F0}ms", files.Count, scanStopwatch.Elapsed.TotalMilliseconds);

        SettingsService.ScanNumber++;

        // analytics
        string mergeValue = "None";
        if (scanOptions.ScanMergeConfig != null)
        {
            mergeValue = string.Format("{0} | {1}",
                string.Join(",", scanOptions.ScanMergeConfig.InsertIndices),
                scanOptions.ScanMergeConfig.SurplusPagesIndex);
        }
        ScannerAutoCropMode autoCropMode = scanOptions.ScanArea is AutoCropArea autoCropArea
            ? autoCropArea.AutoCropMode
            : ScannerAutoCropMode.Disabled;
        string region = scanOptions.ScanArea switch
        {
            PaperSizeArea paperSizeArea => paperSizeArea.PaperSize.ToString(),  // fixed paper size, e.g. "DinA4"
            AutoCropArea autoCrop => autoCrop.AutoCropMode.ToString(),          // e.g. "SingleRegion"
            RectScanArea => "CustomRegion",                                     // user-selected region
            _ => "WholeArea",                                                   // whole bed / no area set
        };
        SentryService?.TrackEvent(AnalyticsEvent.ScanCompleted, new Dictionary<string, string>
        {
            { "source", scanOptions.SourceMode.ToString() },
            { "pages", files.Count.ToString() },
            { "asked_for_save_location", (SettingsService.SettingSaveLocationType != SettingSaveLocationType.FixedLocation).ToString() },
            { "target_format", scanOptions.TargetFormat.ToString() },
            { "color_mode", scanOptions.ColorMode.ToString() },
            { "resolution", scanOptions.Resolution != null ? ((int)scanOptions.Resolution.Resolution.DpiX).ToString() : "Unknown" },
            { "duplex", scanOptions.Duplex.ToString() },
            { "multi_page", scanOptions.ScanMultiplePages.ToString() },
            { "auto_crop_mode", autoCropMode.ToString() },
            { "brightness_adjusted", (scanOptions.Brightness != AppConfig.DefaultBrightness).ToString() },
            { "contrast_adjusted", (scanOptions.Contrast != AppConfig.DefaultContrast).ToString() },
            { "filter", scanOptions.GetFilter().ToString() },
            { "merge", mergeValue },
            { "region", region },
            { "file_naming_pattern", SettingsService.SettingFileNamingPattern.ToString() },
        });
        SentryService?.TrackDistributionMetric(AnalyticsMetric.ScanDuration, scanStopwatch.Elapsed.TotalMilliseconds,
            MeasurementUnit.Duration.Millisecond, new Dictionary<string, string>
            {
                { "source", scanOptions.SourceMode.ToString() },
                { "pages", files.Count.ToString() },
            });
        SentryService?.TrackGaugeMetric(AnalyticsMetric.ScanPageCount, files.Count, MeasurementUnit.None, new Dictionary<string, string>
        {
            { "source", scanOptions.SourceMode.ToString() },
        });

        ScanCompletedSuccessfully?.Invoke(this, EventArgs.Empty);
        return files;
    }

    public async Task<bool> TryDeleteProjectAsync()
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Deleting the current project ({Format})", CurrentProject.Format);

        try
        {
            // get confirmation and delete
            if (await Messenger.Send(new ShowProjectDeletionDialogMessage(CurrentProject)).Response == false)
                return false;

            // close project
            TargetFormat deletedFormat = CurrentProject.Format;
            Guid id = CurrentProject.Id;
            if (await TryCloseProjectAsync(ignoreUnsavedChanges: true))
            {
                await ProjectHistoryService.RemoveEntryAsync(id);
                SentryService?.TrackEvent(AnalyticsEvent.ProjectDeleted, new Dictionary<string, string>
                {
                    { "format", deletedFormat.ToString() }
                });
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the actions couldn't be completed"
            }));
        }
        catch (Exception)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the project needs to be closed"
            }));

            // close project
            await TryCloseProjectAsync(ignoreUnsavedChanges: true);
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TrySaveProjectAsync()
    {
        if (CurrentProject == null) return true;

        // handle unsaved changes
        if (!CurrentProject.IsSaved && await Messenger.Send(new ShowUnsavedChangesDialogMessage()).Response == false)
        {
            // changes couldn't be handled
            return false;
        }

        // changes were handled (saved or discarded)
        return true;
    }

    public async Task<bool> TryCopyProjectAsync()
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Copying the current project to the clipboard");

        try
        {
            // copy project
            if (CurrentProject is PdfProject pdfProject)
            {
                await pdfProject.CopyAsync();
                SentryService?.TrackEvent(AnalyticsEvent.CopyDocument);
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TryCopyPagesAsync(List<ImagePage> pages)
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Copying {PageCount} page(s) to the clipboard", pages.Count);

        try
        {
            // copy project
            if (CurrentProject is MultiFileProject imageProject)
            {
                await imageProject.CopyPagesAsync(pages);
                SentryService?.TrackEvent(pages.Count >= 2 ? AnalyticsEvent.CopyPages : AnalyticsEvent.CopyPage);
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TryOpenWithProjectAsync(AppInfo? app)
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Opening the current project with another app ({UsesDefaultApp})", app == null);

        try
        {
            // open with project
            if (CurrentProject is PdfProject pdfProject)
            {
                bool result = await pdfProject.TryOpenWithAsync(app);
                if (result)
                    SentryService?.TrackEvent(AnalyticsEvent.OpenWith,
                        app != null ? new Dictionary<string, string> { { "display_name", app.DisplayInfo.DisplayName } } : null);
                return result;
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
            return false;
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TryOpenWithPageAsync(AppInfo? app, ImagePage page)
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Opening a page with another app ({UsesDefaultApp})", app == null);

        try
        {
            // open with page
            if (CurrentProject is MultiFileProject imageProject)
            {
                bool result = await imageProject.TryOpenWithPageAsync(app, page);
                if (result)
                    SentryService?.TrackEvent(AnalyticsEvent.OpenWith,
                        app != null ? new Dictionary<string, string> { { "display_name", app.DisplayInfo.DisplayName } } : null);
                return result;
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
            return false;
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TryShareProjectAsync()
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Sharing the current project");

        try
        {
            // share project
            if (CurrentProject is PdfProject pdfProject)
            {
                await pdfProject.ShareAsync();
                SentryService?.TrackEvent(AnalyticsEvent.Share);
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TrySharePagesAsync(List<ImagePage> pages)
    {
        if (CurrentProject == null) return true;
        IsActionRunning = true;
        LogService?.Log.Information("Sharing {PageCount} page(s)", pages.Count);

        try
        {
            // share pages
            if (CurrentProject is MultiFileProject imageProject)
            {
                await imageProject.SharePagesAsync(pages);
                SentryService?.TrackEvent(AnalyticsEvent.Share);
            }
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the action couldn't be completed"
            }));
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    public async Task<bool> TryCloseProjectAsync(bool preserveSourceFilesInIncomingFolder = false, bool ignoreUnsavedChanges = false)
    {
        if (CurrentProject == null)
            return true;

        LogService?.Log.Information("Closing the current project ({Format})", CurrentProject.Format);

        // handle unsaved changes
        if (!ignoreUnsavedChanges && await TrySaveProjectAsync() == false)
        {
            return false;
        }

        // close project
        ObservableCollection<IProjectPage> pages = CurrentProject.Pages;
        ProjectBase project = CurrentProject;
        CurrentProject = null;
        SelectedPage = null;
        await AppDataService.EmptyFolderAsync(AppDataService.IncomingFolder);

        // release files
        if (project is PdfProject pdfProject && pdfProject.TargetFile != null)
        {
            pdfProject.TargetFile.FileStream.Dispose();

            if (pdfProject.SourceFile != null && pdfProject.SourceFile != pdfProject.TargetFile)
                pdfProject.SourceFile.FileStream.Dispose();
        }
        else
        {
            foreach (IProjectPage page in pages)
            {
                if (page is ImagePage imagePage && imagePage.TargetFile != null)
                    imagePage.TargetFile.FileStream.Dispose();
            }
        }

        if (preserveSourceFilesInIncomingFolder)
        {
            // move all source files to the Incoming folder before clearing
            List<Task> tasks = [];
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] is ImagePage imagePage)
                    tasks.Add(imagePage.SourceFile.MoveAsync(AppDataService.IncomingFolder, imagePage.SourceFile.Name, NameCollisionOption.GenerateUniqueName).AsTask());
            }
            await Task.WhenAll(tasks);
        }

        await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);
        await AppDataService.EmptyFolderAsync(AppDataService.UndoFolder);
        await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
        await AppDataService.EmptyFolderAsync(AppDataService.PreviewFolder);
        await AppDataService.EmptyFolderAsync(AppDataService.ChangesFolder);                

        // update undo/redo stacks
        UndoStack.Clear();
        RedoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        // end processes
        IsActionRunning = false;
        IsEditing = false;
        IsScanProcessRunning = false;

        return true;
    }

    public void SelectPreviousPage()
    {
        if (CurrentProject == null) return;
        if (SelectedPage == null) return;

        if (CanSelectPreviousPage)
        {
            SelectedPage = CurrentProject.Pages[SelectedPage.Index - 1];
        }
    }

    public void SelectNextPage()
    {
        if (CurrentProject == null) return;
        if (SelectedPage == null) return;

        if (CanSelectNextPage)
        {
            SelectedPage = CurrentProject.Pages[SelectedPage.Index + 1];
        }
    }

    public void MakeDefaultSelection()
    {
        if (CurrentProject == null) return;

        SelectedPage = CurrentProject.Pages[CurrentProject.Pages.Count - 1];
        SelectedPages = null;
    }

    public async Task ApplyActionAsync(IProjectAction action)
    {
        await InternalApplyActionAsync(action, false);
    }

    private async Task<bool> InternalApplyActionAsync(IProjectAction action, bool redoing)
    {
        if (CurrentProject == null)
            return false;

        try
        {
            bool changesMade = false;
            bool merged = false;

            if (action is IAtomicProjectAction atomicProjectAction)
            {
                // check if action can be merged with previous one
                UndoStack.TryPeek(out IProjectAction? undoAction);
                if (!redoing
                    && undoAction != null
                    && undoAction is IAtomicProjectAction previousAtomicProjectAction
                    && DateTime.Now < previousAtomicProjectAction.MostRecentExecution + AppConfig.ConsecutiveAtomicActionMergeTime
                    && previousAtomicProjectAction.Page == atomicProjectAction.Page
                    && previousAtomicProjectAction.IsActionCompatibleForMerge(atomicProjectAction))
                {
                    // merge with previous action
                    changesMade = previousAtomicProjectAction.MergeAndExecute(CurrentProject, atomicProjectAction, UiDispatcherQueue!);
                    merged = true;
                }
                else
                {
                    // use separate action
                    changesMade = atomicProjectAction.Execute(CurrentProject, UiDispatcherQueue!);
                }
            }
            else
            {
                IsActionRunning = true;
                changesMade = await action.ExecuteAsync(CurrentProject, UiDispatcherQueue!);
            }

            if (changesMade && !merged && CurrentProject != null)
            {
                LogService?.Log.Information("{Verb} action {Action}", redoing ? "Redid" : "Applied", action.GetType().Name);

                // update undo stack
                UndoStack.Push(action);
                OnPropertyChanged(nameof(CanUndo));

                // update redo
                if (!redoing)
                {
                    RedoStack.Clear();
                    await AppDataService.EmptyFolderAsync(AppDataService.RedoFolder);
                }
                OnPropertyChanged(nameof(CanRedo));

                // analytics (genuine user actions only, not redos)
                if (!redoing)
                {
                    (AnalyticsEvent Event, Dictionary<string, string>? Properties)? analytics = action.GetAnalyticsEvent();
                    if (analytics != null)
                        SentryService?.TrackEvent(analytics.Value.Event, analytics.Value.Properties);
                }
            }

            return true;
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the actions couldn't be completed"
            }));

            if (redoing)
            {
                // update redo stack
                RedoStack.Push(action);
                OnPropertyChanged(nameof(RedoStack));
            }

            return false;
        }
        catch (Exception)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the project needs to be closed"
            }));
            await TryCloseProjectAsync(ignoreUnsavedChanges: true);

            return false;
        }
        finally
        {
            IsActionRunning = false;
        }
    }

    private async Task<bool> UndoActionAsync(IProjectAction action)
    {
        if (CurrentProject == null)
            return false;

        try
        {
            IsActionRunning = true;
            LogService?.Log.Information("Undoing action {Action}", action.GetType().Name);
            await action.UndoAsync(CurrentProject, UiDispatcherQueue!);

            // update undo/redo
            RedoStack.Push(action);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            return true;
        }
        catch (ActionFailedAndRolledBackException)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the actions couldn't be completed"
            }));

            // update undo stack
            UndoStack.Push(action);
            OnPropertyChanged(nameof(CanUndo));

            return false;
        }
        catch (Exception)
        {
            Messenger.Send(new ShowInAppNotificationMessage(new Notification
            {
                Title = "Something went wrong and the project needs to be closed"
            }));
            await TryCloseProjectAsync(ignoreUnsavedChanges: true);

            return false;
        }
        finally
        {
            IsActionRunning = false;
        }
    }

    public async Task TryUndoAsync(IProjectAction? upUntil = null)
    {
        if (!CanUndo) return;
        if (upUntil != null && !UndoStack.Contains(upUntil)) return;

        SentryService?.TrackEvent(AnalyticsEvent.Undo);

        TaskCompletionSource process = new();

        if (upUntil == null)
            upUntil = UndoStack.Peek();
        else if (upUntil != UndoStack.Peek())
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, process.Task));

        bool success = true;
        while (UndoStack.TryPeek(out IProjectAction? action) && action != upUntil && success)
        {
            success = await UndoActionAsync(UndoStack.Pop());
        }

        if (UndoStack.Count > 0 && success)
            await UndoActionAsync(UndoStack.Pop());

        process.TrySetResult();
    }

    public async Task TryRedoAsync(IProjectAction? upUntil = null)
    {
        if (!CanRedo) return;
        if (upUntil != null && !RedoStack.Contains(upUntil)) return;

        SentryService?.TrackEvent(AnalyticsEvent.Redo);

        TaskCompletionSource process = new();

        if (upUntil == null)
            upUntil = RedoStack.Peek();
        else if (upUntil != RedoStack.Peek())
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, process.Task));

        bool success = true;
        while (RedoStack.TryPeek(out IProjectAction? action) && action != upUntil && success)
        {
            success = await InternalApplyActionAsync(RedoStack.Pop(), true);
        }
        if (RedoStack.Count > 0 && success)
            await InternalApplyActionAsync(RedoStack.Pop(), true);

        process.TrySetResult();
    }

    public async Task<bool> TryConvertProjectAsync(TargetFormat targetFormat, DispatcherQueue uiDispatcherQueue)
    {
        if (CurrentProject == null) return false;
        IsActionRunning = true;

        // capture the original format before the project is closed/replaced
        TargetFormat originalFormat = CurrentProject.Format;
        LogService?.Log.Information("Converting the current project from {From} to {To}", originalFormat, targetFormat);

        try
        {
            // collect page data for new project
            IProjectCreationData? creationData = null;
            switch (targetFormat)
            {
                case TargetFormat.PDF:
                    // get file name and target folder from first page
                    ImagePage imagePage = (ImagePage)CurrentProject.Pages.First(x => x is ImagePage);

                    creationData = new PdfProjectCreationData(null, CurrentProject.Pages, imagePage.FileNameInfo!.DesiredName, imagePage.TargetFolder, CurrentProject.InitialScanOptions, false);
                    break;
                case TargetFormat.JPG:
                case TargetFormat.PNG:
                case TargetFormat.BMP:
                case TargetFormat.SinglePagePDF:
                case TargetFormat.TIFF:
                case TargetFormat.RAW:
                    if (CurrentProject is MultiFileProject imageProject)
                        creationData = new MultiFileProjectCreationData(null, CurrentProject.Pages, targetFormat, null, CurrentProject.InitialScanOptions, false);
                    else if (CurrentProject is PdfProject pdfProject)
                        creationData = new MultiFileProjectCreationData(null, CurrentProject.Pages, targetFormat, pdfProject.FileNameInfo.DesiredDisplayName + TargetFormatToFileExtension(targetFormat), CurrentProject.InitialScanOptions, false);
                    break;
                default:
                    throw new ArgumentException("Can't convert project to " + targetFormat.ToString());
            }

            if (creationData == null)
                return false;

            // close project and preserve files
            if (!await TryCloseProjectAsync(preserveSourceFilesInIncomingFolder: true))
                return false;

            // create new project from preserved files
            Task createProjectTask = TryCreateProjectAsync(creationData, false, false, uiDispatcherQueue);
            Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, createProjectTask));
            await createProjectTask;

            // analytics
            SentryService?.TrackEvent(AnalyticsEvent.ConvertProject, new Dictionary<string, string>
            {
                { "from", originalFormat.ToString() },
                { "to", targetFormat.ToString() },
            });
        }
        finally
        {
            IsActionRunning = false;
        }

        return true;
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.SettingAutoSave):
                ResetAutoSaveTimer();
                break;
        }
    }

    private void ResetAutoSaveTimer()
    {
        // TODO: ensure auto save continues after exception
        if (CurrentProject != null && SettingsService?.SettingAutoSave == true)
        {
            TimerElapsedHandler? handler = null;
            handler = new TimerElapsedHandler(async (source) =>
            {
                if (SettingsService?.SettingAutoSave == false)
                    return;

                if (CurrentProject != null && !CurrentProject.IsSaved)
                {
                    await CurrentProject.SaveAsync(false, UiDispatcherQueue);
                }
                autoSaveTimer = ThreadPoolTimer.CreateTimer(handler, TimeSpan.FromSeconds(5));
            });
            autoSaveTimer?.Cancel();
            autoSaveTimer = ThreadPoolTimer.CreateTimer(handler, TimeSpan.FromSeconds(5));
        }
        else
        {
            autoSaveTimer?.Cancel();
            autoSaveTimer = null;
        }
    }

    private void CurrentProject_PagesAdded(object? sender, EventArgs e)
    {
        // automatically select last page when pages are added
        if (sender is ProjectBase project && project.Pages is ObservableCollection<IProjectPage> pages)
        {
            if (pages.Count == 0) return;
            SelectedPage = pages[pages.Count - 1];
        }

        OnPropertyChanged(nameof(TotalNumberOfPages));
    }

    private void CurrentProject_PagesRemoved(object? sender, EventArgs e)
    {
        if (CurrentProject == null)
            return;

        // update selection accordingly
        if (SelectedPages != null)
        {
            for (int i = 0; i < SelectedPages.Count; i++)
            {
                if (CurrentProject?.Pages.Contains(SelectedPages[i]) == false)
                {
                    SelectedPages.RemoveAt(i);
                    i--;
                }
            }
        }
        else if (SelectedPage != null && CurrentProject?.Pages.Contains(SelectedPage) == false)
        {
            SelectedPage = CurrentProject?.Pages.FirstOrDefault();
        }
        else if (SelectedPage == null)
        {
            SelectedPage = CurrentProject?.Pages.LastOrDefault();
        }

        OnPropertyChanged(nameof(TotalNumberOfPages));
    }

    private void CurrentProject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectBase.IsSaving):
            case nameof(ProjectBase.IsSaved):
                OnPropertyChanged(nameof(CanSaveProject));
                OnPropertyChanged(nameof(CanSaveAsProject));
                UpdateTaskbarProgress();

                if (CurrentProject != null && CurrentProject.IsSaved)
                {
                    UiDispatcherQueue?.RunOnThread(DispatcherQueuePriority.Low, () =>
                    {
                        ProjectHistoryService.AddOrUpdateEntryAsync(CurrentProject);
                    });
                }
                break;
        }
    }

    private void SelectedPages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedPagesCount));
    }

    private string GetFriendlyCurrentScanState()
    {
        return CurrentScanState switch
        {
            ScanState.Scanning => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressScanning),
            ScanState.AutomaticRotation => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressAutomaticRotation),
            ScanState.GeneratingPDF => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressPdfGeneration),
            ScanState.Processing => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressProcessing),
            ScanState.Saving => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanProgressSaving),
            _ => "",
        };
    }

    /// <summary>
    ///     Calculates the index where a new page will be placed in the <see cref="ScanResult"/>. If the insertion is
    ///     reversed, the index is shifted accordingly to accomodate inserting pages starting from the back unless
    ///     <paramref name="forInsertion"/> is false.
    /// </summary>
    /// <param name="indexOfNewPage">Index of the new page among all new pages.</param>
    /// <param name="totalNewPages">The number of pages being added in total.</param>
    internal static int GetNewIndexAccordingToMergeConfig(ScanMergeConfig mergeConfig, int indexOfNewPage, int totalNewPages,
        bool forInsertion = false)
    {
        if (!mergeConfig.InsertReversed)
        {
            // insert normally
            if (indexOfNewPage < mergeConfig.InsertIndices.Count)
            {
                return mergeConfig.InsertIndices[indexOfNewPage];
            }
            else
            {
                // surplus page
                return mergeConfig.SurplusPagesIndex + indexOfNewPage - mergeConfig.InsertIndices.Count;
            }
        }
        else
        {
            // insert reversed
            if (forInsertion)
            {
                if (totalNewPages - 1 - indexOfNewPage < mergeConfig.InsertIndices.Count)
                {
                    return mergeConfig.InsertIndices[totalNewPages - 1 - indexOfNewPage] - (totalNewPages - 1 - indexOfNewPage);
                }
                else
                {
                    // surplus page
                    return mergeConfig.SurplusPagesIndex - mergeConfig.InsertIndices.Count;
                }
            }
            else
            {
                if (totalNewPages - 1 - indexOfNewPage < mergeConfig.InsertIndices.Count)
                {
                    return mergeConfig.InsertIndices[totalNewPages - 1 - indexOfNewPage];
                }
                else
                {
                    // surplus page
                    return mergeConfig.SurplusPagesIndex + totalNewPages - 1 - indexOfNewPage - mergeConfig.InsertIndices.Count;
                }
            }
        }
    }

    private void UpdateTaskbarProgress()
    {
        ITaskbarList3 taskbarList = (ITaskbarList3)new TaskbarList();
        taskbarList.HrInit();
        TBPFLAG flag = IsScanProcessRunning || (CurrentProject != null && CurrentProject.IsSaving) ? TBPFLAG.TBPF_INDETERMINATE : TBPFLAG.TBPF_NOPROGRESS;
        taskbarList.SetProgressState((Windows.Win32.Foundation.HWND)((App)App.Current).MainWindow.GetWindowHandle(), flag);
    }

    private void ShowScanCompleteToastNotification()
    {
        if (((App)Application.Current).MainWindow.IsInForeground)
            return;

        AppNotificationBuilder builder = new AppNotificationBuilder()
            .AddText(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanCompleteNotificationHeading))
            .AddText(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ScanCompleteNotificationBody));

        AppNotificationManager.Default.Show(builder.BuildNotification());
    }

    public void TryCancelScan()
    {
        try
        {
            if (!IsScanProcessRunning)
                return;

            LogService?.Log.Information("Trying to cancel scan");

            activeScanningDevice?.CancelScan();
            scanCancellationTokenSource?.Cancel();
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to cancel scan");
        }
    }
}
