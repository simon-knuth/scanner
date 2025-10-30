using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class ShowIndeterminateProgressDialogMessage : RequestMessage<Task>
    {
        public readonly string Title;
        public readonly Task Process;

        public ShowIndeterminateProgressDialogMessage(string title, Task process)
        {
            Title = title;
            Process = process;
        }
    }
}
