using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using WinUI = Microsoft.UI.Xaml.Controls;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using static Scanner.Services.Messenger.MessengerEnums;
using Windows.UI.Xaml.Controls;
using static HelpViewEnums;
using Microsoft.Toolkit.Mvvm.Input;

namespace Scanner.ViewModels
{
    public class HelpViewModel : ObservableRecipient, IDisposable
    {
        public event EventHandler<HelpTopic> HelpTopicRequested;
        public RelayCommand DisposeCommand;
        
        public HelpViewModel()
        {
            WeakReferenceMessenger.Default.Register<HelpRequestMessage>(this, (r, m) => HelpRequestMessage_Received(r, m));
            DisposeCommand = new RelayCommand(Dispose);
        }

        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void HelpRequestMessage_Received(object r, HelpRequestMessage m)
        {
            HelpTopicRequested?.Invoke(this, m.HelpTopic);
        }
    }
}
