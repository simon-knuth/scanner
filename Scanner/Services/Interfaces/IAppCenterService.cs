using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages the Microsoft AppCenter integration.
    /// </summary>
    public interface IAppCenterService
    {
        void TrackEvent(AppCenterEvent appCenterEvent, IDictionary<string, string> properties = null);
        void TrackError(Exception exception, IDictionary<string, string> properties = null,
            params ErrorAttachmentLog[] attachments);
        void GenerateTestCrash();
        Task InitializeAsync();
    }

    public enum AppCenterEvent
    {
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
        SettingsRequested
    }
}
