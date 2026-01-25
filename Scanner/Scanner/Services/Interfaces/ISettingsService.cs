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
using Scanner.Models.Interfaces;
using System.Globalization;
using Windows.Storage;
using Serilog;
using System.ComponentModel;
using Scanner.ViewModels;
using Scanner.Models.ItemNaming;
using Scanner.Views;
using Scanner.Helpers;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Manages app settings and other persistent values.
    /// </summary>
    public interface ISettingsService : INotifyPropertyChanged
    {
        int Version { get; set; }
        SettingSaveLocationType SettingSaveLocationType { get; set; }
        SettingAppTheme SettingAppTheme { get; set; }
        bool SettingAutoRotate { get; set; }
        SettingEditorOrientation SettingEditorOrientation { get; set; }
        bool SettingRememberScanOptions { get; set; }
        bool SettingErrorStatistics { get; set; }
        bool SettingShowSurveys { get; set; }
        string LastKnownVersion { get; set; }
        int ScanNumber { get; set; }
        bool LastTouchDrawState { get; set; }
        bool IsFirstAppLaunchWithThisVersion { get; set; }
        bool IsFirstAppLaunchEver { get; set; }
        AspectRatio LastUsedCropAspectRatio { get; set; }
        bool ShowOpenWithWarning { get; set; }
        bool ShowAutoRotationMessage { get; set; }
        bool SetupCompleted { get; set; }
        bool SettingAnimations { get; set; }
        SettingScanAction SettingScanAction { get; set; }
        SettingMeasurementUnits SettingMeasurementUnits { get; set; }
        bool TutorialScanMergeShown { get; set; }
        string SettingAppLanguage { get; set; }
        bool LastScanMergeReversed { get; set; }
        bool SettingExpandPageList { get; set; }
        bool SettingMirrorAppLayout { get; set; }
        string UserId { get; set; }
        int DiagnosticEventsSentThisSession { get; set; }
        bool SettingAutoSave { get; set; }
        SettingFileNamingPattern SettingFileNamingPattern { get; set; }
        ItemNamingPattern CustomFileNamingPattern { get; set; }
        bool SettingUseSubFolder { get; set; }
        SettingSubFolderNamingPattern SettingSubFolderNamingPattern { get; set; }
        ItemNamingPattern CustomSubFolderNamingPattern { get; set; }
        bool SettingGenerateFileNameWithAI { get; set; }
        bool SettingOcrPdfs { get; set; }

        string? LastOpenWithAppPdf { get; set; }
        string? LastOpenWithAppJpg { get; set; }
        string? LastOpenWithAppPng { get; set; }
        string? LastOpenWithAppBmp { get; set; }
        string? LastOpenWithAppTiff { get; set; }

        void TryLogAllSettings();
    }

    public enum SettingSaveLocationType
    {
        /// <summary>
        /// Save files to a fixed location.
        /// </summary>
        FixedLocation = 0,

        /// <summary>
        /// Ask for a location before starting a new project.
        /// </summary>
        AskBeforeNewProject = 1,

        /// <summary>
        /// Ask for a location once the user saves a project.
        /// </summary>
        AskAfterNewProject = 2,

        /// <summary>
        /// Ask for a location every time a new project is started or a new file is being added.
        /// </summary>
        AskEveryTime = 3
    }

    public enum SettingAppTheme
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    public enum SettingEditorOrientation
    {
        Vertical = 1,
        Horizontal = 0
    }

    public enum SettingScanAction
    {
        AddToExisting = 0,
        StartFresh = 1
    }

    public enum SettingMeasurementUnits
    {
        Metric = 0,
        ImperialUS = 1
    }

    public enum SettingFileNamingPattern
    {
        DateTime = 0,
        Date = 1,
        Custom = 2
    }

    public enum SettingSubFolderNamingPattern
    {
        Date = 0,
        FileType = 1,
        Custom = 2
    }
}
