using System;
using Windows.Storage;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    public sealed class SettingsService : ISettingsService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ApplicationDataContainer SettingsContainer = ApplicationData.Current.LocalSettings;

        public event EventHandler<AppSetting> SettingChanged;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Retrieve a setting's value and substitute null values with default ones.
        /// </summary>
        public object GetSetting(AppSetting setting)
        {
            switch (setting)
            {
                case AppSetting.SettingSaveLocationType:
                    return SettingsContainer.Values["SettingSaveLocationType"] ?? SettingSaveLocationType.SetLocation;

                case AppSetting.SettingAppTheme:
                    return SettingsContainer.Values["SettingAppTheme"] ?? SettingAppTheme.System;

                case AppSetting.SettingAutoRotate:
                    return SettingsContainer.Values["SettingAutoRotate"] ?? SettingAutoRotate.AskEveryTime;

                case AppSetting.SettingAppendTime:
                    return SettingsContainer.Values["SettingAppendTime"] ?? true;

                case AppSetting.SettingEditorOrientation:
                    return SettingsContainer.Values["SettingEditorOrientation"] ?? SettingEditorOrientation.Horizontal;

                case AppSetting.SettingRememberScanOptions:
                    return SettingsContainer.Values["SettingRememberScanOptions"] ?? true;

                case AppSetting.SettingErrorStatistics:
                    return SettingsContainer.Values["SettingErrorStatistics"] ?? false;

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
                    break;

                case AppSetting.SettingAppTheme:
                    SettingsContainer.Values[name] = (int)value;
                    break;

                case AppSetting.SettingAutoRotate:
                    SettingsContainer.Values[name] = (int)value;
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

                default:
                    throw new ArgumentException("Can not save value for unknown setting " + setting + ".");
            }

            SettingChanged?.Invoke(this, setting);
        }
    }
}
