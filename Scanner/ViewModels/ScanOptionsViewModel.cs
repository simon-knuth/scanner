using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using WinUI = Microsoft.UI.Xaml.Controls;
using Scanner.Services.Messenger;
using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using static Scanner.Services.Messenger.MessengerEnums;
using Microsoft.Toolkit.Mvvm.Input;

namespace Scanner.ViewModels
{
    public class ScanOptionsViewModel : ObservableRecipient
    {
        public RelayCommand HelpRequestScannerDiscoveryCommand;
        public RelayCommand HelpRequestChooseResolutionCommand;
        public RelayCommand HelpRequestChooseFileFormatCommand;
        
        public ScanOptionsViewModel()
        {
            HelpRequestScannerDiscoveryCommand = new RelayCommand(HelpRequestScannerDiscovery);
            HelpRequestChooseResolutionCommand = new RelayCommand(HelpRequestChooseResolution);
            HelpRequestChooseFileFormatCommand = new RelayCommand(HelpRequestChooseFileFormat);
        }

        private void HelpRequestScannerDiscovery()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ScannerDiscovery));
        }

        private void HelpRequestChooseResolution()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseResolution));
        }

        private void HelpRequestChooseFileFormat()
        {
            Messenger.Send(new HelpRequestShellMessage(HelpViewEnums.HelpTopic.ChooseFileFormat));
        }
    }
}
