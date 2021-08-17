using System;
using Windows.Storage;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    class SettingsService : ISettingsService
    {
        private ApplicationDataContainer SettingsContainer => ApplicationData.Current.LocalSettings;
        
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
            switch (setting)
            {
                case AppSetting.SettingSaveLocationType:
                    SettingsContainer.Values["SettingSaveLocationType"] = (int)value;
                    break;

                case AppSetting.SettingAppTheme:
                    SettingsContainer.Values["SettingAppTheme"] = (int)value;
                    break;

                case AppSetting.SettingAutoRotate:
                    SettingsContainer.Values["SettingAutoRotate"] = (int)value;
                    break;

                case AppSetting.SettingAppendTime:
                    SettingsContainer.Values["SettingAppendTime"] = (bool)value;
                    break;

                case AppSetting.SettingEditorOrientation:
                    SettingsContainer.Values["SettingEditorOrientation"] = (int)value;
                    break;

                case AppSetting.SettingErrorStatistics:
                    SettingsContainer.Values["SettingErrorStatistics"] = (bool)value;
                    break;

                default:
                    throw new ArgumentException("Can not save value for unknown setting " + setting + ".");
            }
        }
    }
}
