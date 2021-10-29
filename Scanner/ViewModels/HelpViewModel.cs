using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services.Messenger;
using System;
using static HelpViewEnums;
using Microsoft.Toolkit.Mvvm.Input;
using System.Threading.Tasks;
using Windows.System;

namespace Scanner.ViewModels
{
    public class HelpViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public event EventHandler<HelpTopic> HelpTopicRequested;
        public RelayCommand DisposeCommand;
        public AsyncRelayCommand LaunchScannerSettingsCommand;
        public AsyncRelayCommand LaunchWifiSettingsCommand;
        public RelayCommand SettingsRequestCommand;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public HelpViewModel()
        {
            WeakReferenceMessenger.Default.Register<HelpRequestMessage>(this, (r, m) => HelpRequestMessage_Received(r, m));
            DisposeCommand = new RelayCommand(Dispose);
            LaunchScannerSettingsCommand = new AsyncRelayCommand(LaunchScannerSettings);
            LaunchWifiSettingsCommand = new AsyncRelayCommand(LaunchWifiSettings);
            SettingsRequestCommand = new RelayCommand(SettingsRequest);
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
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
            }
            catch (Exception) { }
        }

        private async Task LaunchWifiSettings()
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
            }
            catch (Exception) { }
        }

        private void SettingsRequest()
        {
            Messenger.Send(new SettingsRequestMessage());
        }
    }
}
