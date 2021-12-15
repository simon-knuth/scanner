using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services.Messenger;
using System;
using static Enums;
using Microsoft.Toolkit.Mvvm.Input;
using System.Threading.Tasks;
using Windows.System;
using Scanner.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;

namespace Scanner.ViewModels
{
    public class HelpViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();

        public event EventHandler<HelpTopic> HelpTopicRequested;
        public RelayCommand DisposeCommand;
        public AsyncRelayCommand LaunchScannerSettingsCommand;
        public AsyncRelayCommand LaunchWifiSettingsCommand;
        public RelayCommand SettingsScanOptionsRequestCommand;
        public RelayCommand SettingsSaveLocationRequestCommand;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public HelpViewModel()
        {
            WeakReferenceMessenger.Default.Register<HelpRequestMessage>(this, (r, m) => HelpRequestMessage_Received(r, m));
            DisposeCommand = new RelayCommand(Dispose);
            LaunchScannerSettingsCommand = new AsyncRelayCommand(LaunchScannerSettings);
            LaunchWifiSettingsCommand = new AsyncRelayCommand(LaunchWifiSettings);
            SettingsScanOptionsRequestCommand = new RelayCommand(() => SettingsRequest(SettingsSection.ScanOptions));
            SettingsSaveLocationRequestCommand = new RelayCommand(() => SettingsRequest(SettingsSection.SaveLocation));
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void HelpRequestMessage_Received(object r, HelpRequestMessage m)
        {
            HelpTopicRequested?.Invoke(this, m.HelpTopic);
        }

        private async Task LaunchScannerSettings()
        {
            LogService?.Log.Information("LaunchScannerSettings");
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
            }
            catch (Exception) { }
        }

        private async Task LaunchWifiSettings()
        {
            LogService?.Log.Information("LaunchWifiSettings");
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
            }
            catch (Exception) { }
        }

        private void SettingsRequest(SettingsSection section)
        {
            LogService?.Log.Information("SettingsRequest");
            Messenger.Send(new SettingsRequestShellMessage(section));
        }
    }
}
