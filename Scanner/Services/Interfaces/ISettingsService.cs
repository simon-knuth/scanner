using System;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    public interface ISettingsService
    {
        event EventHandler<AppSetting> SettingChanged;

        void SetSetting(AppSetting setting, object value);
        object GetSetting(AppSetting setting);
    }
}
