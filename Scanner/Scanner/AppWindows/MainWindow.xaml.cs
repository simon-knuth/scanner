using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static Scanner.Helpers.Helpers;
using WinRT.Interop;
using WinUIEx;


namespace Scanner.AppWindows
{
    public sealed partial class MainWindow : WindowEx
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private AppWindowTitleBar titlebar;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public MainWindow()
        {
            this.InitializeComponent();
            PersistenceId = "MainWindow";

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
            titlebar.PreferredHeightOption = TitleBarHeightOption.Tall;
            titlebar.ButtonBackgroundColor = Colors.Transparent;
            titlebar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}
