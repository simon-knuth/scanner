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
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;


namespace Scanner.AppWindows
{
    [ObservableRecipientAttribute]
    [ObservableObjectAttribute]
    public sealed partial class MainWindow : WindowEx
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public IDataTransferManagerInterop DataTransferManagerInterop { get; private set; }


        private AppWindowTitleBar titlebar;

        private List<StorageFile> shareFiles;


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

            SetUpSharing();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void SetUpSharing()
        {
            WeakReferenceMessenger.Default.Register<SetShareFilesMessage>(this, (r, m) => shareFiles = m.Files);
            DataTransferManagerInterop = Windows.ApplicationModel.DataTransfer.DataTransferManager.As
                <IDataTransferManagerInterop>();

            IntPtr result = DataTransferManagerInterop.GetForWindow(WindowNative.GetWindowHandle(this), DataTransferManagerInteropGuids.Dtm_iid);
            Windows.ApplicationModel.DataTransfer.DataTransferManager dataTransferManager = WinRT.MarshalInterface
                <Windows.ApplicationModel.DataTransfer.DataTransferManager>.FromAbi(result);

            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
        }

        private void DataTransferManager_DataRequested(Windows.ApplicationModel.DataTransfer.DataTransferManager sender, Windows.ApplicationModel.DataTransfer.DataRequestedEventArgs args)
        {
            if (shareFiles != null && shareFiles.Count >= 1)
            {
                args.Request.Data.SetStorageItems(shareFiles);

                if (shareFiles.Count == 1)
                    args.Request.Data.Properties.Title = shareFiles[0].Name;
                else
                    args.Request.Data.Properties.Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ShareUITitleMultipleFiles);
            }
        }
    }
}
