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

namespace Scanner.Services
{
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
            get => (SettingSaveLocationType)GetSetting(nameof(SettingSaveLocationType), (int)SettingSaveLocationType.FixedLocation);
            set
            {
                if (value == SettingSaveLocationType.AskAfterNewProject)
                {
                    SetSetting(nameof(SettingAutoSave), false);
                }
                else if (SettingSaveLocationType == SettingSaveLocationType.AskAfterNewProject)
                {
                    SetSetting(nameof(SettingAutoSave), true);
                }

                SetSetting(nameof(SettingSaveLocationType), (int)value);
            }
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
            get => GetSetting<string>(nameof(SettingAppLanguage), "");
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

        public bool SettingUseSubFolder
        {
            get => GetSetting<bool>(nameof(SettingUseSubFolder), true);
            set => SetSetting(nameof(SettingUseSubFolder), value);
        }

        public SettingSubFolderNamingPattern SettingSubFolderNamingPattern
        {
            get => (SettingSubFolderNamingPattern)GetSetting(nameof(SettingSubFolderNamingPattern), (int)SettingSubFolderNamingPattern.Date);
            set => SetSetting(nameof(SettingSubFolderNamingPattern), (int)value);
        }

        public ItemNamingPattern CustomSubFolderNamingPattern
        {
            get => new ItemNamingPattern(GetSetting(nameof(CustomSubFolderNamingPattern), ItemNamingStatics.FolderDefaultCustomPattern.GetSerialized(false)));
            set => SetSetting(nameof(CustomSubFolderNamingPattern), value.GetSerialized(false));
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

        private ApplicationDataContainer settingsContainer = ApplicationData.Current.LocalSettings;
        private const int latestSettingsVersion = 0;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsService()
        {
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
            LogService?.Log.Information("Setting {Name} to {Value}", name, value);

            settingsContainer.Values[name.ToUpper()] = value;
            OnPropertyChanged(name);
        }

        public void TryLogAllSettings()
        {
            throw new NotImplementedException();
        }
    }
}
