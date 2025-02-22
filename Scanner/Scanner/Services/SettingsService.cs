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
using Serilog.Formatting.Compact;
using Serilog.Exceptions;
using CommunityToolkit.Mvvm.DependencyInjection;
using System.ComponentModel;
using Scanner.ViewModels;
using static Scanner.Helpers.Helpers;
using System.Security.Cryptography;

namespace Scanner.Services
{
    internal class SettingsService : ObservableObject, ISettingsService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        public int Version
        {
            get => GetSetting<int>(nameof(Version), 0);
            set => SetSetting(nameof(Version), value);
        }

        public SettingSaveLocationType SettingSaveLocationType
        {
            get => (SettingSaveLocationType)GetSetting<int>(nameof(SettingSaveLocationType), (int)SettingSaveLocationType.SetLocation);
            set => SetSetting(nameof(SettingSaveLocationType), value);
        }

        public SettingAppTheme SettingAppTheme
        {
            get => (SettingAppTheme)GetSetting<int>(nameof(SettingAppTheme), (int)SettingAppTheme.System);
            set => SetSetting(nameof(SettingAppTheme), value);
        }

        public bool SettingAutoRotate
        {
            get => GetSetting<bool>(nameof(SettingAutoRotate), true);
            set => SetSetting(nameof(SettingAutoRotate), value);
        }

        public SettingEditorOrientation SettingEditorOrientation
        {
            get => (SettingEditorOrientation)GetSetting<int>(nameof(SettingEditorOrientation), (int)SettingEditorOrientation.Horizontal);
            set => SetSetting(nameof(SettingEditorOrientation), value);
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
            get => GetSetting<int>(nameof(ScanNumber), 0);
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

        public AspectRatioOption LastUsedCropAspectRatio
        {
            get => (AspectRatioOption)GetSetting<int>(nameof(LastUsedCropAspectRatio), (int)AspectRatioOption.Custom);
            set => SetSetting(nameof(LastUsedCropAspectRatio), value);
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
            get => (SettingScanAction)GetSetting<int>(nameof(SettingScanAction), (int)SettingScanAction.AddToExisting);
            set => SetSetting(nameof(SettingScanAction), value);
        }

        public SettingMeasurementUnit SettingMeasurementUnits
        {
            get => (SettingMeasurementUnit)GetSetting<int>(nameof(SettingMeasurementUnits), (int)SettingMeasurementUnit.Metric);
            set => SetSetting(nameof(SettingMeasurementUnits), value);
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
            LogService?.Log.Information("SettingsService - Setting {Name} to {Value}", name, value);

            settingsContainer.Values[name.ToUpper()] = value;
        }

        public void TryLogAllSettings()
        {
            throw new NotImplementedException();
        }
    }
}
