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
using Scanner.Models.FileNaming;
using Scanner.Views;

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
        FileNamingPattern CustomFileNamingPattern { get; set; }
        bool SettingGenerateFileNameWithAI { get; set; }

        void TryLogAllSettings();
    }

    public enum SettingSaveLocationType
    {
        FixedLocation = 0,
        AskForEveryProject = 1,
        AskEveryTime = 2
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

    public enum AspectRatio
    {
        Custom = 0,
        Square = 1,
        ThreeByTwo = 2,
        FourByThree = 3,
        DinA = 4,
        AnsiA = 5,
        AnsiB = 6,
        AnsiC = 7,
        Kai4 = 8,
        Kai8 = 9,
        Kai16 = 10,
        Kai32 = 11,
        Legal = 12
    }
}
