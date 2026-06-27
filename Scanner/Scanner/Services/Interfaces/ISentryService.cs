using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Storage;
using System.ComponentModel;
using Sentry;

namespace Scanner.Services.Interfaces;

/// <summary>
///     Manages the Sentry integration.
/// </summary>
public interface ISentryService
{
    bool HasConsent
    {
        get;
    }

    string UserId
    {
        get;
    }

    void TrackWarning(Exception exception);
    void TrackError(Exception exception, bool isFatal = false);
    void TrackEvent(AnalyticsEvent sentryEvent, Dictionary<string, string>? attributes = null);

    /// <summary>
    ///     Tracks a value as a distribution metric, allowing the backend to aggregate statistics
    ///     (percentiles, min/max, average) over many recorded values. Useful for timings.
    /// </summary>
    void TrackDistributionMetric(AnalyticsMetric metric, double value, MeasurementUnit unit, Dictionary<string, string>? attributes = null);

    /// <summary>
    ///     Tracks a value as a gauge metric, recording statistics (last value, min/max, sum, count)
    ///     for a value that is sampled at points in time.
    /// </summary>
    void TrackGaugeMetric(AnalyticsMetric metric, double value, MeasurementUnit unit, Dictionary<string, string>? attributes = null);
    void SendErrorFeedback(string message, string? contactEmail, string? name);
    void SendSuggestionFeedback(string message, string? contactEmail, string? name);
    Task<string> GetCurrentLogPathAsync(bool flush);
    void GenerateTestCrash();
    void Initialize();
}


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public enum AnalyticsEvent
{
    AppUpdateAvailable,
    AppUpdateDownloaded,
    AppUpdateStarted,
    SetupStarted,
    SetupFinished,
    RatingStoreOpened,
    RatingStoreNotNow,
    RatingStoreNeverAskAgain,
    RatingStoreRate,
    Close,
    Launch,
    SettingsStats,
    ScannerAdded,
    ScanCompleted,
    ScanCanceled,
    Share,
    Preview,
    RotatePages,
    RenamePage,
    RenamePDF,
    Crop,
    CropMultiple,
    CropAsCopy,
    DeletePages,
    DeletePage,
    DrawOnPage, // TODO
    DrawOnPageAsCopy,   // TODO
    CopyPages,
    CopyPage,
    CopyDocument,
    OpenWith,
    DuplicatePage,  // TODO
    DonationDialogOpened,
    DonationLinkClicked,
    HelpRequested,  // TODO
    AutoRotatedPage,
    SetSaveLocationUnavailable,
    SettingsRequested,
    ChangelogOpened,
    ArchitectureDetected,
    OtherAppsDialogOpened,
    ApplyFilter,
    ManageScannersOpened,
    TemplateApplied,
    TemplateCreated,
    TemplateRemoved,
    TemplateRenamed,
    TemplatesCleared,
    HistoryViewOpened,
    HistoryEntryOpened,
    HistoryEntryRemoved,
    HistoryEntryShownInFileExplorer,
    HistoryCleared,
    TemplatesViewOpened,
    AIFileNameGenerationStarted,
    AIFileNameGenerationStopped,
    AIFileNameGenerationCancelled,
    ProjectOpenedFromDisk,
    ConvertProject,
    ExportPagesFromPdf,
    AddImageFiles,
    ReorderPages,
    ProjectSaved,
    UnsavedChangesDialogShown,
    UnsavedChangesDialogResolved,
    ProjectDeleted,
    Undo,
    Redo,
    PreviewRegionSelected,
    ScanMergeDialogOpened,
    ScanMergeConfirmed,
    CopilotModelDownloadStarted,
    CopilotModelDownloadCompleted,
    CopilotModelDownloadFailed,
    TestEvent
}

public enum AnalyticsMetric
{
    AIFileNameGenerationDuration,
    ScanDuration,
    ScanPageCount,
    KnownScannerCount,
    TemplateCount,
    AppColdStartDuration
}
