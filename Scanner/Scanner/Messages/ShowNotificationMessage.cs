using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages
{
    internal class ShowNotificationMessage
    {
        public readonly Notification Notification;

        public ShowNotificationMessage(Notification notification)
        {
            Notification = notification;
        }
    }
}
