using System;
using System.Threading.Tasks;
using Windows.Storage;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    public interface ISettingsService
    {
        event EventHandler<AppSetting> SettingChanged;
        event EventHandler ScanSaveLocationChanged;

        void SetSetting(AppSetting setting, object value);
        object GetSetting(AppSetting setting);

        StorageFolder ScanSaveLocation
        {
            get;
        }

        bool IsScanSaveLocationDefault
        {
            get;
        }

        Task SetScanSaveLocationAsync(StorageFolder folder);
        Task ResetScanSaveLocationAsync();
    }
}
