using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Services.Messenger;
using Scanner.ViewModels;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.System.UserProfile;
using static Enums;
using static Utilities;

namespace Scanner.Services
{
    internal class SettingsService : ObservableRecipient, ISettingsService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private IAppCenterService AppCenterService => Ioc.Default.GetService<IAppCenterService>();

        private const string FutureAccessListStringScanSaveLocation = "scanFolder";

        private ApplicationDataContainer SettingsContainer = ApplicationData.Current.LocalSettings;

        private StorageFolder _ScanSaveLocation;
        public StorageFolder ScanSaveLocation
        {
            get => _ScanSaveLocation;
            set => SetProperty(ref _ScanSaveLocation, value);
        }

        private bool? _IsScanSaveLocationDefault;
        public bool? IsScanSaveLocationDefault
        {
            get => _IsScanSaveLocationDefault;
            set => SetProperty(ref _IsScanSaveLocationDefault, value);
        }

        private string _LastSaveLocationPath;
        public string LastSaveLocationPath
        {
            get => _LastSaveLocationPath;
            set
            {
                SetProperty(ref _LastSaveLocationPath, value);
                LastSaveLocationPathChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        ///     Whether the set save location is the default one, regardless of
        ///     the currently selected <see cref="SettingSaveLocationType"/>.
        /// </summary>
        private bool _IsSaveLocationFolderDefault;

        private bool _IsSaveLocationUnavailable;
        public bool IsSaveLocationUnavailable
        {
            get => _IsSaveLocationUnavailable;
            private set
            {
                SetProperty(ref _IsSaveLocationUnavailable, value);
                if (value == true) AppCenterService.TrackEvent(AppCenterEvent.SetSaveLocationUnavailable);
            }
        }

        public event EventHandler<AppSetting> SettingChanged;
        public event EventHandler ScanSaveLocationChanged;
        public event EventHandler LastSaveLocationPathChanged;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsService()
        {
            // migrate settings if necessary
            if (!SystemInformation.Instance.IsFirstRun
                && SystemInformation.Instance.PreviousVersionInstalled.Major < 3
                && SystemInformation.Instance.ApplicationVersion.Major == 3)
            {
                MigrateSettingsToV3();
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Initializes the settings and especially the save location.
        /// </summary>
        public async Task InitializeAsync()
        {            
            // initialize save location
            var futureAccessList = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList;

            if (futureAccessList.Entries.Count != 0)
            {
                try
                {
                    _ScanSaveLocation = await futureAccessList.GetFolderAsync(FutureAccessListStringScanSaveLocation);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Loading scan save location from futureAccessList failed.");
                    try
                    {
                        _ScanSaveLocation = await KnownFolders.PicturesLibrary.CreateFolderAsync
                            (GetDefaultScanFolderName(), CreationCollisionOption.OpenIfExists);
                    }
                    catch (Exception exc2)
                    {
                        IsSaveLocationUnavailable = true;
                        SetSetting(AppSetting.SettingSaveLocationType, SettingSaveLocationType.AskEveryTime);
                        LogService?.Log.Error(exc2, "Creating a new scan save location in PicturesLibrary failed as well.");
                        AppCenterService.TrackError(exc2);
                    }
                }
            }
            else
            {
                // either first app launch ever or the futureAccessList is unavailable ~> Reset folder
                try
                {
                    _ScanSaveLocation = await KnownFolders.PicturesLibrary.CreateFolderAsync
                        (GetDefaultScanFolderName(), CreationCollisionOption.OpenIfExists);
                }
                catch (UnauthorizedAccessException exc)
                {
                    IsSaveLocationUnavailable = true;
                    SetSetting(AppSetting.SettingSaveLocationType, SettingSaveLocationType.AskEveryTime);
                    LogService?.Log.Error(exc, "Creating a new scan save location in PicturesLibrary failed. (Unauthorized)");
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                        MessageText = LocalizedString("ErrorMessageResetFolderUnauthorizedBody"),
                        AdditionalText = exc.Message,
                        Severity = AppWideStatusMessageSeverity.Error
                    });
                    AppCenterService.TrackError(exc);
                    return;
                }
                catch (Exception exc)
                {
                    IsSaveLocationUnavailable = true;
                    SetSetting(AppSetting.SettingSaveLocationType, SettingSaveLocationType.AskEveryTime);
                    LogService?.Log.Error(exc, "Creating a new scan save location in PicturesLibrary failed.");
                    Messenger.Send(new AppWideStatusMessage
                    {
                        Title = LocalizedString("ErrorMessageResetFolderHeading"),
                        MessageText = LocalizedString("ErrorMessageResetFolderBody"),
                        AdditionalText = exc.Message,
                        Severity = AppWideStatusMessageSeverity.Error
                    });
                    AppCenterService.TrackError(exc);
                    return;
                }
                futureAccessList.AddOrReplace(FutureAccessListStringScanSaveLocation, _ScanSaveLocation);
            }

            _IsScanSaveLocationDefault = await CheckScanSaveLocationDefaultAsync();
        }

        /// <summary>
        ///     Retrieve a setting's value and substitute null values with default ones.
        /// </summary>
        public object GetSetting(AppSetting setting)
        {
            string name = setting.ToString().ToUpper();

            switch (setting)
            {
                case AppSetting.SettingSaveLocationType:
                    return SettingsContainer.Values[name] ?? SettingSaveLocationType.SetLocation;

                case AppSetting.SettingAppTheme:
                    return SettingsContainer.Values[name] ?? SettingAppTheme.System;

                case AppSetting.SettingAutoRotate:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.SettingAppendTime:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.SettingEditorOrientation:
                    return SettingsContainer.Values[name] ?? SettingEditorOrientation.Horizontal;

                case AppSetting.SettingRememberScanOptions:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.SettingErrorStatistics:
                    return SettingsContainer.Values[name] ?? false;

                case AppSetting.SettingShowSurveys:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.TutorialPageListShown:
                    return SettingsContainer.Values[name] ?? false;

                case AppSetting.LastKnownVersion:
                    return SettingsContainer.Values[name] ?? "";

                case AppSetting.ScanNumber:
                    return SettingsContainer.Values[name] ?? 0;

                case AppSetting.LastTouchDrawState:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.IsFirstAppLaunchWithThisVersion:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.IsFirstAppLaunchEver:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.LastUsedCropAspectRatio:
                    if (SettingsContainer.Values[name] != null)
                    {
                        return (AspectRatioOption)(int)SettingsContainer.Values[name];
                    }
                    else
                    {
                        return AspectRatioOption.Custom;
                    }

                case AppSetting.ShowOpenWithWarning:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.ShowAutoRotationMessage:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.SetupCompleted:
                    return SettingsContainer.Values[name] ?? false;

                case AppSetting.SettingAutoRotateLanguage:
                    return SettingsContainer.Values[name] ?? OcrEngine.TryCreateFromUserProfileLanguages()?
                        .RecognizerLanguage?.LanguageTag ?? "";

                case AppSetting.SettingShowAdvancedScanOptions:
                    return SettingsContainer.Values[name] ?? false;

                case AppSetting.SettingAnimations:
                    return SettingsContainer.Values[name] ?? true;

                case AppSetting.SettingScanAction:
                    return SettingsContainer.Values[name] ?? SettingScanAction.AddToExisting;

                case AppSetting.SettingMeasurementUnits:
                    SettingMeasurementUnit measurementUnit = SettingMeasurementUnit.Metric;
                    try
                    {
                        if (new RegionInfo(CultureInfo.InstalledUICulture.LCID).IsMetric)
                        {
                            measurementUnit = SettingMeasurementUnit.Metric;
                        }
                        else
                        {
                            measurementUnit = SettingMeasurementUnit.ImperialUS;
                        }
                    }
                    catch (Exception) { }
                    
                    return SettingsContainer.Values[name] ?? measurementUnit;

                case AppSetting.TutorialScanMergeShown:
                    return SettingsContainer.Values[name] ?? false;

                default:
                    throw new ArgumentException("Can not retrieve value for unknown setting " + setting + ".");
            }
        }

        /// <summary>
        ///     Save a setting's value.
        /// </summary>
        public void SetSetting(AppSetting setting, object value)
        {
            LogService?.Log?.Information($"SetSetting: Setting value for {setting} to {value}");
            string name = setting.ToString().ToUpper();

            switch (setting)
            {
                case AppSetting.SettingSaveLocationType:
                    SettingsContainer.Values[name] = (int)value;
                    if ((SettingSaveLocationType)value == SettingSaveLocationType.AskEveryTime) IsScanSaveLocationDefault = null;
                    else IsScanSaveLocationDefault = _IsSaveLocationFolderDefault;
                    ScanSaveLocationChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case AppSetting.SettingAppTheme:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.SettingAutoRotate:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingAppendTime:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingEditorOrientation:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.SettingRememberScanOptions:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingErrorStatistics:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingShowSurveys:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.TutorialPageListShown:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.LastKnownVersion:
                    SettingsContainer.Values[name] = (string)value;
                    break;

                case AppSetting.ScanNumber:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.LastTouchDrawState:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.IsFirstAppLaunchWithThisVersion:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.IsFirstAppLaunchEver:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.LastUsedCropAspectRatio:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.ShowOpenWithWarning:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.ShowAutoRotationMessage:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SetupCompleted:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingAutoRotateLanguage:
                    SettingsContainer.Values[name] = (string)value;
                    break;

                case AppSetting.SettingShowAdvancedScanOptions:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingAnimations:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                case AppSetting.SettingScanAction:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.SettingMeasurementUnits:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.TutorialScanMergeShown:
                    SettingsContainer.Values[name] = (bool)value;
                    break;

                default:
                    throw new ArgumentException("Can not save value for unknown setting " + setting + ".");
            }

            SettingChanged?.Invoke(this, setting);
        }

        /// <summary>
        ///     Sets the save location to given <paramref name="folder"/>.
        /// </summary>
        public async Task SetScanSaveLocationAsync(StorageFolder folder)
        {
            LogService?.Log.Information("SetScanSaveLocationAsync");
            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList
                .AddOrReplace(FutureAccessListStringScanSaveLocation, folder);

            ScanSaveLocation = folder;

            if ((SettingSaveLocationType)GetSetting(AppSetting.SettingSaveLocationType) == SettingSaveLocationType.SetLocation)
            {
                IsScanSaveLocationDefault = await CheckScanSaveLocationDefaultAsync();
            }
            else
            {
                IsScanSaveLocationDefault = null;
            }
            
            ScanSaveLocationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Checks whether the currently active save location is the default one. Returns null, if the app is currently
        ///     asking every time.
        /// </summary>
        private async Task<bool?> CheckScanSaveLocationDefaultAsync()
        {
            if ((SettingSaveLocationType)GetSetting(AppSetting.SettingSaveLocationType) == SettingSaveLocationType.AskEveryTime)
            {
                LogService?.Log.Information("CheckScanSaveLocationDefaultAsync: Asking every time ~> return null");
                return null;
            }

            if (ScanSaveLocation == null)
            {
                LogService?.Log.Information("CheckScanSaveLocationDefaultAsync: Save location is null ~> return false");
                return false;
            }

            StorageFolder folder;
            try
            {
                folder = await KnownFolders.PicturesLibrary.GetFolderAsync(GetDefaultScanFolderName());
            }
            catch (Exception)
            {
                LogService?.Log.Information("CheckScanSaveLocationDefaultAsync: Default save location doesn't exist ~> return false");
                return false;
            }

            bool result = folder.Path == ScanSaveLocation.Path;
            _IsSaveLocationFolderDefault = result;

            LogService?.Log.Information($"CheckScanSaveLocationDefaultAsync: Return {result}");
            return result;
        }

        /// <summary>
        ///     Resets the current save location regardless of whether asking every time is active.
        /// </summary>
        public async Task ResetScanSaveLocationAsync()
        {
            StorageFolder folder;
            string defaultScanFolderName = GetDefaultScanFolderName();

            try
            {
                folder = await KnownFolders.PicturesLibrary.CreateFolderAsync(defaultScanFolderName, CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException exc)
            {
                LogService?.Log.Error(exc, "Resetting the scan save location failed. (Unauthorized)");
                AppCenterService?.TrackError(exc);
                throw;
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Resetting the scan save location failed.");
                AppCenterService?.TrackError(exc);
                throw;
            }

            await SetScanSaveLocationAsync(folder);
        }

        /// <summary>
        ///     Logs all current settings values.
        /// </summary>
        public void TryLogAllSettings()
        {
            try
            {
                string logString = "Settings loaded: ";
                foreach (AppSetting setting in Enum.GetValues(typeof(AppSetting)))
                {
                    try
                    {
                        string newValue = GetSetting(setting).ToString();
                        logString += $"{setting}={newValue} | ";
                    }
                    catch (Exception exc)
                    {
                        LogService?.Log.Error(exc, "TryLogAllSettings: Couldn't retrieve {setting}", setting);
                    }
                }
                logString = logString.Remove(logString.Length - 3);
                LogService?.Log.Information(logString);
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        ///     Returns the default name of the folder that scans are saved to. This varies depending on the system language.
        ///     The fallback name is "Scans".
        /// </summary>
        public string GetDefaultScanFolderName()
        {
            string defaultScanFolderName = LocalizedString("DefaultScanFolderName");
            bool validName = true;

            foreach (char character in defaultScanFolderName.ToCharArray())
            {
                if (!Char.IsLetter(character))
                {
                    validName = false;
                    break;
                }
            }

            if (defaultScanFolderName == "" || validName == false)
            {
                // use fallback name if there is an issue with the localization
                AppCenterService.TrackError(new ApplicationException($"The localized scan folder " +
                    $"name '{defaultScanFolderName}' is invalid, using 'Scans' instead."));
                defaultScanFolderName = "Scans";
            }

            return defaultScanFolderName;
        }

        /// <summary>
        ///     Migrates all settings from versions prior to v3.0 to the new format.
        /// </summary>
        public void MigrateSettingsToV3()
        {
            // hide setup
            SetSetting(AppSetting.SetupCompleted, true);
            
            // save location type
            if (SettingsContainer.Values["settingSaveLocationAsk"] != null)
            {
                if ((bool)SettingsContainer.Values["settingSaveLocationAsk"])
                {
                    SetSetting(AppSetting.SettingSaveLocationType, SettingSaveLocationType.AskEveryTime);
                }
            }

            // theme
            if (SettingsContainer.Values["settingAppTheme"] != null)
            {
                switch ((int)SettingsContainer.Values["settingAppTheme"])
                {
                    case 0:
                        SetSetting(AppSetting.SettingAppTheme, SettingAppTheme.System);
                        break;
                    case 1:
                        SetSetting(AppSetting.SettingAppTheme, SettingAppTheme.Light);
                        break;
                    case 2:
                        SetSetting(AppSetting.SettingAppTheme, SettingAppTheme.Dark);
                        break;
                    default:
                        SetSetting(AppSetting.SettingAppTheme, SettingAppTheme.System);
                        break;
                }
            }

            // append time
            if (SettingsContainer.Values["settingAppendTime"] != null)
            {
                SetSetting(AppSetting.SettingAppendTime, SettingsContainer.Values["settingAppendTime"]);
            }

            // analytics and error reports
            if (SettingsContainer.Values["settingErrorStatistics"] != null)
            {
                if ((bool)SettingsContainer.Values["settingErrorStatistics"])
                {
                    SetSetting(AppSetting.SettingErrorStatistics, SettingsContainer.Values["settingErrorStatistics"]);
                }
            }

            // scan number
            if (SettingsContainer.Values["scanNumber"] != null)
            {
                SetSetting(AppSetting.ScanNumber, SettingsContainer.Values["scanNumber"]);
            }

            // last touch draw state
            if (SettingsContainer.Values["lastTouchDrawState"] != null)
            {
                SetSetting(AppSetting.LastTouchDrawState, SettingsContainer.Values["lastTouchDrawState"]);
            }

            // page list tutorial
            if (SettingsContainer.Values["manageTutorialAlreadyShown"] != null)
            {
                SetSetting(AppSetting.TutorialPageListShown, SettingsContainer.Values["manageTutorialAlreadyShown"]);
            }
        }
    }
}
