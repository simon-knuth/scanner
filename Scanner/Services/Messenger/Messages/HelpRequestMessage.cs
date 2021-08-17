using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using static Scanner.Services.Messenger.MessengerEnums;
using static HelpViewEnums;

namespace Scanner.Services.Messenger
{
    class HelpRequestMessage
    {
        public HelpTopic HelpTopic;

        public HelpRequestMessage(HelpTopic helpTopic)
        {
            HelpTopic = helpTopic;
        }
    }
}
