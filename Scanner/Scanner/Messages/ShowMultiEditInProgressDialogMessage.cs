using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class ShowMultiEditInProgressDialogMessage : RequestMessage<Task>
    {
        public Task Process;

        public ShowMultiEditInProgressDialogMessage(Task process)
        {
            Process = process;
        }
    }
}
