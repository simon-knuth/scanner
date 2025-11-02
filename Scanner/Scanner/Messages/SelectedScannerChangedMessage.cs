using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class SelectedScannerChangedMessage
    {
        public readonly IScanningDevice? SelectedScanner;

        public SelectedScannerChangedMessage(IScanningDevice? selectedScanner)
        {
            SelectedScanner = selectedScanner;
        }
    }
}
