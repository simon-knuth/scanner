using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    public interface ISettingsService
    {
        void SetSetting(AppSetting setting, object value);
        object GetSetting(AppSetting setting);
    }
}
