﻿﻿using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using Scanner.Models;

namespace Scanner.Services.Messenger
{
    class SelectedScannerChangedMessage : ValueChangedMessage<DiscoveredScanner>
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        DiscoveredScanner Scanner;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SelectedScannerChangedMessage(DiscoveredScanner scanner) : base(scanner)
        {
            Scanner = scanner;
        }
    }
}