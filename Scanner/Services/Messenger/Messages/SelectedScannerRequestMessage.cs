﻿﻿using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using Scanner.Models;

namespace Scanner.Services.Messenger
{
    class SelectedScannerRequestMessage : RequestMessage<DiscoveredScanner>
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SelectedScannerRequestMessage()
        {

        }
    }
}