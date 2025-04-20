using CommunityToolkit.Mvvm.Messaging.Messages;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class SelectedScannerRequestMessage : RequestMessage<IScanningDevice?>
    {
        
    }
}
