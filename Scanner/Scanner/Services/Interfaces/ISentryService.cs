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
    DrawOnPage,
    DrawOnPageAsCopy,
    CopyPages,
    CopyPage,
    CopyDocument,
    OpenWith,
    DuplicatePage,
    DonationDialogOpened,
    DonationLinkClicked,
    HelpRequested,
    AutoRotatedPage,
    CorrectedAutoRotation,
    SetSaveLocationUnavailable,
    SettingsRequested,
    ChangelogOpened,
    ArchitectureDetected,
    OtherAppsDialogOpened,
    TestEvent
}
