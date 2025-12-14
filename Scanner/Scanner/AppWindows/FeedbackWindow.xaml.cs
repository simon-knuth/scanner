using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Messages;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;


namespace Scanner.AppWindows
{
    public sealed partial class FeedbackWindow : WindowEx
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private AppWindowTitleBar titlebar;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FeedbackWindow()
        {
            this.InitializeComponent();

            // backdrop
            if (IsWindows11())
            {
                SystemBackdrop = new MicaBackdrop();
            }
            else
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }

            // titlebar
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);
            titlebar = appWindow.TitleBar;
            titlebar.ExtendsContentIntoTitleBar = true;
            titlebar.ButtonBackgroundColor = Colors.Transparent;
            titlebar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // icon
            string? iconPath = Environment.ProcessPath;
            if (iconPath != null)
            {
                iconPath = Path.GetDirectoryName(iconPath);

                if (iconPath != null)
                {
                    iconPath = Path.Combine(iconPath, "Assets/Icon.ico");
                    AppWindow.SetIcon(iconPath);
                }
            }
        }

        private void FeedbackView_FeedbackSent(object sender, EventArgs e)
        {
            this.Close();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
