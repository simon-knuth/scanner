using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using Scanner.AppWindows;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages;

internal class MainWindowClosingMessage
{
    public readonly MainWindow MainWindow;

    public MainWindowClosingMessage(MainWindow mainWindow)
    {
        MainWindow = mainWindow;
    }
}
