using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class InvokeScanMergeMessage
    {
        public ScanMergeConfig ScanMergeConfig;


        public InvokeScanMergeMessage(ScanMergeConfig scanMergeConfig)
        {
            ScanMergeConfig = scanMergeConfig;
        }
    }
}
