using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using WinUI = Microsoft.UI.Xaml.Controls;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using static Scanner.Services.Messenger.MessengerEnums;
using Windows.UI.Xaml.Controls;
using Microsoft.Toolkit.Mvvm.Input;
using Scanner.Services;
using static Scanner.Services.SettingsEnums;
using Microsoft.Toolkit.Mvvm.DependencyInjection;

namespace Scanner.ViewModels
{
    public class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ISettingsService SettingsService => Ioc.Default.GetRequiredService<ISettingsService>();

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
