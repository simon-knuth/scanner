using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages;

internal class ShowSettingsMessage
{
    public SettingsViewModelIntent? Intent;

    public ShowSettingsMessage(SettingsViewModelIntent? intent = null)
    {
        Intent = intent;
    }
}
