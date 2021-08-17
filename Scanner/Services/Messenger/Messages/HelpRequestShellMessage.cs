using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using static Scanner.Services.Messenger.MessengerEnums;
using static HelpViewEnums;

namespace Scanner.Services.Messenger
{
    class HelpRequestShellMessage
    {
        public HelpTopic HelpTopic;

        public HelpRequestShellMessage(HelpTopic helpTopic)
        {
            HelpTopic = helpTopic;
        }
    }
}
