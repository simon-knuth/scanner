using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;

namespace Scanner.ViewModels
{
    public class SetupDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();

        public RelayCommand ConfirmSettingsCommand;

        private bool _ProxySettingErrorStatistics = true;
        public bool ProxySettingErrorStatistics
        {
            get => _ProxySettingErrorStatistics;
            set
            {
                SetProperty(ref _ProxySettingErrorStatistics, value);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SetupDialogViewModel()
        {
            ConfirmSettingsCommand = new RelayCommand(ConfirmSettings);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ConfirmSettings()
        {
            LogService?.Log.Information("ConfirmSettings");

            SettingsService.SetSetting(AppSetting.SettingErrorStatistics, ProxySettingErrorStatistics);
            SettingsService.SetSetting(AppSetting.SetupCompleted, true);

            Messenger.Send(new SetupCompletedMessage());
        }
    }
}
