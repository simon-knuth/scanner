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
        private ISettingsService SettingsService => Ioc.Default.GetService<ISettingsService>();

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

        public object SettingAutoRotate
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingAutoRotate);
            set
            {
                if (value.GetType() == Type.GetType("System.Boolean"))
                {
                    if ((bool)value == false)
                    {
                        SettingsService.SetSetting(AppSetting.SettingAutoRotate, SettingsEnums.SettingAutoRotate.Off);
                    }
                }
                else
                {
                    SettingsService.SetSetting(AppSetting.SettingAutoRotate, value);
                }
            }
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

        public bool SettingErrorStatistics
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingErrorStatistics);
            set => SettingsService.SetSetting(AppSetting.SettingErrorStatistics, value);
        }

        public SettingsViewModel()
        {
            
        }

        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }
    }
}
