using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Messages
{
    /// <summary>
    /// Invokes the Windows share sheet. Must be predeced by a <see cref="SetShareFilesMessage"/> to set the share sheet content.
    /// </summary>
    internal class InvokeShareUIMessage
    {
        public InvokeShareUIMessage()
        {
            
        }
    }
}
