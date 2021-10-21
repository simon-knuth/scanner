using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using static Utilities;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages app settings and other persistent values.
    /// </summary>
    internal class SettingsService : ObservableObject, ISettingsService
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

        public event EventHandler<AppSetting> SettingChanged;
        public event EventHandler ScanSaveLocationChanged;
        public event EventHandler LastSaveLocationPathChanged;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsService()
        {
            Task.Run(async () => await InitializeAsync());
            LogAllSettings();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async Task InitializeAsync()
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
                        LogService?.Log.Error(exc2, "Creating a new scan save location in PicturesLibrary failed as well.");
                        ShowMessageDialogAsync(LocalizedString("ErrorMessageLoadScanFolderHeader"),
                            LocalizedString("ErrorMessageLoadScanFolderBody"));
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
                    LogService?.Log.Error(exc, "Creating a new scan save location in PicturesLibrary failed. (Unauthorized)");
                    ShowMessageDialogAsync(LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"), LocalizedString("ErrorMessageResetFolderUnauthorizedBody"));
                    return;
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Creating a new scan save location in PicturesLibrary failed.");
                    ShowMessageDialogAsync(LocalizedString("ErrorMessageResetFolderHeading"), LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message);
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

                default:
                    throw new ArgumentException("Can not retrieve value for unknown setting " + setting + ".");
            }
        }

        /// <summary>
        ///     Save a setting's value.
        /// </summary>
        public void SetSetting(AppSetting setting, object value)
        {
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

                default:
                    throw new ArgumentException("Can not save value for unknown setting " + setting + ".");
            }

            SettingChanged?.Invoke(this, setting);
        }

        public async Task SetScanSaveLocationAsync(StorageFolder folder)
        {
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

        private async Task<bool?> CheckScanSaveLocationDefaultAsync()
        {
            if (ScanSaveLocation == null) return false;

            StorageFolder folder;
            try
            {
                folder = await KnownFolders.PicturesLibrary.GetFolderAsync(GetDefaultScanFolderName());
            }
            catch (Exception)
            {
                return false;
            }

            bool result = folder.Path == ScanSaveLocation.Path;
            _IsSaveLocationFolderDefault = result;

            if ((SettingSaveLocationType)GetSetting(AppSetting.SettingSaveLocationType) == SettingSaveLocationType.AskEveryTime)
            {
                return null;
            }
            else
            {
                return result;
            }
        }

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

        public void LogAllSettings()
        {
            string logString = "Settings loaded: ";
            foreach (AppSetting setting in Enum.GetValues(typeof(AppSetting)))
            {
                logString += $"{setting}={GetSetting(setting)} | ";
            }
            logString = logString.Remove(logString.Length - 3);
            LogService?.Log.Information(logString);
        }
    }
}
