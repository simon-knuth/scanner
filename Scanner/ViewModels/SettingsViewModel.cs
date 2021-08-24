using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using static Scanner.Services.SettingsEnums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ISettingsService SettingsService => Ioc.Default.GetRequiredService<ISettingsService>();
        private ILogService _LogService = Ioc.Default.GetService<ILogService>();

        public int SettingSaveLocationType
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingSaveLocationType);
            set => SettingsService.SetSetting(AppSetting.SettingSaveLocationType, value);
        }

        public int SettingAppTheme
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingAppTheme);
            set => SettingsService.SetSetting(AppSetting.SettingAppTheme, value);
        }

        public int SettingAutoRotate
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingAutoRotate);
            set => SettingsService.SetSetting(AppSetting.SettingAutoRotate, value);
        }

        public bool SettingAppendTime
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingAppendTime);
            set => SettingsService.SetSetting(AppSetting.SettingAppendTime, value);
        }

        public int SettingEditorOrientation
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingEditorOrientation);
            set => SettingsService.SetSetting(AppSetting.SettingEditorOrientation, value);
        }

        public bool SettingRememberScanOptions
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingRememberScanOptions);
            set => SettingsService.SetSetting(AppSetting.SettingRememberScanOptions, value);
        }

        public bool SettingErrorStatistics
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingErrorStatistics);
            set => SettingsService.SetSetting(AppSetting.SettingErrorStatistics, value);
        }

        public string CurrentVersion => GetCurrentVersion();

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsViewModel()
        {
            SettingAutoRotate = (int)SettingsService.GetSetting(AppSetting.SettingAutoRotate);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }
    }
}
