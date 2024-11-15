﻿using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages app settings and other persistent values.
    /// </summary>
    public interface ISettingsService
    {
        event EventHandler<AppSetting> SettingChanged;
        event EventHandler ScanSaveLocationChanged;

        void TryLogAllSettings();
        void SetSetting(AppSetting setting, object value);
        object GetSetting(AppSetting setting);

        StorageFolder ScanSaveLocation
        {
            get;
        }

        bool? IsScanSaveLocationDefault
        {
            get;
        }

        string LastSaveLocationPath
        {
            get;
            set;
        }

        bool IsSaveLocationUnavailable
        {
            get;
        }

        Task SetScanSaveLocationAsync(StorageFolder folder);
        Task ResetScanSaveLocationAsync();
        void MigrateSettingsToV3();
        void MigrateSettingsToV3_2();
        Task InitializeAsync();
    }

    public enum AppSetting
    {
        SettingSaveLocationType,
        SettingAppTheme,
        SettingAutoRotate,
        SettingAppendTime,
        SettingEditorOrientation,
        SettingRememberScanOptions,
        SettingErrorStatistics,
        SettingShowSurveys,
        TutorialPageListShown,
        LastKnownVersion,
        ScanNumber,
        LastTouchDrawState,
        IsFirstAppLaunchWithThisVersion,
        IsFirstAppLaunchEver,
        LastUsedCropAspectRatio,
        ShowOpenWithWarning,
        ShowAutoRotationMessage,
        SetupCompleted,
        SettingAutoRotateLanguage,
        SettingShowAdvancedScanOptions,
        SettingAnimations,
        SettingScanAction,
        SettingMeasurementUnits,
        TutorialScanMergeShown,
        SettingAppLanguage,
        SettingFileNamingPattern,
        CustomFileNamingPattern,
        LastScanMergeReversed
    }

    public enum SettingSaveLocationType
    {
        SetLocation = 0,
        AskEveryTime = 1
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

    public enum SettingMeasurementUnit
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
}
