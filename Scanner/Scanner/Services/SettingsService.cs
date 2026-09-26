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
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using Serilog.Sinks.File;
using Serilog;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Serilog.Exceptions;
using CommunityToolkit.Mvvm.DependencyInjection;
using System.ComponentModel;
using Scanner.ViewModels;
using static Scanner.Helpers.Helpers;
using System.Security.Cryptography;
using Scanner.Models.ItemNaming;
using Scanner.Views;
using Scanner.Helpers;
using Microsoft.Windows.AppLifecycle;

namespace Scanner.Services;

internal class SettingsService : ObservableObject, ISettingsService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ICopilotRuntimeService CopilotRuntimeService = Ioc.Default.GetRequiredService<ICopilotRuntimeService>();
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    public int Version
    {
        get => GetSetting(nameof(Version), 0);
        set => SetSetting(nameof(Version), value);
    }

    public SettingSaveLocationType SettingSaveLocationType
    {
        get => (SettingSaveLocationType)GetSetting(nameof(SettingSaveLocationType), (int)SettingSaveLocationType.AskAfterNewProject);
        set => SetSetting(nameof(SettingSaveLocationType), (int)value);
    }

    public SettingAppTheme SettingAppTheme
    {
        get => (SettingAppTheme)GetSetting(nameof(SettingAppTheme), (int)SettingAppTheme.System);
        set => SetSetting(nameof(SettingAppTheme), (int)value);
    }

    public bool SettingAutoRotate
    {
        get => GetSetting<bool>(nameof(SettingAutoRotate), true);
        set => SetSetting(nameof(SettingAutoRotate), value);
    }

    public SettingEditorOrientation SettingEditorOrientation
    {
        get => (SettingEditorOrientation)GetSetting(nameof(SettingEditorOrientation), (int)SettingEditorOrientation.Horizontal);
        set => SetSetting(nameof(SettingEditorOrientation), (int)value);
    }

    public bool SettingRememberScanOptions
    {
        get => GetSetting<bool>(nameof(SettingRememberScanOptions), true);
        set => SetSetting(nameof(SettingRememberScanOptions), value);
    }

    public bool SettingErrorStatistics
    {
        get => GetSetting<bool>(nameof(SettingErrorStatistics), false);
        set => SetSetting(nameof(SettingErrorStatistics), value);
    }

    public bool SettingShowSurveys
    {
        get => GetSetting<bool>(nameof(SettingShowSurveys), true);
        set => SetSetting(nameof(SettingShowSurveys), value);
    }

    public string LastKnownVersion
    {
        get => GetSetting<string>(nameof(LastKnownVersion), "");
        set => SetSetting(nameof(LastKnownVersion), value);
    }

    public int ScanNumber
    {
        get => GetSetting(nameof(ScanNumber), 0);
        set => SetSetting(nameof(ScanNumber), value);
    }

    public bool LastTouchDrawState
    {
        get => GetSetting<bool>(nameof(LastTouchDrawState), true);
        set => SetSetting(nameof(LastTouchDrawState), value);
    }

    public bool IsFirstAppLaunchWithThisVersion
    {
        get => GetSetting<bool>(nameof(IsFirstAppLaunchWithThisVersion), false);
        set => SetSetting(nameof(IsFirstAppLaunchWithThisVersion), value);
    }

    public bool IsFirstAppLaunchEver
    {
        get => GetSetting<bool>(nameof(IsFirstAppLaunchEver), true);
        set => SetSetting(nameof(IsFirstAppLaunchEver), value);
    }

    public AspectRatio LastUsedCropAspectRatio
    {
        get => (AspectRatio)GetSetting(nameof(LastUsedCropAspectRatio), (int)AspectRatio.Custom);
        set => SetSetting(nameof(LastUsedCropAspectRatio), (int)value);
    }

    public bool ShowOpenWithWarning
    {
        get => GetSetting<bool>(nameof(ShowOpenWithWarning), true);
        set => SetSetting(nameof(ShowOpenWithWarning), value);
    }

    public bool ShowAutoRotationMessage
    {
        get => GetSetting<bool>(nameof(ShowAutoRotationMessage), true);
        set => SetSetting(nameof(ShowAutoRotationMessage), value);
    }

    public bool SetupCompleted
    {
        get => GetSetting<bool>(nameof(SetupCompleted), false);
        set => SetSetting(nameof(SetupCompleted), value);
    }

    public bool SettingAnimations
    {
        get => GetSetting<bool>(nameof(SettingAnimations), true);
        set => SetSetting(nameof(SettingAnimations), value);
    }

    public SettingScanAction SettingScanAction
    {
        get => (SettingScanAction)GetSetting(nameof(SettingScanAction), (int)SettingScanAction.AddToExisting);
        set => SetSetting(nameof(SettingScanAction), (int)value);
    }

    public SettingMeasurementUnits SettingMeasurementUnits
    {
        get => (SettingMeasurementUnits)GetSetting(nameof(SettingMeasurementUnits), (int)SettingMeasurementUnits.Metric);
        set => SetSetting(nameof(SettingMeasurementUnits), (int)value);
    }

    public bool TutorialScanMergeShown
    {
        get => GetSetting<bool>(nameof(TutorialScanMergeShown), false);
        set => SetSetting(nameof(TutorialScanMergeShown), value);
    }

    public string SettingAppLanguage
    {
        get => GetSetting<string>(nameof(SettingAppLanguage), "SYSTEM");
        set => SetSetting(nameof(SettingAppLanguage), value);
    }

    public bool LastScanMergeReversed
    {
        get => GetSetting<bool>(nameof(LastScanMergeReversed), true);
        set => SetSetting(nameof(LastScanMergeReversed), value);
    }

    public bool SettingExpandPageList
    {
        get => GetSetting<bool>(nameof(SettingExpandPageList), true);
        set => SetSetting(nameof(SettingExpandPageList), value);
    }

    public bool TutorialScanOptionsButtonShown
    {
        get => GetSetting<bool>(nameof(TutorialScanOptionsButtonShown), false);
        set => SetSetting(nameof(TutorialScanOptionsButtonShown), value);
    }

    public bool SettingMirrorAppLayout
    {
        get => GetSetting<bool>(nameof(SettingMirrorAppLayout), false);
        set => SetSetting(nameof(SettingMirrorAppLayout), value);
    }

    public string? UserId
    {
        get => GetSetting<string?>(nameof(UserId), null);
        set => SetSetting(nameof(UserId), value);
    }

    public int DiagnosticEventsSentThisSession
    {
        get => GetSetting(nameof(DiagnosticEventsSentThisSession), 0);
        set => SetSetting(nameof(DiagnosticEventsSentThisSession), value);
    }

    public int ErrorFeedbackSentThisSession
    {
        get => GetSetting(nameof(DiagnosticEventsSentThisSession), 0);
        set => SetSetting(nameof(DiagnosticEventsSentThisSession), value);
    }

    public bool SettingAutoSave
    {
        get => GetSetting<bool>(nameof(SettingAutoSave), true);
        set => SetSetting(nameof(SettingAutoSave), value);
    }

    public SettingFileNamingPattern SettingFileNamingPattern
    {
        get => (SettingFileNamingPattern)GetSetting(nameof(SettingFileNamingPattern), (int)SettingFileNamingPattern.DateTime);
        set => SetSetting(nameof(SettingFileNamingPattern), (int)value);
    }

    public ItemNamingPattern CustomFileNamingPattern
    {
        get => new ItemNamingPattern(GetSetting(nameof(CustomFileNamingPattern), ItemNamingStatics.FileDefaultCustomPattern.GetSerialized(false)));
        set => SetSetting(nameof(CustomFileNamingPattern), value.GetSerialized(false));
    }

    public bool SettingUseSubfolder
    {
        get => GetSetting<bool>(nameof(SettingUseSubfolder), true);
        set => SetSetting(nameof(SettingUseSubfolder), value);
    }

    public SettingSubfolderNamingPattern SettingSubfolderNamingPattern
    {
        get => (SettingSubfolderNamingPattern)GetSetting(nameof(SettingSubfolderNamingPattern), (int)SettingSubfolderNamingPattern.Date);
        set => SetSetting(nameof(SettingSubfolderNamingPattern), (int)value);
    }

    public ItemNamingPattern CustomSubfolderNamingPattern
    {
        get => new ItemNamingPattern(GetSetting(nameof(CustomSubfolderNamingPattern), ItemNamingStatics.FolderDefaultCustomPattern.GetSerialized(false)));
        set => SetSetting(nameof(CustomSubfolderNamingPattern), value.GetSerialized(false));
    }

    public bool SettingGenerateFileNameWithAI
    {
        get => GetSetting<bool>(nameof(SettingGenerateFileNameWithAI), CopilotRuntimeService.AreModelsInstalled);
        set => SetSetting(nameof(SettingGenerateFileNameWithAI), value);
    }

    public bool SettingOcrPdfs
    {
        get => GetSetting<bool>(nameof(SettingOcrPdfs), true);
        set => SetSetting(nameof(SettingOcrPdfs), value);
    }

    public TemplateSortMode SettingTemplateSortMode
    {
        get => (TemplateSortMode)GetSetting(nameof(SettingTemplateSortMode), (int)TemplateSortMode.RecentlyUsed);
        set => SetSetting(nameof(SettingTemplateSortMode), (int)value);
    }

    public string? LastOpenWithAppPdf
    {
        get => GetSetting<string?>(nameof(LastOpenWithAppPdf), null);
        set => SetSetting(nameof(LastOpenWithAppPdf), value);
    }

    public string? LastOpenWithAppJpg
    {
        get => GetSetting<string?>(nameof(LastOpenWithAppJpg), null);
        set => SetSetting(nameof(LastOpenWithAppJpg), value);
    }

    public string? LastOpenWithAppPng
    {
        get => GetSetting<string?>(nameof(LastOpenWithAppPng), null);
        set => SetSetting(nameof(LastOpenWithAppPng), value);
    }

    public string? LastOpenWithAppBmp
    {
        get => GetSetting<string?>(nameof(LastOpenWithAppBmp), null);
        set => SetSetting(nameof(LastOpenWithAppBmp), value);
    }

    public string? LastOpenWithAppTiff
    {
        get => GetSetting<string?>(nameof(LastOpenWithAppTiff), null);
        set => SetSetting(nameof(LastOpenWithAppTiff), value);
    }

    public bool IsAppRestartRequired => changedSettingsRequiringRestart.Count != 0;

    private ApplicationDataContainer settingsContainer = ApplicationData.Current.LocalSettings;
    private const int latestSettingsVersion = 0;

    // effective values (including defaults) of settings requiring an app restart, captured at startup
    private readonly Dictionary<string, object?> settingsRequiringRestartOriginalValues = [];
    private readonly HashSet<string> changedSettingsRequiringRestart = [];


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsService()
    {
        // capture original values of settings requiring an app restart
        settingsRequiringRestartOriginalValues[nameof(SettingAppTheme)] = (int)SettingAppTheme;
        settingsRequiringRestartOriginalValues[nameof(SettingAppLanguage)] = SettingAppLanguage;
        settingsRequiringRestartOriginalValues[nameof(SettingMirrorAppLayout)] = SettingMirrorAppLayout;

        // update settings version
        if (IsFirstAppLaunchEver)
        {
            Version = latestSettingsVersion;
            IsFirstAppLaunchEver = false;
        }

        // update app version related settings
        string currentVersion = GetCurrentVersion();
        if (LastKnownVersion != currentVersion)
        {
            IsFirstAppLaunchWithThisVersion = true;
            LastKnownVersion = currentVersion;
        }

        // initialize user ID
        if (UserId == null)
        {
            UserId = Guid.NewGuid().ToString();
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private T GetSetting<T>(string name, T defaultValue)
    {
        object value = settingsContainer.Values[name.ToUpper()];

        return value is T castValue ? castValue : defaultValue;
    }

    private void SetSetting<T>(string name, T value)
    {
        string key = name.ToUpper();
        object? currentValue = settingsContainer.Values[key];

        if (settingsRequiringRestartOriginalValues.TryGetValue(name, out object? originalValue))
        {
            // track state of settings requiring an app restart
            bool oldIsAppRestartRequiredValue = IsAppRestartRequired;
            if (object.Equals(originalValue, value))
            {
                // set to original value ~> no restart required
                changedSettingsRequiringRestart.Remove(name);
            }
            else
            {
                // set to new value ~> restart required
                changedSettingsRequiringRestart.Add(name);
            }

            if (oldIsAppRestartRequiredValue != IsAppRestartRequired)
                OnPropertyChanged(nameof(IsAppRestartRequired));
        }

        // raise property changed event
        if (currentValue is T castCurrentValue && EqualityComparer<T>.Default.Equals(castCurrentValue, value))
        {
            // value unchanged
            return;
        }

        LogService?.Log.Information("Setting {Name} to {Value}", name, value);

        settingsContainer.Values[key] = value;
        OnPropertyChanged(name);
    }

    public void ResetAllSettingsAndRestart()
    {
        LogService?.Log.Information("Resetting all settings values");
        settingsContainer.Values.Clear();
        AppInstance.Restart("");
    }

    public void TryLogAllSettings()
    {
        throw new NotImplementedException();
    }
}
